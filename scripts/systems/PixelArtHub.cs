using Godot;
using System.Collections.Generic;
using System.Text.Json;

// TESTE: hub em pixel art pré-renderizado (kit da cripta, tools/pixel_art/crypt_kit/build_room.py --godot).
// A imagem do hub fica num quadrilátero no chão, visto por uma câmera ortográfica 2:1 (yaw 45, pitch 30);
// um mapa de altura por pixel deixa o shader escrever a profundidade real de cada pixel, então o jogador
// e qualquer objeto 3D passam atrás das paredes e dos caixões. Paredes e obstáculos viram colisões invisíveis.
// Mundo do kit: 1 tile = 64 unidades; o centro do hub (onde fica a fonte) vira a origem da cena.
public partial class PixelArtHub : Node3D
{
    public const string DefaultRoomPath = "res://assets/pixel_art/crypt/rooms/hub_completo.json";

    // 1 pixel da imagem = PlayerSpriteVisual.PixelSize (0,033 m) no plano da tela, o mesmo pixel do sprite do Virgil.
    public const float PixelMeters = 0.033f;
    public static readonly float UnitMeters = PixelMeters * Mathf.Sqrt2;
    // Altura real de 1 unidade de z do kit (a projeção 2:1 do kit achata a altura em sqrt(2/3)).
    public static readonly float HeightMeters = UnitMeters * Mathf.Sqrt(2.0f / 3.0f);
    public const float CameraPitchDegrees = 30.0f;
    public const float CameraYawDegrees = 45.0f;
    public const float CameraDistance = 80.0f;
    private const float WallHeight = 3.2f;
    private const float WallThickness = 0.35f;

    [Export] public string RoomPath { get; set; } = DefaultRoomPath;
    // Pixels da tela por pixel da arte; 0 = automático (2 até 1080p, 3 acima).
    [Export] public int PixelScale { get; set; }

    public Vector2 CenterUnits { get; set; } = new(992.0f, 992.0f);

    private Camera3D _camera;

    // Ponto do mundo do kit (unidades; 64 = 1 tile) para a posição 3D na cena.
    public Vector3 KitToWorld(float wx, float wy, float height = 0.0f)
    {
        return new Vector3((wx - CenterUnits.X) * UnitMeters, height, (wy - CenterUnits.Y) * UnitMeters);
    }

    public Vector3 TileToWorld(float gx, float gy, float height = 0.0f) => KitToWorld(gx * 64.0f, gy * 64.0f, height);

    public bool Build()
    {
        var meta = LoadMeta();
        if (meta == null)
            return false;

        var root = meta.RootElement;
        var folder = RoomPath.GetBaseDir() + "/";
        var color = LoadImage(folder + root.GetProperty("image").GetString());
        var height = LoadImage(folder + root.GetProperty("height_image").GetString());
        if (color == null || height == null)
            return false;

        var size = root.GetProperty("size");
        var origin = root.GetProperty("origin");
        var width = size[0].GetSingle();
        var heightPx = size[1].GetSingle();
        var ox = origin[0].GetSingle();
        var oy = origin[1].GetSingle();

        CreateBackdrop(color, height, width, heightPx, ox, oy, root.GetProperty("height_scale").GetSingle());
        CreateCollision(root);
        meta.Dispose();
        return true;
    }

    // Câmera ortográfica travada no ângulo do kit; chamar antes do _Ready da IsoCamera.
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

    public override void _Process(double delta)
    {
        UpdateCameraSize();
    }

    private void UpdateCameraSize()
    {
        if (_camera == null)
            return;

        // A janela renderiza o 3D na resolução real; o tamanho ortográfico mantém um múltiplo inteiro de pixels.
        var windowHeight = Mathf.Max(DisplayServer.WindowGetSize().Y, 1);
        var scale = PixelScale > 0 ? PixelScale : (windowHeight > 1100 ? 3 : 2);
        _camera.Size = windowHeight / (float)scale * PixelMeters;
    }

    private JsonDocument LoadMeta()
    {
        if (!FileAccess.FileExists(RoomPath))
        {
            GD.PushError($"PixelArtHub: sala nao encontrada em {RoomPath}");
            return null;
        }

        try
        {
            return JsonDocument.Parse(FileAccess.GetFileAsString(RoomPath));
        }
        catch (JsonException exception)
        {
            GD.PushError($"PixelArtHub: JSON invalido em {RoomPath}: {exception.Message}");
            return null;
        }
    }

    // Lido do PNG direto (sem importação): o mapa de altura não pode passar por compressão.
    private static Image LoadImage(string path)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image == null || image.IsEmpty())
        {
            GD.PushError($"PixelArtHub: imagem nao carregada: {path}");
            return null;
        }

        return image;
    }

    private void CreateBackdrop(Image color, Image height, float width, float heightPx, float ox, float oy, float heightScale)
    {
        // Canto da imagem -> ponto do chão (z = 0) que cai naquele pixel.
        Vector3 Ground(float px, float py)
        {
            var sx = px - ox;
            var sy = py - oy;
            return KitToWorld(sy + sx * 0.5f, sy - sx * 0.5f, -0.02f);
        }

        var vertices = new[] { Ground(0, 0), Ground(width, 0), Ground(width, heightPx), Ground(0, heightPx) };
        var uvs = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        var indices = new[] { 0, 1, 2, 0, 2, 3 };
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        var material = new ShaderMaterial { Shader = new Shader { Code = BackdropShader } };
        material.SetShaderParameter("color_tex", ImageTexture.CreateFromImage(color));
        material.SetShaderParameter("height_tex", ImageTexture.CreateFromImage(height));
        // Altura guardada (x height_scale) -> distância ao longo do raio da câmera: altura real / sen(30°).
        material.SetShaderParameter("ray_per_unit", HeightMeters / Mathf.Sin(Mathf.DegToRad(CameraPitchDegrees)));
        material.SetShaderParameter("height_scale", heightScale);

        AddChild(new MeshInstance3D
        {
            Name = "PixelBackdrop",
            Mesh = mesh,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 200.0f
        });
    }

    private void CreateCollision(JsonElement root)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var tile in root.GetProperty("tiles").EnumerateArray())
        {
            var gx = tile[0].GetSingle() * 64.0f;
            var gy = tile[1].GetSingle() * 64.0f;
            minX = Mathf.Min(minX, gx);
            minY = Mathf.Min(minY, gy);
            maxX = Mathf.Max(maxX, gx + 64.0f);
            maxY = Mathf.Max(maxY, gy + 64.0f);
        }

        var body = new StaticBody3D { Name = "PixelHubCollision" };
        AddChild(body);
        var floorCenter = KitToWorld((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, -0.15f);
        body.AddChild(new CollisionShape3D
        {
            Name = "Floor",
            Position = floorCenter,
            Shape = new BoxShape3D { Size = new Vector3((maxX - minX) * UnitMeters, 0.3f, (maxY - minY) * UnitMeters) }
        });

        var index = 0;
        foreach (var wall in root.GetProperty("walls").EnumerateArray())
        {
            var x0 = wall[0].GetSingle();
            var y0 = wall[1].GetSingle();
            var x1 = wall[2].GetSingle();
            var y1 = wall[3].GetSingle();
            var size = new Vector3(
                Mathf.Abs(x1 - x0) * UnitMeters + WallThickness,
                WallHeight,
                Mathf.Abs(y1 - y0) * UnitMeters + WallThickness);
            body.AddChild(new CollisionShape3D
            {
                Name = $"Wall{index++}",
                Position = KitToWorld((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, WallHeight * 0.5f),
                Shape = new BoxShape3D { Size = size }
            });
        }

        index = 0;
        foreach (var blocker in root.GetProperty("blockers").EnumerateArray())
        {
            var size = new Vector3(blocker.GetProperty("hx").GetSingle() * 2.0f * UnitMeters, 1.6f, blocker.GetProperty("hy").GetSingle() * 2.0f * UnitMeters);
            body.AddChild(new CollisionShape3D
            {
                Name = $"Blocker{index++}",
                Position = KitToWorld(blocker.GetProperty("x").GetSingle(), blocker.GetProperty("y").GetSingle(), 0.8f),
                Shape = new BoxShape3D { Size = size }
            });
        }
    }

    // Cor sem luz da cena (a luz das velas já está na arte); a profundidade vem da altura de cada pixel:
    // o pixel está no mesmo raio da câmera que o ponto do chão, deslocado em direção a ela.
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
}
