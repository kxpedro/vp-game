using Godot;
using System.Collections.Generic;

public partial class IntermissionChamber : Node3D
{
    [Export] public float FountainHealFraction { get; set; } = 0.35f;
    [Export] public float BlessingSacrificeFraction { get; set; } = 0.05f;

    private PlayerController _player;
    private Health _health;
    private Area3D _fountainArea;
    private Area3D _altarArea;
    private Label3D _fountainPrompt;
    private Label3D _altarPrompt;
    private Label _statusLabel;
    private ColorRect _upgradeOverlay;
    private VBoxContainer _upgradeChoices;
    private Label _upgradeStatusLabel;
    private readonly List<string> _upgradeOfferIds = new();
    private readonly RandomNumberGenerator _upgradeRng = new();
    private bool _insideFountain;
    private bool _insideAltar;
    private bool _fountainUsed;
    private bool _altarUsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        _upgradeRng.Randomize();
        ControllerUiSupport.EnsureMappings();
        _player = GetNode<PlayerController>("Player");
        _player.ProcessMode = ProcessModeEnum.Pausable;
        _health = _player.GetNode<Health>("Health");
        _fountainArea = GetNode<Area3D>("BloodFountain/InteractionArea");
        _altarArea = GetNode<Area3D>("SacrificeAltar/InteractionArea");
        _fountainPrompt = GetNode<Label3D>("BloodFountain/Prompt");
        _altarPrompt = GetNode<Label3D>("SacrificeAltar/Prompt");

        _fountainArea.BodyEntered += body => SetFountainPresence(body, true);
        _fountainArea.BodyExited += body => SetFountainPresence(body, false);
        _altarArea.BodyEntered += body => SetAltarPresence(body, true);
        _altarArea.BodyExited += body => SetAltarPresence(body, false);
        _health.HealthChanged += (_, _) => RefreshStatus();
        BuildInterface();
        RefreshPrompts();
        RefreshStatus();
    }

    public override void _ExitTree()
    {
        if (GetTree() != null)
            GetTree().Paused = false;
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (_upgradeOverlay?.Visible == true || !Input.IsActionJustPressed("interact"))
            return;

        if (_insideFountain)
            UseFountain();
        else if (_insideAltar)
            OpenUpgradeOverlay();
    }

    public override void _Input(InputEvent @event)
    {
        if (_upgradeOverlay?.Visible != true || !@event.IsActionPressed("ui_cancel"))
            return;
        CloseUpgradeOverlay();
        GetViewport().SetInputAsHandled();
    }

    private void SetFountainPresence(Node3D body, bool inside)
    {
        if (!body.IsInGroup("player")) return;
        _insideFountain = inside;
        RefreshPrompts();
    }

    private void SetAltarPresence(Node3D body, bool inside)
    {
        if (!body.IsInGroup("player")) return;
        _insideAltar = inside;
        RefreshPrompts();
    }

    private void UseFountain()
    {
        if (_fountainUsed)
        {
            SetMessage("A fonte já entregou seu sangue nesta passagem.");
            return;
        }
        if (_health.CurrentHealth >= _health.MaxHealth - 0.01f)
        {
            SetMessage("Sua vida já está cheia.");
            return;
        }

        var before = _health.CurrentHealth;
        _health.Heal(_health.MaxHealth * FountainHealFraction);
        _fountainUsed = true;
        SetMessage($"A fonte restaurou {Mathf.RoundToInt(_health.CurrentHealth - before)} de vida.");
        RefreshPrompts();
    }

    private void OpenUpgradeOverlay()
    {
        if (_altarUsed)
        {
            SetMessage("O altar já aceitou um sacrifício nesta passagem.");
            return;
        }

        var eligible = new List<BloodBlessingRecord>();
        foreach (var blessing in RunState.RunBlessings)
            if (blessing.Level < BloodBlessingProgression.MaximumLevel)
                eligible.Add(blessing);
        if (eligible.Count == 0)
        {
            SetMessage("Não há bênçãos equipadas que possam subir de nível.");
            return;
        }

        if (_upgradeOfferIds.Count == 0)
        {
            for (var i = eligible.Count - 1; i > 0; i--)
            {
                var swapIndex = _upgradeRng.RandiRange(0, i);
                (eligible[i], eligible[swapIndex]) = (eligible[swapIndex], eligible[i]);
            }
            for (var i = 0; i < Mathf.Min(3, eligible.Count); i++)
                _upgradeOfferIds.Add(eligible[i].Id);
        }

        foreach (var child in _upgradeChoices.GetChildren())
        {
            _upgradeChoices.RemoveChild(child);
            child.QueueFree();
        }
        var renderedOffers = 0;
        foreach (var blessingId in _upgradeOfferIds)
        {
            var blessing = eligible.Find(record => record.Id == blessingId);
            if (blessing == null)
                continue;
            var preview = BuildUpgradePreview(blessing);
            BloodBlessingCatalog.TryGet(blessing.Id, out var definition);
            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(1080.0f, 135.0f),
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            card.AddThemeStyleboxOverride("panel", UIThemeFactory.ChoiceCard(BlessingRarityColor(blessing.Rarity)));
            var row = new HBoxContainer();
            if (definition != null)
            {
                row.AddChild(new TextureRect
                {
                    Texture = ResourceLoader.Load<Texture2D>(definition.IconPath),
                    CustomMinimumSize = new Vector2(92.0f, 92.0f),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                });
            }
            row.AddChild(new RichTextLabel
            {
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                Text = $"[font_size=22][b]{blessing.Name}[/b] | {blessing.Rarity} | N{blessing.Level} → N{blessing.Level + 1}[/font_size]\n{preview}",
                TooltipText = BuildUpgradeTooltip(blessing, preview),
                CustomMinimumSize = new Vector2(790.0f, 120.0f),
                MouseFilter = Control.MouseFilterEnum.Ignore
            });
            var choose = new Button
            {
                Text = "Aprimorar",
                CustomMinimumSize = new Vector2(145.0f, 82.0f),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            var capturedId = blessing.Id;
            choose.Pressed += () => SacrificeForUpgrade(capturedId);
            row.AddChild(choose);
            card.AddChild(row);
            _upgradeChoices.AddChild(card);
            renderedOffers++;
        }

        _upgradeStatusLabel.Text = $"O altar revelou {renderedOffers} bênção(ões). Escolha uma para aprimorar sem alterar sua raridade.";
        _upgradeOverlay.Visible = true;
        GetTree().Paused = true;
        ControllerUiSupport.FocusFirst(_upgradeOverlay);
    }

    private static string BuildUpgradePreview(BloodBlessingRecord record)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return "Efeitos indisponíveis para comparação.";

        var lines = new List<string>();
        if (record.Id == "orlok_rats_blessing")
        {
            var currentCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record);
            var nextCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record with { Level = record.Level + 1 });
            lines.Add($"Ratos simultâneos: {currentCapacity} → {nextCapacity}");
        }

        for (var i = 0; i < definition.Effects.Length; i++)
        {
            var effect = definition.Effects[i];
            if (record.Id == "orlok_rats_blessing" && effect.Type == "RatSummonOnPestilentKill")
                continue;
            var baseValue = CompendiumTuning.GetFloat($"blessing.{record.Id}.effect.{i}.value", effect.BooleanValue ? 1.0f : effect.Value);
            var currentValue = effect.BooleanValue ? baseValue : baseValue * record.EffectivePowerMultiplier;
            var nextValue = effect.BooleanValue
                ? baseValue
                : baseValue * record.PowerMultiplier * BloodBlessingProgression.GetLevelMultiplier(record.Level + 1);
            if (effect.Type == "MaxHealth" && effect.Operation == "AddFlat")
            {
                var activeHealthMultiplier = RunState.MaxHealthMultiplier * RunState.SacrificedMaxHealthMultiplier;
                currentValue *= activeHealthMultiplier;
                nextValue *= activeHealthMultiplier;
            }
            var currentText = FormatUpgradeValue(effect, currentValue);
            var nextText = FormatUpgradeValue(effect, nextValue);
            lines.Add($"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {currentText} → {nextText}");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : "Esta melhoria não altera valores numéricos.";
    }

    private static string BuildUpgradeTooltip(BloodBlessingRecord record, string preview)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return preview;
        return $"{definition.DisplayName} [{record.Rarity}]\nN{record.Level} → N{record.Level + 1}\n\n{preview}";
    }

    private static string FormatUpgradeValue(BloodBlessingEffectDefinition effect, float value)
    {
        if (effect.Operation is "Set" or "SetBool")
            return value > 0.0f ? "ativo" : "inativo";
        if (effect.Type.Contains("Chance", System.StringComparison.OrdinalIgnoreCase))
            value = Mathf.Clamp(value, 0.0f, 1.0f);
        if (effect.Type == "PestilenceSpreadTargets")
            value = Mathf.Round(value);
        if (effect.Operation.Contains("Multiplier") || effect.Type.Contains("Chance"))
            return $"{value * 100:+0.##;-0.##;0}%";
        return $"{value:+0.##;-0.##;0}";
    }

    private static Color BlessingRarityColor(string rarity) => rarity.ToLowerInvariant() switch
    {
        "stirps" => new Color(1.0f, 0.08f, 0.22f),
        "uncommon" => new Color(0.2f, 0.9f, 0.3f),
        "rare" => new Color(0.2f, 0.48f, 1.0f),
        "epic" => new Color(0.68f, 0.25f, 0.95f),
        "legendary" => new Color(1.0f, 0.48f, 0.08f),
        _ => Colors.White
    };

    private void SacrificeForUpgrade(string blessingId)
    {
        if (!RunState.TryUpgradeBloodBlessing(blessingId, out var upgraded))
        {
            SetMessage("Essa bênção não pode mais ser aprimorada.");
            CloseUpgradeOverlay();
            return;
        }

        CloseUpgradeOverlay();
        _player.ApplyRuntimeTuning();
        var previousMaximum = _health.MaxHealth;
        RunState.SacrificeMaxHealth(BlessingSacrificeFraction);
        _player.ApplyRuntimeTuning();
        GetTree().CallGroup("hud", "RefreshBlessingDisplays");
        _altarUsed = true;
        var sacrificedMaximum = Mathf.Max(previousMaximum - _health.MaxHealth, 0.0f);
        GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_upgrade");
        SetMessage($"{upgraded.Name} foi aprimorada para N{upgraded.Level}. A raridade {upgraded.Rarity} foi preservada; o altar consumiu {sacrificedMaximum:0.#} de vida máxima.");
        RefreshPrompts();
    }

    private void CloseUpgradeOverlay()
    {
        _upgradeOverlay.Visible = false;
        GetTree().Paused = false;
    }

    private void BuildInterface()
    {
        var layer = new CanvasLayer { Name = "ChamberInterface", ProcessMode = ProcessModeEnum.Always };
        AddChild(layer);
        _statusLabel = new Label
        {
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -390.0f, OffsetTop = 22.0f, OffsetRight = 390.0f, OffsetBottom = 88.0f,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = "Câmara de Hemomancia"
        };
        layer.AddChild(_statusLabel);

        _upgradeOverlay = new ColorRect
        {
            Name = "BlessingUpgradeOverlay",
            ProcessMode = ProcessModeEnum.Always,
            Theme = UIThemeFactory.Create(),
            Color = new Color(0.012f, 0.0f, 0.018f, 0.96f),
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
            Visible = false
        };
        layer.AddChild(_upgradeOverlay);
        var altarPanel = new ColorRect
        {
            AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -620.0f, OffsetTop = -340.0f, OffsetRight = 620.0f, OffsetBottom = 340.0f,
            Color = new Color(0.025f, 0.0f, 0.008f, 0.98f)
        };
        _upgradeOverlay.AddChild(altarPanel);
        UIThemeFactory.AddSimpleFrame(
            altarPanel,
            "AltarFrame",
            new Vector4(0.0f, 0.0f, 1.0f, 1.0f),
            Vector4.Zero,
            new Color(0.12f, 0.055f, 0.07f, 0.96f),
            12);

        var title = new Label
        {
            Text = "ALTAR DO SACRIFÍCIO",
            AnchorRight = 1.0f,
            OffsetTop = 18.0f,
            OffsetBottom = 58.0f,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        title.AddThemeFontSizeOverride("font_size", 27);
        altarPanel.AddChild(title);
        _upgradeStatusLabel = new Label
        {
            Text = $"Sacrifique {BlessingSacrificeFraction * 100.0f:0.#}% da vida máxima para aprimorar o nível de uma bênção.",
            AnchorRight = 1.0f,
            OffsetTop = 62.0f,
            OffsetBottom = 108.0f,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        altarPanel.AddChild(_upgradeStatusLabel);
        _upgradeChoices = new VBoxContainer
        {
            OffsetLeft = 56.0f,
            OffsetTop = 120.0f,
            OffsetRight = 1184.0f,
            OffsetBottom = 585.0f,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _upgradeChoices.AddThemeConstantOverride("separation", 14);
        altarPanel.AddChild(_upgradeChoices);
        var close = new Button
        {
            Text = "Voltar",
            AnchorLeft = 1.0f,
            AnchorTop = 1.0f,
            AnchorRight = 1.0f,
            AnchorBottom = 1.0f,
            OffsetLeft = -238.0f,
            OffsetTop = -70.0f,
            OffsetRight = -58.0f,
            OffsetBottom = -28.0f
        };
        close.Pressed += CloseUpgradeOverlay;
        altarPanel.AddChild(close);
    }

    private void SetMessage(string message)
    {
        _statusLabel.Text = $"Câmara de Hemomancia\n{message}";
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null || _health == null) return;
        _statusLabel.Text = $"Câmara de Hemomancia  •  Vida {Mathf.CeilToInt(_health.CurrentHealth)}/{Mathf.CeilToInt(_health.MaxHealth)}";
    }

    private void RefreshPrompts()
    {
        if (_fountainPrompt != null)
        {
            _fountainPrompt.Visible = _insideFountain;
            _fountainPrompt.Text = _fountainUsed ? "Fonte exaurida" : "Interagir — recuperar 35% da vida";
        }
        if (_altarPrompt != null)
        {
            _altarPrompt.Visible = _insideAltar;
            _altarPrompt.Text = _altarUsed ? "Altar saciado" : "Interagir — sacrificar sangue por poder";
        }
    }
}
