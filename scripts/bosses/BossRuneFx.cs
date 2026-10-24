using Godot;

/// <summary>
/// Aviso de golpe em pixel art desenhado por shader (sem textura): círculo de runas, anel de onda, vórtice ou faixa de carga,
/// deitado no chão e alinhado à câmera para que cada "pixel" do desenho fique quadrado na tela, no mesmo tamanho de pixel
/// dos sprites (PlayerSpriteVisual.PixelSize). Usado pelos golpes da Guardiã Invisível (BossController.InvisibleWoman.cs).
/// Ciclo: o preenchimento pontilhado adensa até o impacto (Charge), o impacto solta uma onda e dissolve o desenho (Release).
/// </summary>
public partial class BossRuneFx : Node3D
{
    public enum Shape { Runes = 0, Ring = 1, Vortex = 2, Line = 3 }

    // Paleta AAP-64 (a mesma do kit da cripta): rampa violeta da Guardiã e rampa azul-clara do teleporte.
    public static readonly Color[] Violet = { Hex("242234"), Hex("793a80"), Hex("bc4a9b"), Hex("e3e6ff") };
    public static readonly Color[] Pale = { Hex("242234"), Hex("588dbe"), Hex("849be4"), Hex("e3e6ff") };

    private const float GroundHeight = 0.07f;
    private static Shader _shader;

    private ShaderMaterial _material;
    private Tween _tween;
    private float _extent;

    private static Color Hex(string hex) => Color.FromHtml(hex);

    /// <summary>Círculo de runas (Runes), vórtice (Vortex) ou anel entre inner e outer (Ring), com o centro no chão.</summary>
    public static BossRuneFx Circle(Node parent, Vector3 center, float outer, float inner, Shape shape, Color[] palette, float floorY)
    {
        var fx = Create(parent, new Vector3(center.X, floorY + GroundHeight, center.Z), outer + 0.2f, palette);
        fx._material.SetShaderParameter("shape", (int)shape);
        fx._material.SetShaderParameter("outer_r", outer);
        fx._material.SetShaderParameter("inner_r", shape == Shape.Ring ? Mathf.Max(inner, 0.0f) : 0.0f);
        return fx;
    }

    /// <summary>Faixa de carga de origin até origin + direction * length, com meia-largura halfWidth.</summary>
    public static BossRuneFx Line(Node parent, Vector3 origin, Vector3 direction, float length, float halfWidth, Color[] palette, float floorY)
    {
        direction.Y = 0.0f;
        direction = direction.LengthSquared() > 0.0001f ? direction.Normalized() : Vector3.Forward;
        var middle = origin + direction * (length * 0.5f);
        var fx = Create(parent, new Vector3(middle.X, floorY + GroundHeight, middle.Z), length * 0.5f + halfWidth + 0.2f, palette);
        // Segmento em coordenadas locais do plano (o plano gira com a câmera, o segmento não).
        var localA = fx.ToPlane(origin);
        var localB = fx.ToPlane(origin + direction * length);
        fx._material.SetShaderParameter("shape", (int)Shape.Line);
        fx._material.SetShaderParameter("seg_a", localA);
        fx._material.SetShaderParameter("seg_b", localB);
        fx._material.SetShaderParameter("outer_r", halfWidth);
        return fx;
    }

    private static BossRuneFx Create(Node parent, Vector3 position, float extent, Color[] palette)
    {
        var fx = new BossRuneFx { Name = "BossRuneFx", _extent = extent };
        parent.AddChild(fx);
        var camera = fx.GetViewport()?.GetCamera3D();
        var yaw = camera?.GlobalRotation.Y ?? Mathf.DegToRad(IsoCamera.DefaultYawDegrees);
        var pitch = camera != null ? -camera.GlobalRotation.X : Mathf.DegToRad(PixelRoom.CameraPitchDegrees);
        fx.GlobalPosition = position;
        fx.GlobalRotation = new Vector3(0.0f, yaw, 0.0f);

        fx._material = new ShaderMaterial { Shader = LoadShader(), RenderPriority = 8 };
        fx._material.SetShaderParameter("extent", extent);
        fx._material.SetShaderParameter("pixel_m", PlayerSpriteVisual.PixelSize);
        // Um pixel de tela no eixo de profundidade cobre 1/sen(inclinação) pixels no chão.
        fx._material.SetShaderParameter("depth_scale", 1.0f / Mathf.Max(Mathf.Sin(Mathf.Clamp(pitch, 0.2f, Mathf.Pi / 2.0f)), 0.2f));
        fx._material.SetShaderParameter("c_dark", palette[0]);
        fx._material.SetShaderParameter("c_mid", palette[1]);
        fx._material.SetShaderParameter("c_bright", palette[2]);
        fx._material.SetShaderParameter("c_hot", palette[3]);
        fx._material.SetShaderParameter("seed", (float)GD.RandRange(0.0, 100.0));
        fx.AddChild(new MeshInstance3D
        {
            Name = "Plane",
            Mesh = new PlaneMesh { Size = new Vector2(extent * 2.0f, extent * 2.0f) },
            MaterialOverride = fx._material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        return fx;
    }

    private Vector2 ToPlane(Vector3 world)
    {
        var local = ToLocal(new Vector3(world.X, GlobalPosition.Y, world.Z));
        return new Vector2(local.X, local.Z);
    }

    /// <summary>O preenchimento adensa de 0 a 1 em seconds (o golpe cai no fim).</summary>
    public BossRuneFx Charge(float seconds)
    {
        _tween?.Kill();
        _material.SetShaderParameter("progress", 0.0f);
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From<float>(value => _material.SetShaderParameter("progress", value)), 0.0f, 1.0f, Mathf.Max(seconds, 0.05f));
        return this;
    }

    /// <summary>Impacto: onda que corre do centro à borda e dissolve o desenho. Sem impacto: só apaga.</summary>
    public void Release(bool impact, float seconds = 0.32f)
    {
        _tween?.Kill();
        if (!IsInsideTree())
        {
            QueueFree();
            return;
        }
        _material.SetShaderParameter("progress", 1.0f);
        _tween = CreateTween();
        if (impact)
            _tween.TweenMethod(Callable.From<float>(value => _material.SetShaderParameter("impact", value)), 0.0f, 1.0f, seconds);
        else
            _tween.TweenMethod(Callable.From<float>(value => _material.SetShaderParameter("fade", value)), 0.0f, 1.0f, 0.15f);
        _tween.TweenCallback(Callable.From(QueueFree));
    }

    /// <summary>Aviso curto que acende e já estoura (início de golpe, troca de fase, chegada do teleporte).</summary>
    public static void Flare(Node parent, Vector3 center, float radius, Color[] palette, float floorY, float seconds = 0.35f)
    {
        var fx = Circle(parent, center, radius, 0.0f, Shape.Runes, palette, floorY);
        fx._material.SetShaderParameter("progress", 0.55f);
        fx.Release(true, seconds);
    }

    private static Shader LoadShader() => _shader ??= new Shader { Code = ShaderCode };

    // Coordenadas do plano em metros (x para a direita da câmera, y em profundidade). Tudo é quantizado numa grade de
    // pixel_m x (pixel_m * depth_scale), então o desenho sai em pixels quadrados na tela, sem textura.
    private const string ShaderCode = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, shadows_disabled;

uniform int shape = 0;
uniform float extent = 1.0;
uniform float pixel_m = 0.033;
uniform float depth_scale = 2.0;
uniform float outer_r = 1.0;
uniform float inner_r = 0.0;
uniform vec2 seg_a = vec2(0.0);
uniform vec2 seg_b = vec2(0.0, -1.0);
uniform float progress = 0.0;
uniform float impact = 0.0;
uniform float fade = 0.0;
uniform float seed = 0.0;
uniform vec4 c_dark : source_color;
uniform vec4 c_mid : source_color;
uniform vec4 c_bright : source_color;
uniform vec4 c_hot : source_color;

const int BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

float bayer4(ivec2 p) {
    int x = p.x & 3;
    int y = p.y & 3;
    return (float(BAYER[y * 4 + x]) + 0.5) / 16.0;
}

float hash(float n) { return fract(sin(n * 12.9898 + seed * 78.233) * 43758.5453); }

void fragment() {
    vec2 cell = vec2(pixel_m, pixel_m * depth_scale);
    vec2 raw = (UV - 0.5) * 2.0 * extent;
    ivec2 ip = ivec2(floor(raw / cell));
    vec2 q = (vec2(ip) + 0.5) * cell;
    float px = pixel_m;
    float dither = bayer4(ip);

    // d = distância ao centro (ou ao segmento); band = 0 na borda interna/eixo e 1 na borda externa.
    float d;
    float band;
    float along = 0.0;
    float edge_in = -1.0;
    if (shape == 3) {
        vec2 ab = seg_b - seg_a;
        float t = clamp(dot(q - seg_a, ab) / max(dot(ab, ab), 0.0001), 0.0, 1.0);
        d = length(q - (seg_a + ab * t));
        along = t;
        band = d / outer_r;
    } else {
        d = length(q);
        band = (d - inner_r) / max(outer_r - inner_r, 0.001);
        edge_in = inner_r;
    }
    if (d > outer_r + px * 0.5) discard;
    if (shape == 1 && d < inner_r - px * 0.5) discard;

    float t_time = TIME;
    vec4 col = vec4(c_dark.rgb, 0.45);
    bool lit = false;

    // Preenchimento pontilhado: cresce (da origem da carga, do centro ou da borda interna) e adensa até o impacto.
    float reach = shape == 3 ? along : clamp(band, 0.0, 1.0);
    if (shape == 2) reach = 1.0 - clamp(band, 0.0, 1.0);
    if (reach <= progress * 1.05) {
        float density = 0.18 + 0.55 * progress;
        if (dither < density) { col = vec4(c_mid.rgb, 0.85); lit = true; }
    }

    // Desenho próprio de cada forma.
    float ang = atan(q.y, q.x);
    if (shape == 0) {
        // Faixa de runas junto à borda, girando devagar.
        float runeOuter = outer_r - px * 1.5;
        float runeInner = outer_r - px * 5.5;
        if (d < runeOuter && d > runeInner) {
            float n = max(floor(outer_r * 9.0), 8.0);
            float a = (ang + t_time * 0.35) / 6.2831853 * n;
            float slot = floor(a);
            float local = fract(a);
            float row = floor((d - runeInner) / px);
            float g = hash(slot * 7.0 + row);
            if (local > 0.18 && local < 0.82 && g > 0.42) { col = vec4(c_bright.rgb, 0.95); lit = true; }
        }
        // Anel interno fino e seis raios pontilhados girando ao contrário.
        if (abs(d - outer_r * 0.62) < px * 0.6) { col = vec4(c_bright.rgb, 0.8); lit = true; }
        float spoke = fract((ang - t_time * 0.2) / 6.2831853 * 6.0);
        float spokeDistance = min(spoke, 1.0 - spoke) * 6.2831853 / 6.0 * d;
        if (d < outer_r * 0.6 && d > px * 3.0 && spokeDistance < px * 0.7 && dither < 0.75) { col = vec4(c_bright.rgb, 0.7); lit = true; }
    } else if (shape == 1) {
        // Onda: hachuras diagonais correndo para fora.
        float stripe = fract((d - t_time * 2.5) / (px * 6.0));
        if (stripe < 0.25 && dither < 0.6) { col = vec4(c_bright.rgb, 0.75); lit = true; }
    } else if (shape == 2) {
        // Vórtice: braços em espiral girando e anéis caindo para o centro.
        float spiral = fract((ang + d * 2.2 - t_time * 2.6) / 6.2831853 * 3.0);
        if (spiral < 0.2) { col = vec4(c_bright.rgb, 0.85); lit = true; }
        float rings = fract(d / (px * 7.0) + t_time * 1.4);
        if (rings < 0.16 && dither < 0.7) { col = vec4(c_hot.rgb, 0.7); lit = true; }
        if (d < px * 2.5) { col = vec4(0.02, 0.0, 0.03, 1.0); lit = true; }
    } else {
        // Carga: setas apontando para o fim da faixa.
        float chevron = fract(along * max(length(seg_b - seg_a), 1.0) / 0.9 + band * 0.5 - t_time * 1.8);
        if (chevron < 0.18 && band < 0.85) { col = vec4(c_bright.rgb, 0.9); lit = true; }
    }

    // Contorno de 1 pixel (pisca rápido perto do impacto).
    float blink = progress > 0.8 ? step(0.5, fract(t_time * 12.0)) : 1.0;
    bool outline = abs(d - outer_r) < px * 0.75 || (shape == 1 && abs(d - edge_in) < px * 0.75);
    if (outline) { col = mix(vec4(c_bright.rgb, 1.0), vec4(c_hot.rgb, 1.0), blink * progress); lit = true; }

    // Impacto: frente de onda clara do centro à borda; atrás dela o desenho se desfaz em pontilhado.
    if (impact > 0.0) {
        float front = impact * 1.15;
        float r01 = shape == 3 ? along : clamp(d / outer_r, 0.0, 1.0);
        if (abs(r01 - front) < 0.12) { col = vec4(c_hot.rgb, 1.0); lit = true; }
        else if (r01 < front) {
            if (dither < impact) discard;
            col = vec4(mix(c_bright.rgb, c_hot.rgb, 0.5), 0.9);
        } else {
            col.rgb = mix(col.rgb, c_hot.rgb, 0.35);
        }
    }
    if (fade > 0.0 && dither < fade) discard;
    if (!lit && col.a < 0.5 && dither > 0.5) col.a *= 0.6;

    ALBEDO = col.rgb;
    ALPHA = col.a;
}
";
}
