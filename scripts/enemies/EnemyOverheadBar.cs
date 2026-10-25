using Godot;

// Barra de vida em cima da cabeça do inimigo, em pixel art: uma textura só (emblema do arquétipo + moldura na cor
// da raridade + preenchimento), redesenhada quando a vida muda. Fica num Sprite3D em billboard ancorado no eixo do
// inimigo (x = z = 0), então não sai do lugar quando ele gira para olhar o jogador.
// Substitui o anel colorido no chão (arquétipo) e o losango solto ao lado da barra (raridade).
public partial class EnemyOverheadBar : Node3D
{
    private const int EmblemSize = 7;
    private const int InnerHeight = 3;
    private const int NormalInnerWidth = 34;
    private const int LegendaryInnerWidth = 48;
    private const float TrailCatchUpPerSecond = 1.4f;
    private const float TrailDelaySeconds = 0.28f;

    private static readonly Color Outline = new(0.05f, 0.025f, 0.035f);
    private static readonly Color Background = new(0.13f, 0.05f, 0.06f);
    private static readonly Color Fill = new(0.78f, 0.03f, 0.05f);
    private static readonly Color FillHighlight = new(0.97f, 0.3f, 0.26f);
    private static readonly Color Trail = new(1.0f, 0.86f, 0.68f);
    private static readonly Color NormalFrame = new(0.42f, 0.38f, 0.38f);

    // Glifos 5x5 dos arquétipos ('#' = cor do arquétipo); o contorno escuro é gerado em volta.
    private static readonly string[] MeleeGlyph = { "..#..", "..#..", "..#..", ".###.", "..#.." };
    private static readonly string[] RangedGlyph = { ".###.", "#...#", "#.#.#", "#...#", ".###." };
    private static readonly string[] TankGlyph = { "#####", "#####", "#####", ".###.", "..#.." };

    private Sprite3D _sprite;
    private Image _image;
    private ImageTexture _texture;
    private EnemyArchetype _archetype = EnemyArchetype.Melee;
    private Color _frameColor = NormalFrame;
    private int _innerWidth = NormalInnerWidth;
    private float _ratio = 1.0f;
    private float _trailRatio = 1.0f;
    private float _trailDelay;

    // Modo 2D: cores da paleta AAP-64, as mesmas do corpo e do aviso do golpe.
    public static Color ArchetypeColor(EnemyArchetype archetype) => archetype switch
    {
        EnemyArchetype.Ranged => GameMode.IsPixelArt ? Color.FromHtml("e86a73") : new Color(1.0f, 0.02f, 0.48f),
        EnemyArchetype.Tank => GameMode.IsPixelArt ? Color.FromHtml("f9a31b") : new Color(1.0f, 0.78f, 0.04f),
        _ => GameMode.IsPixelArt ? Color.FromHtml("588dbe") : new Color(0.0f, 0.86f, 0.96f)
    };

    public override void _Ready()
    {
        _sprite = new Sprite3D
        {
            Name = "Sprite",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
            Shaded = false,
            NoDepthTest = true,
            RenderPriority = 3,
            PixelSize = PixelRoom.PixelMeters,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_sprite);
        Redraw();
        SetProcess(false);
    }

    public void Configure(EnemyRarity rarity, Color rarityColor, EnemyArchetype archetype)
    {
        _archetype = archetype;
        _frameColor = rarity == EnemyRarity.Normal ? NormalFrame : rarityColor;
        _innerWidth = rarity == EnemyRarity.Legendary ? LegendaryInnerWidth : NormalInnerWidth;
        _image = null;
        Redraw();
    }

    public void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp(ratio, 0.0f, 1.0f);
        if (ratio < _ratio)
        {
            // Dano: o pedaço perdido fica claro um instante e depois encolhe até a vida atual.
            _trailRatio = Mathf.Max(_trailRatio, _ratio);
            _trailDelay = TrailDelaySeconds;
            SetProcess(IsInsideTree());
        }
        else
        {
            _trailRatio = ratio;
        }
        _ratio = ratio;
        Redraw();
    }

    public override void _Process(double delta)
    {
        if (_trailDelay > 0.0f)
        {
            _trailDelay -= (float)delta;
            return;
        }
        _trailRatio = Mathf.MoveToward(_trailRatio, _ratio, TrailCatchUpPerSecond * (float)delta);
        if (_trailRatio <= _ratio)
            SetProcess(false);
        Redraw();
    }

    private void Redraw()
    {
        if (_sprite == null)
            return;
        var barWidth = _innerWidth + 4;
        var width = EmblemSize - 1 + barWidth;
        var height = EmblemSize;
        if (_image == null || _image.GetWidth() != width)
        {
            _image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            _texture = null;
        }
        _image.Fill(Colors.Transparent);

        // Barra: contorno escuro, moldura na cor da raridade, fundo e preenchimento (com brilho na linha de cima).
        var left = EmblemSize - 1;
        var top = (height - (InnerHeight + 4)) / 2;
        FillRect(left, top, barWidth, InnerHeight + 4, Outline);
        FillRect(left + 1, top + 1, barWidth - 2, InnerHeight + 2, _frameColor);
        FillRect(left + 2, top + 2, _innerWidth, InnerHeight, Background);
        var filled = _ratio > 0.0f ? Mathf.Max(Mathf.CeilToInt(_ratio * _innerWidth), 1) : 0;
        var trail = Mathf.Max(Mathf.CeilToInt(_trailRatio * _innerWidth), filled);
        FillRect(left + 2 + filled, top + 2, trail - filled, InnerHeight, Trail);
        FillRect(left + 2, top + 2, filled, InnerHeight, Fill);
        FillRect(left + 2, top + 2, filled, 1, FillHighlight);
        DrawEmblem(0, 0);

        if (_texture == null)
        {
            _texture = ImageTexture.CreateFromImage(_image);
            _sprite.Texture = _texture;
        }
        else
        {
            _texture.Update(_image);
        }
        // Base da textura na âncora, centrada na barra (o emblema fica pendurado à esquerda).
        _sprite.Offset = new Vector2(-(EmblemSize - 1) * 0.5f, height * 0.5f);
    }

    private void DrawEmblem(int x, int y)
    {
        var glyph = _archetype switch
        {
            EnemyArchetype.Ranged => RangedGlyph,
            EnemyArchetype.Tank => TankGlyph,
            _ => MeleeGlyph
        };
        var color = ArchetypeColor(_archetype);
        FillRect(x, y, EmblemSize, EmblemSize, Outline);
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                if (glyph[row][column] == '#')
                    _image.SetPixel(x + 1 + column, y + 1 + row, color);
            }
        }
    }

    private void FillRect(int x, int y, int width, int height, Color color)
    {
        if (width <= 0 || height <= 0)
            return;
        _image.FillRect(new Rect2I(x, y, width, height), color);
    }
}
