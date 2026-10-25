using Godot;
using System.Collections.Generic;

// Modo 2D: mostra uma sala montada pelo PixelRoomComposer dentro da cena 3D.
// A imagem fica num quadrilátero no chão, vista por uma câmera ortográfica 2:1 (yaw 45, pitch 30). O mapa de altura
// deixa o shader escrever a profundidade real de cada pixel, então o jogador, os inimigos e qualquer objeto 3D passam
// atrás das paredes e dos caixões. Paredes e obstáculos viram colisões invisíveis.
public partial class PixelRoom : Node3D
{
    // 1 pixel da arte = 0,033 m no plano da tela, o mesmo pixel do sprite do Virgil (PlayerSpriteVisual.PixelSize).
    public const float PixelMeters = 0.033f;
    public static readonly float UnitMeters = PixelMeters * Mathf.Sqrt2;
    // Altura real de 1 unidade de z do kit (a projeção 2:1 do kit achata a altura em sqrt(2/3)).
    public static readonly float HeightMeters = UnitMeters * Mathf.Sqrt(2.0f / 3.0f);
    public static readonly float TileMeters = 64.0f * UnitMeters;
    public const float CameraPitchDegrees = 30.0f;
    public const float CameraYawDegrees = 45.0f;
    public const float CameraDistance = 80.0f;
    private const float WallHeight = 3.2f;
    // Espessura das paredes do kit (T = 22 unidades em tools/pixel_art/crypt_kit/walls.py), da borda do tile para fora.
    private static readonly float WallThickness = 22.0f * UnitMeters;

    // Pixels da tela por pixel da arte; 0 = automático (2 até 1080p, 3 acima).
    [Export] public int PixelScale { get; set; }

    // Ponto do kit (unidades) que fica na origem da cena.
    public Vector2 CenterUnits { get; set; }
    public PixelRoomPicture Picture { get; private set; }
    // Layout da sala montada: quem troca bordas nele (PixelDoorWall) chama RecomposeAsync para redesenhar.
    public PixelRoomLayout Layout { get; private set; }

    // Quadros por segundo das peças animadas do layout (fonte de sangue).
    private const float AnimationFps = 8.0f;
    // Leva a peça animada um pouco para a frente da câmera: ela fica exatamente sobre o quadro 0 já desenhado no
    // fundo da sala e sem isso as duas brigariam pela profundidade.
    private const float AnimationDepthBias = 0.03f;

    private Camera3D _camera;
    private ShaderMaterial _backdropMaterial;
    private int _recomposeVersion;
    private readonly List<(MeshInstance3D Mesh, ImageTexture[] Colors, ImageTexture[] Heights)> _animations = new();
    private double _animationTime;
    private int _animationFrame = -1;

    /// <summary>
    /// Deixa um sprite de personagem em pé na vertical (billboard só no eixo Y) e estica a altura para compensar o
    /// ângulo da câmera, então ele sai na tela do mesmo tamanho. Com o billboard comum o quadro deita para trás junto
    /// com a câmera: a cabeça fica até ~0,6 m atrás dos pés e some dentro das paredes do fundo e dos objetos altos,
    /// como se o personagem entrasse neles.
    /// </summary>
    public static void StandUpright(SpriteBase3D sprite)
    {
        if (sprite == null)
            return;
        sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        sprite.Scale = new Vector3(1.0f, 1.0f / Mathf.Cos(Mathf.DegToRad(CameraPitchDegrees)), 1.0f);
    }

    public Vector3 KitToWorld(float wx, float wy, float height = 0.0f)
    {
        return new Vector3((wx - CenterUnits.X) * UnitMeters, height, (wy - CenterUnits.Y) * UnitMeters);
    }

    public Vector3 TileToWorld(float gx, float gy, float height = 0.0f) => KitToWorld(gx * 64.0f, gy * 64.0f, height);

    public Vector2 WorldToKit(Vector3 world) => new(world.X / UnitMeters + CenterUnits.X, world.Z / UnitMeters + CenterUnits.Y);

    public bool Build(PixelRoomLayout layout, Vector2? centerUnits = null)
    {
        Layout = layout;
        Picture = PixelRoomComposer.Compose(layout);
        if (Picture == null)
            return false;

        CenterUnits = centerUnits ?? Picture.TileBounds.GetCenter();
        CreateBackdrop();
        CreateCollision();
        CreateAnimations();
        return true;
    }

    /// <summary>
    /// Redesenha a sala a partir do <see cref="Layout"/> (já alterado) numa thread de fundo e troca a imagem quando
    /// fica pronta. Só para mudanças de borda (arco no lugar de parede): os tiles, as colisões e o enquadramento não
    /// mudam. Chamadas seguidas valem pela última.
    /// </summary>
    public void RecomposeAsync()
    {
        if (Layout == null || _backdropMaterial == null)
            return;
        var version = ++_recomposeVersion;
        // Cópia: o layout pode mudar de novo enquanto a thread desenha.
        var layout = new PixelRoomLayout { Ambient = Layout.Ambient, Seed = Layout.Seed, Kit = Layout.Kit };
        foreach (var tile in Layout.Tiles)
            layout.Tiles[tile.Key] = tile.Value;
        foreach (var edge in Layout.Edges)
            layout.Edges[edge.Key] = edge.Value;
        layout.Props.AddRange(Layout.Props);
        System.Threading.Tasks.Task.Run(() =>
        {
            PixelRoomPicture picture = null;
            try
            {
                picture = PixelRoomComposer.Compose(layout);
            }
            catch (System.Exception error)
            {
                GD.PushError($"PixelRoom: falha ao redesenhar a sala: {error.Message}");
            }
            Callable.From(() => ApplyRecomposed(picture, version)).CallDeferred();
        });
    }

    private void ApplyRecomposed(PixelRoomPicture picture, int version)
    {
        if (picture == null || version != _recomposeVersion || !IsInstanceValid(this) || _backdropMaterial == null)
            return;
        Picture = picture;
        _backdropMaterial.SetShaderParameter("color_tex", ImageTexture.CreateFromImage(picture.Color));
        _backdropMaterial.SetShaderParameter("height_tex", ImageTexture.CreateFromImage(picture.Height));
    }

    // Câmera ortográfica travada no ângulo da arte; chamar antes do _Ready da IsoCamera.
    public void ConfigureCamera(IsoCamera camera)
    {
        _camera = camera;
        camera.LockedYawDegrees = CameraYawDegrees;
        camera.LockedPitchDegrees = CameraPitchDegrees;
        camera.LockedZoomDistance = CameraDistance;
        camera.Projection = Camera3D.ProjectionType.Orthogonal;
        camera.KeepAspect = Camera3D.KeepAspectEnum.Height;
        camera.Near = 1.0f;
        camera.Far = CameraDistance * 3.0f;
        UpdateCameraSize();
    }

    // Cena 2D sem tonemap, glow, SSAO, névoa nem ajuste de cor: a luz das velas já está na arte e os pixels saem exatos.
    public static void ConfigureEnvironment(Environment environment)
    {
        if (environment == null)
            return;
        environment.BackgroundMode = Environment.BGMode.Color;
        environment.BackgroundColor = new Color("060608");
        environment.TonemapMode = Environment.ToneMapper.Linear;
        environment.GlowEnabled = false;
        environment.SsaoEnabled = false;
        environment.SsilEnabled = false;
        environment.SdfgiEnabled = false;
        environment.FogEnabled = false;
        environment.VolumetricFogEnabled = false;
        environment.AdjustmentEnabled = false;
    }

    public override void _Process(double delta)
    {
        UpdateCameraSize();
        if (_animations.Count == 0)
            return;
        _animationTime += delta;
        var frame = (int)(_animationTime * AnimationFps);
        if (frame == _animationFrame)
            return;
        _animationFrame = frame;
        foreach (var (mesh, colors, heights) in _animations)
        {
            if (mesh.MaterialOverride is not ShaderMaterial material)
                continue;
            material.SetShaderParameter("color_tex", colors[frame % colors.Length]);
            material.SetShaderParameter("height_tex", heights[frame % heights.Length]);
        }
    }

    // Peças animadas: um quadrilátero por peça, por cima do quadro 0 já desenhado no fundo, trocando os quadros.
    private void CreateAnimations()
    {
        foreach (var animation in Picture.Animations)
        {
            var first = animation.Colors[0];
            var overlay = CreateOverlayMesh($"Anim_{animation.Name.Replace('/', '_')}", first.GetWidth(), first.GetHeight(),
                animation.Gx * 64.0f + animation.Dx, animation.Gy * 64.0f + animation.Dy, animation.TileTop);
            var colors = new ImageTexture[animation.Colors.Count];
            var heights = new ImageTexture[animation.Heights.Count];
            for (var i = 0; i < colors.Length; i++)
            {
                colors[i] = ImageTexture.CreateFromImage(animation.Colors[i]);
                heights[i] = ImageTexture.CreateFromImage(animation.Heights[i]);
            }
            var material = (ShaderMaterial)overlay.MaterialOverride;
            material.SetShaderParameter("depth_bias", AnimationDepthBias);
            material.SetShaderParameter("color_tex", colors[0]);
            material.SetShaderParameter("height_tex", heights[0]);
            _animations.Add((overlay, colors, heights));
        }
    }

    private void UpdateCameraSize()
    {
        if (_camera == null || !IsInstanceValid(_camera))
            return;

        // A janela renderiza o 3D na resolução real; o tamanho ortográfico mantém um múltiplo inteiro de pixels.
        var windowHeight = Mathf.Max(DisplayServer.WindowGetSize().Y, 1);
        var scale = PixelScale > 0 ? PixelScale : (windowHeight > 1100 ? 3 : 2);
        _camera.Size = windowHeight / (float)scale * PixelMeters;
    }

    private void CreateBackdrop()
    {
        var width = Picture.Color.GetWidth();
        var height = Picture.Color.GetHeight();
        var origin = Picture.Origin;

        // Canto da imagem -> ponto do chão (z = 0) que cai naquele pixel.
        Vector3 Ground(float px, float py)
        {
            var sx = px - origin.X;
            var sy = py - origin.Y;
            return KitToWorld(sy + sx * 0.5f, sy - sx * 0.5f, -0.02f);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[] { Ground(0, 0), Ground(width, 0), Ground(width, height), Ground(0, height) };
        arrays[(int)Mesh.ArrayType.TexUV] = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        var material = new ShaderMaterial { Shader = new Shader { Code = BackdropShader } };
        _backdropMaterial = material;
        material.SetShaderParameter("color_tex", ImageTexture.CreateFromImage(Picture.Color));
        material.SetShaderParameter("height_tex", ImageTexture.CreateFromImage(Picture.Height));
        // Altura guardada -> distância ao longo do raio da câmera: altura real / sen(30°).
        material.SetShaderParameter("ray_per_unit", HeightMeters / Mathf.Sin(Mathf.DegToRad(CameraPitchDegrees)));
        material.SetShaderParameter("height_scale", Picture.HeightScale);

        AddChild(new MeshInstance3D
        {
            Name = "PixelBackdrop",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200.0f
        });
    }

    /// <summary>
    /// Peça do kit desenhada por cima da sala (fora do layout), alinhada ao tile (gx, gy) como o composer alinha as
    /// peças: mesmo chão, mesma profundidade por pixel (o jogador passa na frente e atrás dela) e pixels
    /// transparentes descartados. Troque o quadro com <see cref="SetOverlayPiece"/>. Usada pela grade do portão
    /// (RunStartGate). A luz das velas não é recalculada: a peça sai com a luz base do kit.
    /// </summary>
    public MeshInstance3D AddOverlay(string name, string piece, int gx, int gy)
    {
        var color = PixelRoomComposer.LoadImage(PixelRoomComposer.KitRoot + piece + ".png");
        if (color == null)
            return null;

        var overlay = CreateOverlayMesh(name, color.GetWidth(), color.GetHeight(), gx * 64.0f, gy * 64.0f,
            new Vector2I(PixelRoomComposer.TileTopX, PixelRoomComposer.TileTopY));
        SetOverlayPiece(overlay, piece, color);
        return overlay;
    }

    // Quadrilátero no chão para uma peça de width x height pixels, com o vértice de cima do tile no pixel tileTop da
    // peça e esse vértice no ponto (wx, wy) do kit, igual ao Blit do composer; a tela vira ponto do chão como no
    // CreateBackdrop. Texturas a cargo de quem chama.
    private MeshInstance3D CreateOverlayMesh(string name, int width, int height, float wx, float wy, Vector2I tileTop)
    {
        Vector3 Ground(float px, float py)
        {
            var sx = wx - wy - tileTop.X + px;
            var sy = (wx + wy) * 0.5f - tileTop.Y + py;
            return KitToWorld(sy + sx * 0.5f, sy - sx * 0.5f, -0.02f);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = new[] { Ground(0, 0), Ground(width, 0), Ground(width, height), Ground(0, height) };
        arrays[(int)Mesh.ArrayType.TexUV] = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        arrays[(int)Mesh.ArrayType.Index] = new[] { 0, 1, 2, 0, 2, 3 };
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        var material = new ShaderMaterial { Shader = new Shader { Code = OverlayShader } };
        material.SetShaderParameter("ray_per_unit", HeightMeters / Mathf.Sin(Mathf.DegToRad(CameraPitchDegrees)));
        material.SetShaderParameter("height_scale", PixelRoomComposer.HeightScale);
        var overlay = new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 50.0f
        };
        AddChild(overlay);
        return overlay;
    }

    /// <summary>Troca a imagem (e a altura) de uma peça criada por <see cref="AddOverlay"/>; a peça nova tem o mesmo tamanho.</summary>
    public static void SetOverlayPiece(MeshInstance3D overlay, string piece, Image color = null)
    {
        if (overlay?.MaterialOverride is not ShaderMaterial material)
            return;
        color ??= PixelRoomComposer.LoadImage(PixelRoomComposer.KitRoot + piece + ".png");
        if (color == null)
            return;
        var height = PixelRoomComposer.LoadImage(PixelRoomComposer.KitRoot + "heights/" + piece + ".png");
        material.SetShaderParameter("color_tex", ImageTexture.CreateFromImage(color));
        if (height != null)
            material.SetShaderParameter("height_tex", ImageTexture.CreateFromImage(height));
    }

    private void CreateCollision()
    {
        var body = new StaticBody3D { Name = "PixelRoomCollision" };
        AddChild(body);
        var bounds = Picture.TileBounds;
        body.AddChild(new CollisionShape3D
        {
            Name = "Floor",
            Position = KitToWorld(bounds.GetCenter().X, bounds.GetCenter().Y, -0.15f),
            Shape = new BoxShape3D { Size = new Vector3(bounds.Size.X * UnitMeters, 0.3f, bounds.Size.Y * UnitMeters) }
        });

        var index = 0;
        foreach (var (a, b, outward) in Picture.Walls)
        {
            float x0 = a.X, y0 = a.Y, x1 = b.X, y1 = b.Y;
            // A face da parede fica na borda do tile; o corpo cresce para fora.
            var shift = new Vector3(outward.X, 0.0f, outward.Y) * (WallThickness * 0.5f);
            body.AddChild(new CollisionShape3D
            {
                Name = $"Wall{index++}",
                Position = KitToWorld((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, WallHeight * 0.5f) + shift,
                Shape = new BoxShape3D
                {
                    Size = new Vector3(Mathf.Abs(x1 - x0) * UnitMeters + WallThickness, WallHeight, Mathf.Abs(y1 - y0) * UnitMeters + WallThickness)
                }
            });
        }

        index = 0;
        foreach (var (center, half) in Picture.Blockers)
        {
            body.AddChild(new CollisionShape3D
            {
                Name = $"Blocker{index++}",
                Position = KitToWorld(center.X, center.Y, 0.8f),
                Shape = new BoxShape3D { Size = new Vector3(half.X * 2.0f * UnitMeters, 1.6f, half.Y * 2.0f * UnitMeters) }
            });
        }
    }

    // Cor sem luz da cena; a profundidade vem da altura de cada pixel: o pixel está no mesmo raio da câmera que o
    // ponto do chão, deslocado em direção a ela.
    private const string BackdropShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform sampler2D color_tex : source_color, filter_nearest;
uniform sampler2D height_tex : filter_nearest;
uniform float ray_per_unit;
uniform float height_scale;

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 h = texture(height_tex, UV);
    float z = (round(h.r * 255.0) * 256.0 + round(h.g * 255.0)) / height_scale;
    vec3 toward_camera = normalize(INV_VIEW_MATRIX[2].xyz);
    vec3 p = world_pos + toward_camera * (z * ray_per_unit);
    vec4 clip = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(p, 1.0));
    DEPTH = clip.z / clip.w;
    ALBEDO = texture(color_tex, UV).rgb;
}
";

    // Igual ao BackdropShader, com os pixels transparentes da peça descartados (e um empurrão opcional para a câmera).
    private const string OverlayShader = @"
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform sampler2D color_tex : source_color, filter_nearest;
uniform sampler2D height_tex : filter_nearest;
uniform float ray_per_unit;
uniform float height_scale;
uniform float depth_bias = 0.0;

varying vec3 world_pos;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec4 c = texture(color_tex, UV);
    if (c.a < 0.5) {
        discard;
    }
    vec4 h = texture(height_tex, UV);
    float z = (round(h.r * 255.0) * 256.0 + round(h.g * 255.0)) / height_scale;
    vec3 toward_camera = normalize(INV_VIEW_MATRIX[2].xyz);
    vec3 p = world_pos + toward_camera * (z * ray_per_unit + depth_bias);
    vec4 clip = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(p, 1.0));
    DEPTH = clip.z / clip.w;
    ALBEDO = c.rgb;
}
";
}
