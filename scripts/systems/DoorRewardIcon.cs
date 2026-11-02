using Godot;
using System;

// Medalhão de recompensa (portas de sala e item no chão; docs/design/portas-recompensas.md, seção 8). Provisório, sem arte nova.
// Estilo: disco escuro com aro dourado brilhante e emblema no centro (~0,7 m). Sem rótulo de texto: o prompt de
// interação (ao se aproximar) diz o que é a recompensa.
// Realce: disco ameixa (não preto), aro dourado largo, brilho forte; na porta o medalhão é ampliado (ScenePortal.AddRewardIcon).
// Emblemas em pixel art 32x32 (assets/pixel_art/rewards/, tools/pixel_art/rewards/draw_reward_icons.py).
// Emblema por tipo (forma e luminância primeiro, cor só reforça):
//   Bênção de Sangue: ícone da família sorteada (ou losango dourado claro, se faltar arte);
//   Fruta do Diabo: esfera vermelho-escuro com haste (emissiva, para não sumir no disco escuro);
//   Moedas de Sangue: pilha de três discos bronze;
//   Bigorna: silhueta de bigorna em aço claro (base larga, cintura, tampo e chifre);
//   Ícor (ex-Sangue Amaldiçoado): o frasco em pixel art do HUD (assets/pixel_art/hud/cursed_blood.png), sangue vermelho com o fundo violeta.
// Tudo fica de frente para a câmera (billboard), para ler igual de qualquer ponto da sala.
// Quem chama: ScenePortal.AddRewardIcon (porta, medalhão a 3,5 m do piso, acima do lintel) e RewardPickup (item no chão).
public static class DoorRewardIcon
{
    // Medalhão: lado do quadro de 1,2 m. O aro fica a 90% do raio (~1,08 m de diâmetro); o resto é o brilho externo.
    public const float MedallionSize = 1.2f;
    private const int MedallionTextureSize = 128;
    // Emblema de Bênção (ícone da família ou losango): lado de 0,7 m, dentro do fundo escuro do disco.
    private const float EmblemSize = 0.78f;
    // O emblema fica um pouco à frente do disco (+Z local, lado da sala), para não brigar no mesmo plano.
    private const float EmblemDepth = 0.03f;
    private const int DiamondTextureSize = 64;

    private const string BlessingHex = "FFD541";
    // Disco claro o bastante para o emblema escuro ler (ameixa do AAP-64), aro dourado vivo e brilho externo forte.
    private const string FillHex = "5B3138";
    private const string RingDarkHex = "F9A31B";
    private const string RingHex = "FEF3C0";
    private const string GlowHex = "FFD541";

    private const string FruitIconPath = "res://assets/pixel_art/rewards/devils_fruit.png";
    private const string AnvilIconPath = "res://assets/pixel_art/rewards/anvil.png";
    private const string CoinsIconPath = "res://assets/pixel_art/rewards/blood_coins.png";
    private const string CursedBloodIconPath = "res://assets/pixel_art/hud/cursed_blood.png";

    // Texturas geradas por código, uma vez (medalhão e losango de fallback).
    private static ImageTexture _medallionTexture;
    private static ImageTexture _diamondTexture;

    /// <summary>Medalhão da porta (ScenePortal.AddRewardIcon).</summary>
    public static void PopulateDoor(Node3D icon, RoomRewardDoor door) => Build(icon, door);

    /// <summary>Medalhão do item no chão (RewardPickup): o mesmo desenho da porta.</summary>
    public static void PopulatePickup(Node3D icon, RoomRewardDoor door) => Build(icon, door);

    /// <summary>Rótulo curto localizado do tipo (reward.door.*.short). Bênção mostra a família; Moedas, o valor.</summary>
    public static string ShortLabel(RoomRewardDoor door)
    {
        return door.Type switch
        {
            RoomRewardType.Blessing => BlessingLabel(door),
            RoomRewardType.DevilsFruit => Localization.Text("reward.door.fruit.short"),
            RoomRewardType.Anvil => Localization.Text("reward.door.anvil.short"),
            RoomRewardType.CursedBlood => Localization.TextFormat("reward.door.cursed_blood.short", door.CoinAmount),
            _ => Localization.TextFormat("reward.door.coins.short", door.CoinAmount),
        };
    }

    private static void Build(Node3D icon, RoomRewardDoor door)
    {
        if (icon == null || door == null)
            return;

        AddMedallion(icon);

        var emblem = new Node3D { Name = "Emblem", Position = new Vector3(0.0f, 0.0f, EmblemDepth) };
        icon.AddChild(emblem);
        switch (door.Type)
        {
            case RoomRewardType.Blessing:
                AddBlessingEmblem(emblem, door.BlessingPlan);
                break;
            case RoomRewardType.DevilsFruit:
                AddPixelEmblem(emblem, "FruitIcon", FruitIconPath);
                break;
            case RoomRewardType.Anvil:
                AddPixelEmblem(emblem, "AnvilIcon", AnvilIconPath);
                break;
            case RoomRewardType.CursedBlood:
                AddPixelEmblem(emblem, "CursedBloodIcon", CursedBloodIconPath);
                break;
            default:
                AddPixelEmblem(emblem, "CoinsIcon", CoinsIconPath);
                break;
        }
    }

    // Disco do medalhão: quadro com textura gerada (fundo escuro, aro dourado e brilho externo que se apaga).
    private static void AddMedallion(Node3D icon)
    {
        icon.AddChild(new Sprite3D
        {
            Name = "Medallion",
            Texture = MedallionTexture(),
            PixelSize = MedallionSize / MedallionTextureSize,
            Shaded = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        });
    }

    // Bênção: ícone da família sorteada (ou o da primeira bênção do plano; Stirps não tem arquivo próprio). Sem arte, losango.
    private static void AddBlessingEmblem(Node3D emblem, RoomBlessingPlan plan)
    {
        var texture = plan == null || plan.Offers.Count == 0 ? null : FamilyTexture(plan);
        if (texture != null)
        {
            // O lado maior da arte mede EmblemSize (as artes são quadradas ou quase).
            emblem.AddChild(new Sprite3D
            {
                Name = "BlessingFamilyIcon",
                Texture = texture,
                PixelSize = EmblemSize / Mathf.Max(Mathf.Max(texture.GetWidth(), texture.GetHeight()), 1),
                Shaded = false,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
            });
            return;
        }

        emblem.AddChild(new Sprite3D
        {
            Name = "BlessingDiamond",
            Texture = DiamondTexture(),
            PixelSize = EmblemSize / DiamondTextureSize,
            Shaded = false,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        });
    }

    // Emblema em pixel art (32x32, AAP-64), sem filtro (pixels nítidos). Sem o arquivo, fica só o disco.
    private static void AddPixelEmblem(Node3D emblem, string name, string path)
    {
        var texture = LoadTexture(path);
        if (texture == null)
        {
            GD.PushWarning($"DoorRewardIcon: ícone ausente ({path}).");
            return;
        }
        emblem.AddChild(new Sprite3D
        {
            Name = name,
            Texture = texture,
            PixelSize = EmblemSize / Mathf.Max(Mathf.Max(texture.GetWidth(), texture.GetHeight()), 1),
            Shaded = false,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        });
    }

    // Ícone da família: <familia>-icon.png (BloodBlessingCatalog.FamilyIconPath). Stirps usa o ícone da primeira bênção do plano.
    // Sem nenhum, null (losango).
    private static Texture2D FamilyTexture(RoomBlessingPlan plan)
    {
        var texture = LoadTexture(BloodBlessingCatalog.FamilyIconPath(plan.Family));
        if (texture != null)
            return texture;
        foreach (var offer in plan.Offers)
        {
            texture = LoadTexture(offer.Definition.IconPath);
            if (texture != null)
                return texture;
        }
        return null;
    }

    // "Bênção <Família>" quando há plano (o nome da família é próprio, igual nos três idiomas); "Bênção" sem plano.
    private static string BlessingLabel(RoomRewardDoor door)
    {
        var family = door.BlessingPlan?.Family;
        return string.IsNullOrEmpty(family)
            ? Localization.Text("reward.door.blessing.short")
            : Localization.TextFormat("reward.door.blessing.family", family);
    }

    private static Texture2D LoadTexture(string path)
    {
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
            return null;
        return ResourceLoader.Load<Texture2D>(path);
    }

    private static ImageTexture MedallionTexture() => _medallionTexture ??= Generate(MedallionTextureSize, MedallionPixel);

    private static ImageTexture DiamondTexture() => _diamondTexture ??= Generate(DiamondTextureSize, DiamondPixel);

    // dx, dy em [-1, 1] (borda do quadro = 1). Até 0,8: fundo escuro. De 0,8 a 0,9: aro dourado (mais claro no meio).
    // De 0,9 a 1: brilho externo que se apaga. Fora disso: transparente.
    private static Color MedallionPixel(float dx, float dy)
    {
        var r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r <= 0.76f)
            return new Color(Color.FromHtml("#" + FillHex), 1.0f);
        if (r <= 0.88f)
        {
            var t = Mathf.Sin((r - 0.76f) / 0.12f * Mathf.Pi);
            return Color.FromHtml("#" + RingDarkHex).Lerp(Color.FromHtml("#" + RingHex), t);
        }
        if (r <= 1.0f)
        {
            var fade = 1.0f - (r - 0.88f) / 0.12f;
            return new Color(Color.FromHtml("#" + GlowHex), 0.85f * fade);
        }
        return new Color(0.0f, 0.0f, 0.0f, 0.0f);
    }

    // Losango (|dx| + |dy| <= 0,95) em dourado claro, usado só quando a Bênção não tem arte.
    private static Color DiamondPixel(float dx, float dy)
    {
        return Mathf.Abs(dx) + Mathf.Abs(dy) <= 0.95f
            ? Color.FromHtml("#" + BlessingHex)
            : new Color(0.0f, 0.0f, 0.0f, 0.0f);
    }

    private static ImageTexture Generate(int size, Func<float, float, Color> pixel)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2.0f - 1.0f;
                var dy = (y + 0.5f) / size * 2.0f - 1.0f;
                image.SetPixel(x, y, pixel(dx, dy));
            }
        return ImageTexture.CreateFromImage(image);
    }
}
