using Godot;

// Ícone provisório das portas de recompensa, em geometria (docs/design/portas-recompensas.md, seção 8).
// Cada tipo se diferencia pela forma primeiro, depois pela luminância; a cor só reforça:
//   Bênção de Sangue: losango dourado claro (luminância alta), com aro escuro atrás para contrastar com o piso;
//   Fruta do Diabo: esfera vermelho-escuro com haste (luminância baixa); é a única com emissão, para não sumir no piso escuro;
//   Moedas de Sangue: pilha de três discos bronze com aro mais claro (luminância média).
// Quem chama (ScenePortal.AddRewardIcon) cria o nó "RewardIcon" a ~3,1 m e entrega o nó para cá.
// O rótulo curto (reward.door.*.short) fica acima do ícone; o prompt de aproximação (ScenePortal) continua a 3,9 m.
public static class DoorRewardIcon
{
    // Rótulo a 0,42 m acima do ponto de encaixe (~3,5 m no modelo da porta), em tamanho de mundo (não fixo na tela).
    private const float LabelHeight = 0.42f;
    private const float LabelPixelSize = 0.0040f;
    private const int LabelFontSize = 40;
    private const int LabelOutline = 10;

    private const string BlessingHex = "E2B94A";
    private const string BlessingRimHex = "2A1A10";
    private const string FruitHex = "8E0F1A";
    private const string StemHex = "1F0D0A";
    private const string CoinHex = "B9733B";
    private const string CoinRimHex = "D9A066";

    /// <summary>Monta a geometria do tipo da porta dentro de <paramref name="icon"/> e o rótulo curto acima dela.</summary>
    public static void Populate(Node3D icon, RoomRewardDoor door)
    {
        if (icon == null || door == null)
            return;

        switch (door.Type)
        {
            case RoomRewardType.Blessing:
                AddBlessing(icon);
                break;
            case RoomRewardType.DevilsFruit:
                AddDevilsFruit(icon);
                break;
            default:
                AddCoins(icon);
                break;
        }

        AddLabel(icon, door);
    }

    /// <summary>Rótulo curto localizado do tipo (reward.door.*.short). Moedas mostra o valor da porta.</summary>
    public static string ShortLabel(RoomRewardDoor door)
    {
        return door.Type switch
        {
            RoomRewardType.Blessing => Localization.Text("reward.door.blessing.short"),
            RoomRewardType.DevilsFruit => Localization.Text("reward.door.fruit.short"),
            _ => Localization.TextFormat("reward.door.coins.short", door.CoinAmount),
        };
    }

    // Losango: caixa plana girada 45 graus no plano da porta. O aro escuro é uma caixa um pouco maior atrás.
    private static void AddBlessing(Node3D icon)
    {
        icon.AddChild(new MeshInstance3D
        {
            Name = "Rim",
            Mesh = new BoxMesh { Size = new Vector3(0.36f, 0.36f, 0.04f) },
            MaterialOverride = Material(BlessingRimHex, 0.0f, 0.8f),
            RotationDegrees = new Vector3(0.0f, 0.0f, 45.0f),
            Position = new Vector3(0.0f, 0.0f, -0.03f)
        });
        icon.AddChild(new MeshInstance3D
        {
            Name = "Diamond",
            Mesh = new BoxMesh { Size = new Vector3(0.30f, 0.30f, 0.07f) },
            MaterialOverride = Material(BlessingHex, 0.0f, 0.35f),
            RotationDegrees = new Vector3(0.0f, 0.0f, 45.0f)
        });
    }

    // Esfera com haste curta no topo. Material brilhante (roughness baixo) para o brilho especular da seção 8.
    private static void AddDevilsFruit(Node3D icon)
    {
        icon.AddChild(new MeshInstance3D
        {
            Name = "Fruit",
            Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f, RadialSegments = 24, Rings = 12 },
            MaterialOverride = FruitMaterial()
        });
        icon.AddChild(new MeshInstance3D
        {
            Name = "Stem",
            Mesh = new CylinderMesh { TopRadius = 0.02f, BottomRadius = 0.035f, Height = 0.12f, RadialSegments = 8 },
            MaterialOverride = Material(StemHex, 0.0f, 0.8f),
            Position = new Vector3(0.0f, 0.22f, 0.0f)
        });
    }

    // Três discos achatados empilhados. Cada disco é um cilindro bronze com um anel externo mais claro por cima.
    private static void AddCoins(Node3D icon)
    {
        for (var i = 0; i < 3; i++)
        {
            var y = (i - 1) * 0.075f;
            icon.AddChild(new MeshInstance3D
            {
                Name = $"Coin_{i}",
                Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.07f, RadialSegments = 24 },
                MaterialOverride = Material(CoinRimHex, 0.0f, 0.4f),
                Position = new Vector3(0.0f, y, 0.0f)
            });
            icon.AddChild(new MeshInstance3D
            {
                Name = $"CoinFace_{i}",
                Mesh = new CylinderMesh { TopRadius = 0.165f, BottomRadius = 0.165f, Height = 0.075f, RadialSegments = 24 },
                MaterialOverride = Material(CoinHex, 0.0f, 0.5f),
                Position = new Vector3(0.0f, y, 0.0f)
            });
        }
    }

    private static void AddLabel(Node3D icon, RoomRewardDoor door)
    {
        var label = new Label3D
        {
            Name = "RewardLabel",
            Text = ShortLabel(door),
            Position = Vector3.Up * LabelHeight,
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
        icon.AddChild(label);

        // O texto segue o idioma enquanto a porta existe; a assinatura sai quando o rótulo sai da árvore.
        var localization = Localization.Instance;
        if (localization == null)
            return;

        Localization.LanguageChangedEventHandler onLanguageChanged = _ =>
        {
            if (GodotObject.IsInstanceValid(label))
                label.Text = ShortLabel(door);
        };
        localization.LanguageChanged += onLanguageChanged;
        label.TreeExiting += () =>
        {
            if (Localization.Instance != null)
                Localization.Instance.LanguageChanged -= onLanguageChanged;
        };
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

    // Material fosco simples. emission > 0 só é usado na Fruta (docs/design/portas-recompensas.md, seção 8).
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
