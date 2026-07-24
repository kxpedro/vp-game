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
    private bool _insideFountain;
    private bool _insideAltar;
    private bool _fountainUsed;
    private bool _altarUsed;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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
            if (BloodBlessingCatalog.TryGetNextRarity(blessing.Rarity, out _))
                eligible.Add(blessing);
        if (eligible.Count == 0)
        {
            SetMessage("Não há bênçãos equipadas que possam subir de nível.");
            return;
        }

        foreach (var child in _upgradeChoices.GetChildren()) child.QueueFree();
        foreach (var blessing in eligible)
        {
            BloodBlessingCatalog.TryGetNextRarity(blessing.Rarity, out var nextRarity);
            var preview = BuildUpgradePreview(blessing, nextRarity);
            var choice = new Button
            {
                Text = $"{blessing.Name}  •  {blessing.Rarity} → {nextRarity.DisplayName}\n{preview}",
                TooltipText = BuildUpgradeTooltip(blessing, nextRarity, preview),
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(600.0f, 76.0f + Mathf.Max(0, preview.Split('\n').Length - 1) * 22.0f)
            };
            choice.Pressed += () => SacrificeForUpgrade(blessing.Id);
            _upgradeChoices.AddChild(choice);
        }

        _upgradeOverlay.Visible = true;
        GetTree().Paused = true;
        ControllerUiSupport.FocusFirst(_upgradeOverlay);
    }

    private static string BuildUpgradePreview(BloodBlessingRecord record, BloodBlessingRarity nextRarity)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return "Efeitos indisponíveis para comparação.";

        var lines = new List<string>();
        if (record.Id == "orlok_rats_blessing")
        {
            var currentCapacity = BloodBlessingCatalog.GetRarityLevel(record.Rarity);
            var nextCapacity = BloodBlessingCatalog.GetRarityLevel(nextRarity.DisplayName);
            lines.Add($"Ratos simultâneos: {currentCapacity} → {nextCapacity}");
        }

        for (var i = 0; i < definition.Effects.Length; i++)
        {
            var effect = definition.Effects[i];
            if (record.Id == "orlok_rats_blessing" && effect.Type == "RatSummonOnPestilentKill")
                continue;
            var baseValue = CompendiumTuning.GetFloat($"blessing.{record.Id}.effect.{i}.value", effect.BooleanValue ? 1.0f : effect.Value);
            var currentValue = effect.BooleanValue ? baseValue : baseValue * record.PowerMultiplier;
            var nextValue = effect.BooleanValue ? baseValue : baseValue * nextRarity.PowerMultiplier;
            var currentText = FormatUpgradeValue(effect, currentValue);
            var nextText = FormatUpgradeValue(effect, nextValue);
            lines.Add($"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {currentText} → {nextText}");
        }

        return lines.Count > 0 ? string.Join("\n", lines) : "Esta melhoria não altera valores numéricos.";
    }

    private static string BuildUpgradeTooltip(BloodBlessingRecord record, BloodBlessingRarity nextRarity, string preview)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return preview;
        return $"{definition.DisplayName}\n{definition.Description}\n\n{record.Rarity} → {nextRarity.DisplayName}\n{preview}";
    }

    private static string FormatUpgradeValue(BloodBlessingEffectDefinition effect, float value)
    {
        if (effect.Operation is "Set" or "SetBool")
            return value > 0.0f ? "ativo" : "inativo";
        if (effect.Operation.Contains("Multiplier") || effect.Type.Contains("Chance"))
            return $"{value * 100:+0.##;-0.##;0}%";
        return $"{value:+0.##;-0.##;0}";
    }

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
        SetMessage($"{upgraded.Name} ascendeu para {upgraded.Rarity}. O altar consumiu {sacrificedMaximum:0.#} de vida máxima.");
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
            Color = new Color(0.018f, 0.0f, 0.025f, 0.94f),
            AnchorRight = 1.0f, AnchorBottom = 1.0f,
            Visible = false
        };
        layer.AddChild(_upgradeOverlay);
        var panel = new VBoxContainer
        {
            AnchorLeft = 0.5f, AnchorTop = 0.5f, AnchorRight = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -310.0f, OffsetTop = -250.0f, OffsetRight = 310.0f, OffsetBottom = 250.0f,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _upgradeOverlay.AddChild(panel);
        panel.AddChild(new Label
        {
            Text = "ALTAR DO SACRIFÍCIO",
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0.0f, 46.0f)
        });
        panel.AddChild(new Label
        {
            Text = "Sacrifique 20% da vida máxima para elevar uma bênção em uma raridade.",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(560.0f, 62.0f)
        });
        var choiceScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(610.0f, 285.0f),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        panel.AddChild(choiceScroll);
        _upgradeChoices = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        choiceScroll.AddChild(_upgradeChoices);
        var close = new Button { Text = "Voltar", CustomMinimumSize = new Vector2(180.0f, 42.0f) };
        close.Pressed += CloseUpgradeOverlay;
        panel.AddChild(close);
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
