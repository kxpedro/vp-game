using Godot;

// Modo 2D: ao renascer (HUD "Renascer" depois da morte) o Virgil volta ao hub saindo do próprio caixão. A sala troca
// o caixão de sempre pela peça coffin_virgil_rise (quadro 0 = sem tampa, quadros 1..N-1 = tampa da fechada à
// entreaberta, iguais à peça de sempre no último), a tampa é empurrada por dentro, o Virgil sobe do forro, pula
// para o chão e dá uns passos. A câmera fica parada no caixão durante a cena; o controle volta no fim.
public static class CoffinRise
{
    /// <summary>Próxima entrada no hub sai do caixão. Ligado pelo "Renascer" do game over; o hub consome.</summary>
    public static bool Pending { get; set; }

    /// <summary>Cena tocando: o HUD bloqueia o controle (HUD.IsGameplayInputBlocked).</summary>
    public static bool IsBusy => _driver != null && GodotObject.IsInstanceValid(_driver) && _driver.IsInsideTree();

    public const string PieceSuffix = "_rise";

    // Tempos da cena (s). A tela de carregamento clareia em ~0,6 s.
    private const float StartDelay = 0.7f;
    private const float JoltSeconds = 0.12f;
    private const float JoltPause = 0.4f;
    private const float SlideFrameSeconds = 0.09f;
    private const float RiseSeconds = 1.0f;
    private const float StandSeconds = 0.3f;
    private const float HopSeconds = 0.45f;
    private const float HopArc = 0.35f;
    private const float WalkSeconds = 0.45f;
    // Medidas do caixão do Virgil no kit (props.py: meio comprimento 29, estrado até 35, tampa a partir de 12 do pé
    // da cabeceira, tampo do caixão em z 27).
    private const float OpeningFromCenter = 21.5f;
    private const float LandingFromCenter = 50.0f;
    private const float CoffinTopZ = 27.0f;
    // Pés abaixo do forro o bastante para a cabeça (~47 de z) ficar escondida dentro do caixão.
    private const float HiddenFeet = -1.1f;
    private const float FloorHeight = 0.0f;

    // Quadros da tampa (props.py VIRGIL_LID_FRAMES): 1 = fechada, 2 e 4 = pancadas, 5-7 = erguida deslizando,
    // 8 = caída na posição de sempre (entreaberta).
    private const int LastLidFrame = 8;

    private static Node _driver;

    /// <summary>Nome da peça do layout trocada pela da cena (coffin_virgil_sw -> coffin_virgil_rise_sw).</summary>
    public static string RisePiece(string piece) => piece.Replace("coffin_virgil_", "coffin_virgil" + PieceSuffix + "_");

    /// <summary>
    /// Toca a cena no caixão <paramref name="coffin"/> (peça coffin_virgil_rise_* já montada na sala). Quem chama
    /// põe o caixão no layout e já trocou a peça; a câmera é a da sala.
    /// </summary>
    public static void Play(Node parent, PixelRoom room, PixelRoomProp coffin, PlayerController player, IsoCamera camera)
    {
        var piece = "props/" + coffin.Name;
        if (!room.SetAnimationFrame(piece, 1))
        {
            GD.PushWarning($"CoffinRise: peca animada {piece} ausente; o jogador nasce no lugar de sempre.");
            return;
        }

        // Eixo do caixão: "sw" é comprido em y e abre na cabeceira de y menor; "se" é comprido em x.
        var along = coffin.Name.EndsWith("_se") ? new Vector2(-1.0f, 0.0f) : new Vector2(0.0f, -1.0f);
        var center = new Vector2(coffin.Gx * 64.0f + coffin.Dx + 32.0f, coffin.Gy * 64.0f + coffin.Dy + 32.0f);
        var opening = center + along * OpeningFromCenter;
        var landing = center + along * LandingFromCenter;
        var top = CoffinTopZ * PixelRoom.HeightMeters;
        var inside = room.KitToWorld(opening.X, opening.Y, HiddenFeet);
        var standing = room.KitToWorld(opening.X, opening.Y, top);
        var floor = room.KitToWorld(landing.X, landing.Y, FloorHeight);
        var forward = new Vector3(along.X, 0.0f, along.Y);

        player.SetCutsceneHold(true);
        player.GlobalPosition = inside;
        // Sobe de frente para a câmera e vira para a saída na hora do pulo.
        player.LookAt(inside - forward, Vector3.Up);
        if (camera != null)
            camera.FocusOverride = room.KitToWorld((opening.X + landing.X) * 0.5f, (opening.Y + landing.Y) * 0.5f, 0.4f);

        var driver = new Node { Name = "CoffinRise" };
        _driver = driver;
        parent.AddChild(driver);
        var tween = driver.CreateTween();
        void Frame(int k) => tween.TweenCallback(Callable.From(() => room.SetAnimationFrame(piece, k)));
        void Shake(float strength) => tween.TweenCallback(Callable.From(() => camera?.Shake(strength, 0.15f)));

        // Tampa: duas pancadas por dentro; depois sobe, desliza e cai na posição de sempre.
        tween.TweenInterval(StartDelay);
        Frame(2);
        Shake(0.04f);
        tween.TweenInterval(JoltSeconds);
        Frame(3);
        tween.TweenInterval(JoltPause);
        Frame(4);
        Shake(0.07f);
        tween.TweenInterval(JoltSeconds);
        Frame(5);
        tween.TweenInterval(SlideFrameSeconds);
        Frame(6);
        tween.TweenInterval(SlideFrameSeconds);

        // Sobe do forro enquanto a tampa termina de deslizar.
        tween.TweenProperty(player, "global_position", standing, RiseSeconds)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        for (var k = 7; k <= LastLidFrame; k++)
        {
            var frame = k;
            tween.Parallel().TweenCallback(Callable.From(() =>
            {
                room.SetAnimationFrame(piece, frame);
                if (frame == LastLidFrame)
                    camera?.Shake(0.08f, 0.18f);
            })).SetDelay(SlideFrameSeconds * (k - 6));
        }
        tween.TweenInterval(StandSeconds);

        // Pulo para fora pela cabeceira, em arco, até o chão.
        tween.TweenCallback(Callable.From(() =>
        {
            player.LookAt(player.GlobalPosition + forward, Vector3.Up);
            player.SetCutsceneHold(true, moving: true);
        }));
        tween.TweenMethod(Callable.From<float>(t =>
        {
            var position = standing.Lerp(floor, t);
            position.Y = Mathf.Lerp(standing.Y, floor.Y, t) + 4.0f * HopArc * t * (1.0f - t);
            player.GlobalPosition = position;
        }), 0.0f, 1.0f, HopSeconds);

        // Devolve o controle com alguns passos para a frente; a câmera volta a seguir o jogador.
        tween.TweenCallback(Callable.From(() =>
        {
            player.SetCutsceneHold(false);
            player.StartScriptedWalk(forward, WalkSeconds);
            if (camera != null)
                camera.FocusOverride = null;
        }));
        tween.TweenInterval(WalkSeconds);
        tween.TweenCallback(Callable.From(driver.QueueFree));
    }
}
