using Godot;
using System;

// Medalhão de recompensa (portas de sala e item no chão; docs/design/portas-recompensas.md, seção 8). Provisório, sem arte nova.
// Estilo: disco escuro com aro dourado brilhante, emblema no centro (~0,7 m) e, ao lado, faixa escura com "Recompensa: <nome>".
// Emblema por tipo (forma e luminância primeiro, cor só reforça):
//   Bênção de Sangue: ícone da família sorteada (ou losango dourado claro, se faltar arte);
//   Fruta do Diabo: esfera vermelho-escuro com haste (emissiva, para não sumir no disco escuro);
//   Moedas de Sangue: pilha de três discos bronze;
//   Bigorna: silhueta de bigorna em aço claro (base larga, cintura, tampo e chifre).
// Tudo fica de frente para a câmera (billboard), inclusive a faixa, para o rótulo ler igual de qualquer ponto da sala.
// Quem chama: ScenePortal.AddRewardIcon (porta, medalhão a 3,5 m do piso, acima do lintel) e RewardPickup (item no chão).
public static class DoorRewardIcon
{
    // Medalhão: lado do quadro de 1,2 m. O aro fica a 90% do raio (~1,08 m de diâmetro); o resto é o brilho externo.
    public const float MedallionSize = 1.2f;
    private const int MedallionTextureSize = 128;
    // Raio visível do disco: a faixa do rótulo começa logo depois dele.
    private const float VisibleRadius = MedallionSize * 0.5f * 0.9f;

    // Emblema de Bênção (ícone da família ou losango): lado de 0,7 m, dentro do fundo escuro do disco.
    private const float EmblemSize = 0.70f;
    private const int DiamondTextureSize = 64;
    // Emblemas em geometria (Fruta e Moedas): o desenho original tinha ~0,36 m; a escala leva para ~0,7 m.
    private const float GeometryEmblemScale = 1.7f;
    private const float GeometryEmblemOffsetY = -0.06f;
    // Emblemas e faixa ficam um pouco à frente do disco (+Z local, lado da sala), para não brigar no mesmo plano.
    private const float EmblemDepth = 0.03f;
    private const float BandDepth = 0.01f;
    private const float LabelDepth = 0.02f;

    // Faixa do rótulo: altura fixa, largura pelo texto (+ margem em cada lado).
    private const float BandGap = 0.05f;
    private const float BandPadding = 0.12f;
    private const float BandHeight = 0.26f;

    private const float LabelPixelSize = 0.0040f;
    private const int LabelFontSize = 36;
    private const int LabelOutline = 10;

    private const string BlessingHex = "E2B94A";
    private const string FruitHex = "8E0F1A";
    private const string StemHex = "1F0D0A";
    private const string AnvilHex = "8795A8";
    private const string AnvilTopHex = "C4CEDA";
    private const string CoinHex = "B9733B";
    private const string CoinRimHex = "D9A066";
    private const string FillHex = "2A1B12";
    private const string RingDarkHex = "B8862E";
    private const string RingHex = "F3D27A";
    private const string GlowHex = "F0C860";
    private const string BandHex = "120A07";

    // Texturas geradas por código, uma vez (medalhão e losango de fallback).
    private static ImageTexture _medallionTexture;
    private static ImageTexture _diamondTexture;

    /// <summary>
    /// Medalhão da porta (ScenePortal.AddRewardIcon). <paramref name="labelSide"/>: -1 põe o rótulo à esquerda, 1 à direita.
    /// Com duas portas, cada uma leva o rótulo para o lado de fora do conjunto, para as faixas não se encontrarem.
    /// </summary>
    public static void PopulateDoor(Node3D icon, RoomRewardDoor door, int labelSide = 1) => Build(icon, door, labelSide);

    /// <summary>Medalhão do item no chão (RewardPickup): o mesmo desenho da porta, com o rótulo à direita.</summary>
    public static void PopulatePickup(Node3D icon, RoomRewardDoor door) => Build(icon, door, 1);

    /// <summary>Rótulo curto localizado do tipo (reward.door.*.short). Bênção mostra a família; Moedas, o valor.</summary>
    public static string ShortLabel(RoomRewardDoor door)
    {
        return door.Type switch
        {
            RoomRewardType.Blessing => BlessingLabel(door),
            RoomRewardType.DevilsFruit => Localization.Text("reward.door.fruit.short"),
            RoomRewardType.Anvil => Localization.Text("reward.door.anvil.short"),
            _ => Localization.TextFormat("reward.door.coins.short", door.CoinAmount),
        };
    }

    private static void Build(Node3D icon, RoomRewardDoor door, int labelSide)
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
                AddDevilsFruit(GeometryShape(emblem));
                break;
            case RoomRewardType.Anvil:
                AddAnvil(GeometryShape(emblem));
                break;
            default:
                AddCoins(GeometryShape(emblem));
                break;
        }

        AddLabel(icon, door, labelSide);
    }

    // Subnó com a escala e o deslocamento dos emblemas em geometria (Fruta e Moedas), para caber no disco.
    private static Node3D GeometryShape(Node3D emblem)
    {
        var shape = new Node3D
        {
            Name = "Shape",
            Scale = Vector3.One * GeometryEmblemScale,
            Position = Vector3.Up * GeometryEmblemOffsetY
        };
        emblem.AddChild(shape);
        return shape;
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

    // Esfera com haste curta no topo. Material brilhante (roughness baixo) e emissivo: sem isso some no disco escuro.
    private static void AddDevilsFruit(Node3D parent)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = "Fruit",
            Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 24, Rings = 12 },
            MaterialOverride = FruitMaterial()
        });
        parent.AddChild(new MeshInstance3D
        {
            Name = "Stem",
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.035f, Height = 0.12f, RadialSegments = 8 },
            MaterialOverride = Material(StemHex, 0.0f, 0.8f),
            Position = new Vector3(0.0f, 0.22f, 0.0f)
        });
    }

    // Bigorna: silhueta em T assimétrica (base larga, cintura estreita, tampo largo e chifre cônico à direita). Aço claro com
    // brilho leve, para ler no disco escuro; a forma (não a cor) a distingue da esfera, da pilha de discos e do losango.
    private static void AddAnvil(Node3D parent)
    {
        var steel = AnvilMaterial(AnvilHex, 0.25f);
        var top = AnvilMaterial(AnvilTopHex, 0.35f);
        AddBox(parent, "AnvilBase", new Vector3(0.34f, 0.07f, 0.16f), new Vector3(0.0f, -0.16f, 0.0f), steel);
        AddBox(parent, "AnvilWaist", new Vector3(0.16f, 0.14f, 0.12f), new Vector3(0.0f, -0.055f, 0.0f), steel);
        AddBox(parent, "AnvilTop", new Vector3(0.40f, 0.09f, 0.16f), new Vector3(0.0f, 0.055f, 0.0f), top);
        parent.AddChild(new MeshInstance3D
        {
            Name = "AnvilHorn",
            Mesh = new CylinderMesh { TopRadius = 0.01f, BottomRadius = 0.075f, Height = 0.20f, RadialSegments = 12 },
            MaterialOverride = top,
            Position = new Vector3(0.30f, 0.055f, 0.0f),
            RotationDegrees = new Vector3(0.0f, 0.0f, -90.0f)
        });
    }

    private static void AddBox(Node3D parent, string name, Vector3 size, Vector3 position, Material material)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = material,
            Position = position
        });
    }

    private static StandardMaterial3D AnvilMaterial(string hex, float emission)
    {
        var color = Color.FromHtml("#" + hex);
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.4f,
            Metallic = 0.5f,
            EmissionEnabled = true,
            Emission = color,
            EmissionEnergyMultiplier = emission
        };
    }

    // Três discos achatados empilhados. Cada disco é um cilindro bronze com um anel externo mais claro por cima.
    private static void AddCoins(Node3D parent)
    {
        for (var i = 0; i < 3; i++)
        {
            var y = (i - 1) * 0.075f;
            parent.AddChild(new MeshInstance3D
            {
                Name = $"Coin_{i}",
                Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.07f, RadialSegments = 24 },
                MaterialOverride = Material(CoinRimHex, 0.0f, 0.4f),
                Position = new Vector3(0.0f, y, 0.0f)
            });
            parent.AddChild(new MeshInstance3D
            {
                Name = $"CoinFace_{i}",
                Mesh = new CylinderMesh { TopRadius = 0.165f, BottomRadius = 0.165f, Height = 0.075f, RadialSegments = 24 },
                MaterialOverride = Material(CoinHex, 0.0f, 0.5f),
                Position = new Vector3(0.0f, y, 0.0f)
            });
        }
    }

    // Faixa escura ao lado do disco + rótulo "Recompensa: <nome>". A largura acompanha o texto e muda ao trocar de idioma.
    private static void AddLabel(Node3D icon, RoomRewardDoor door, int labelSide)
    {
        var side = labelSide < 0 ? -1.0f : 1.0f;
        var bandMesh = new QuadMesh();
        var band = new MeshInstance3D { Name = "RewardBand", Mesh = bandMesh, MaterialOverride = BandMaterial() };
        var label = new Label3D
        {
            Name = "RewardLabel",
            PixelSize = LabelPixelSize,
            FontSize = LabelFontSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = UiPalette.TextPrimary,
            OutlineSize = LabelOutline,
            OutlineModulate = UiPalette.TextOutline,
            // Legível por cima do lintel e da moldura, como os demais rótulos 3D.
            NoDepthTest = true,
            RenderPriority = 2,
            OutlineRenderPriority = 1
        };
        icon.AddChild(band);
        icon.AddChild(label);

        void Layout(string text)
        {
            label.Text = text;
            var width = TextWidth(text) + 2.0f * BandPadding;
            bandMesh.Size = new Vector2(width, BandHeight);
            var x = side * (VisibleRadius + BandGap + width * 0.5f);
            band.Position = new Vector3(x, 0.0f, BandDepth);
            label.Position = new Vector3(x, 0.0f, LabelDepth);
        }

        Layout(BadgeText(door));

        // O texto segue o idioma enquanto a porta existe; a assinatura sai quando o rótulo sai da árvore.
        var localization = Localization.Instance;
        if (localization == null)
            return;

        Localization.LanguageChangedEventHandler onLanguageChanged = _ =>
        {
            if (GodotObject.IsInstanceValid(label))
                Layout(BadgeText(door));
        };
        localization.LanguageChanged += onLanguageChanged;
        label.TreeExiting += () =>
        {
            if (Localization.Instance != null)
                Localization.Instance.LanguageChanged -= onLanguageChanged;
        };
    }

    private static string BadgeText(RoomRewardDoor door) => Localization.TextFormat("reward.door.badge", ShortLabel(door));

    // Largura do texto em metros, medida na fonte de fallback (a que o Label3D usa sem fonte própria).
    // Sem fonte, estimativa por caractere. O contorno soma uma margem.
    private static float TextWidth(string text)
    {
        var font = ThemeDB.FallbackFont;
        var pixels = font != null
            ? font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFontSize).X
            : text.Length * LabelFontSize * 0.55f;
        return (pixels + LabelOutline) * LabelPixelSize;
    }

    private static StandardMaterial3D BandMaterial()
    {
        return new StandardMaterial3D
        {
            AlbedoColor = new Color(Color.FromHtml("#" + BandHex), 0.84f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true
        };
    }

    private static ImageTexture MedallionTexture() => _medallionTexture ??= Generate(MedallionTextureSize, MedallionPixel);

    private static ImageTexture DiamondTexture() => _diamondTexture ??= Generate(DiamondTextureSize, DiamondPixel);

    // dx, dy em [-1, 1] (borda do quadro = 1). Até 0,8: fundo escuro. De 0,8 a 0,9: aro dourado (mais claro no meio).
    // De 0,9 a 1: brilho externo que se apaga. Fora disso: transparente.
    private static Color MedallionPixel(float dx, float dy)
    {
        var r = Mathf.Sqrt(dx * dx + dy * dy);
        if (r <= 0.80f)
            return new Color(Color.FromHtml("#" + FillHex), 0.94f);
        if (r <= 0.90f)
        {
            var t = Mathf.Sin((r - 0.80f) / 0.10f * Mathf.Pi);
            return Color.FromHtml("#" + RingDarkHex).Lerp(Color.FromHtml("#" + RingHex), t);
        }
        if (r <= 1.0f)
        {
            var fade = 1.0f - (r - 0.90f) / 0.10f;
            return new Color(Color.FromHtml("#" + GlowHex), 0.5f * fade * fade);
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

    private static StandardMaterial3D FruitMaterial()
    {
        var color = Color.FromHtml("#" + FruitHex);
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.25f,
            Metallic = 0.15f,
            EmissionEnabled = true,
            Emission = new Color(0.85f, 0.04f, 0.08f),
            EmissionEnergyMultiplier = 0.5f
        };
    }

    // Material fosco simples. emission > 0 só é usado na Fruta.
    private static StandardMaterial3D Material(string hex, float emission, float roughness = 0.45f)
    {
        var color = Color.FromHtml("#" + hex);
        var material = new StandardMaterial3D { AlbedoColor = color, Roughness = roughness };
        if (emission > 0.0f)
        {
            material.EmissionEnabled = true;
            material.Emission = color;
            material.EmissionEnergyMultiplier = emission;
        }
        return material;
    }
}
