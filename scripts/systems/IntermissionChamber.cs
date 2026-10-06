using Godot;
using System;
using System.Collections.Generic;

// Câmara de Hemomancia (guia de UI §8.19): fonte de cura + altar que aprimora uma bênção pagando vida máxima.
// O altar é um Window(Large) com ChoiceCard (o mesmo card das ofertas de bênção/artefato) e entra na UiModalStack.
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
    private Control _uiRoot;
    private Control _statusRoot;
    private Label _statusTitleLabel;
    private Label _statusDetailLabel;
    private UiWindow _altarWindow;
    private ColorRect _upgradeOverlay;
    private VBoxContainer _upgradeChoices;
    private readonly List<UiChoiceCard> _upgradeCards = new();
    private readonly List<string> _upgradeOfferIds = new();
    private readonly RandomNumberGenerator _upgradeRng = new();
    private Action<InputDevice> _deviceChangedHandler;
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
        SetupPromptLabels();

        _fountainArea.BodyEntered += body => SetFountainPresence(body, true);
        _fountainArea.BodyExited += body => SetFountainPresence(body, false);
        _altarArea.BodyEntered += body => SetAltarPresence(body, true);
        _altarArea.BodyExited += body => SetAltarPresence(body, false);
        _health.HealthChanged += (_, _) =>
        {
            RefreshStatus();
            RefreshPrompts();
        };
        BuildInterface();
        RefreshPrompts();
        RefreshStatus();
        // Debug: --ui-preview=altar abre o altar com bênçãos fictícias (ver OpenPreviewScreen).
        UiPreview.Attach(this);
    }

    public override void _ExitTree()
    {
        if (_deviceChangedHandler != null)
            UiInput.DeviceChanged -= _deviceChangedHandler;
        // Fecha o altar pela pilha (recalcula a pausa) em vez de escrever GetTree().Paused direto.
        if (_upgradeOverlay != null && IsInstanceValid(_upgradeOverlay))
            UiModalStack.Pop(_upgradeOverlay);
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        RefreshStatusVisibility();
        // ProcessMode e Always por causa do overlay. ConsumeInteract ignora o aperto com UI aberta ou árvore
        // pausada (neste frame e no anterior): o mesmo botão que fecha um overlay não reabre a fonte/o altar.
        if (UiModalStack.IsOpen(_upgradeOverlay) || !InteractPrompt.ConsumeInteract(this))
            return;

        if (_insideFountain)
            UseFountain();
        else if (_insideAltar)
            OpenUpgradeOverlay();
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

    // Mesma regra de UseFountain: fração da vida máxima, limitada ao que falta para encher (Health.Heal faz o clamp).
    private int FountainHealPreview()
    {
        if (_health == null)
            return 0;
        var missing = Mathf.Max(_health.MaxHealth - _health.CurrentHealth, 0.0f);
        return Mathf.RoundToInt(Mathf.Min(_health.MaxHealth * FountainHealFraction, missing));
    }

    private void UseFountain()
    {
        if (_fountainUsed)
        {
            SetMessage(UiComponents.TextOr("chamber.fountain.used_message", "A fonte já entregou seu sangue nesta passagem."));
            return;
        }
        if (_health.CurrentHealth >= _health.MaxHealth - 0.01f)
        {
            SetMessage(UiComponents.TextOr("chamber.fountain.full_message", "Sua vida já está cheia."));
            return;
        }

        var before = _health.CurrentHealth;
        _health.Heal(_health.MaxHealth * FountainHealFraction);
        _fountainUsed = true;
        SetMessage(string.Format(
            UiComponents.TextOr("chamber.fountain.healed_message", "A fonte restaurou {0} de vida."),
            Mathf.RoundToInt(_health.CurrentHealth - before)));
        RefreshPrompts();
        RefreshStatus();
    }

    // ---------- Altar (guia §8.19): Window(Large) + ChoiceCard + HintBar, pausa pela UiModalStack ----------

    public void OpenUpgradeOverlay()
    {
        if (_altarUsed)
        {
            SetMessage(UiComponents.TextOr("altar.used_message", "O altar já aceitou um sacrifício nesta passagem."));
            return;
        }

        var eligible = new List<BloodBlessingRecord>();
        foreach (var blessing in RunState.RunBlessings)
            if (blessing.Level < BloodBlessingProgression.MaximumLevel)
                eligible.Add(blessing);
        if (eligible.Count == 0)
        {
            SetMessage(UiComponents.TextOr("altar.no_eligible", "Não há bênçãos equipadas que possam subir de nível."));
            return;
        }

        // Sorteio único por passagem: fechar e reabrir mostra as mesmas opções.
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

        ClearUpgradeCards();
        foreach (var blessingId in _upgradeOfferIds)
        {
            var blessing = eligible.Find(record => record.Id == blessingId);
            if (blessing == null)
                continue;
            var capturedId = blessing.Id;
            var card = UiComponents.ChoiceCard(BuildUpgradeCardData(blessing), () => SacrificeForUpgrade(capturedId));
            card.Root.TooltipText = BuildUpgradeTooltip(blessing, BuildUpgradePreview(blessing));
            _upgradeChoices.AddChild(card.Root);
            _upgradeCards.Add(card);
        }
        if (_upgradeCards.Count == 0)
        {
            SetMessage(UiComponents.TextOr("altar.no_eligible", "Não há bênçãos equipadas que possam subir de nível."));
            return;
        }
        SetUpgradeChoiceNeighbors();

        _altarWindow.SetStatus(BuildAltarStatus());
        UiModalStack.Push(_upgradeOverlay, true, _upgradeCards[0].FocusTarget, CloseUpgradeOverlay);
        _altarWindow.FadeIn();
    }

    private string SacrificePercentText() => $"{BlessingSacrificeFraction * 100.0f:0.#}";

    // Estimativa do custo em pontos: a regra é multiplicativa sobre a vida máxima atual (RunState.SacrificeMaxHealth).
    private string BuildAltarStatus()
    {
        var maximum = _health?.MaxHealth ?? 0.0f;
        var cost = maximum * BlessingSacrificeFraction;
        return string.Format(
            UiComponents.TextOr("altar.status", "Sacrifique {0}% da vida máxima (≈ {1} de {2}) para aprimorar uma bênção. A raridade é preservada."),
            SacrificePercentText(),
            $"{cost:0.#}",
            Mathf.CeilToInt(maximum));
    }

    private ChoiceCardData BuildUpgradeCardData(BloodBlessingRecord blessing)
    {
        BloodBlessingCatalog.TryGet(blessing.Id, out var definition);
        var metaParts = new List<string>();
        if (definition != null && !string.IsNullOrEmpty(definition.Family))
            metaParts.Add(string.Format(UiComponents.TextOr("offer.blessing.lineage", "Linhagem {0}"), FormatLineage(definition)));
        metaParts.Add(string.Format(UiComponents.TextOr("offer.blessing.level_up", "N{0} → N{1}"), blessing.Level, blessing.Level + 1));
        return new ChoiceCardData
        {
            Width = 1080.0f,
            Icon = LoadIcon(definition),
            Title = definition?.DisplayName ?? blessing.Name,
            RarityId = blessing.Rarity,
            Meta = string.Join("  ·  ", metaParts),
            PreviewBbcode = FormatPreviewBbcode(BuildUpgradePreview(blessing)),
            ActionText = UiComponents.TextOr("ui.common.upgrade", "Aprimorar"),
            CostText = string.Format(UiComponents.TextOr("altar.cost", "−{0}% vida máx."), SacrificePercentText()),
            CostColor = UiPalette.Warn
        };
    }

    private static string FormatLineage(BloodBlessingDefinition definition)
    {
        return definition.ParentFamilies != null && definition.ParentFamilies.Length > 0
            ? $"{definition.Family}: {string.Join(" + ", definition.ParentFamilies)}"
            : definition.Family;
    }

    private static Texture2D LoadIcon(BloodBlessingDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.IconPath) || !ResourceLoader.Exists(definition.IconPath))
            return null;
        return ResourceLoader.Load<Texture2D>(definition.IconPath);
    }

    // "Efeito: atual → próximo": valor atual em TextSecondary, próximo em Gold (a seta também separa sem depender de cor).
    private static string FormatPreviewBbcode(string preview)
    {
        var current = UiPalette.TextSecondary.ToHtml(false);
        var next = UiPalette.Gold.ToHtml(false);
        var lines = preview.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Replace("[", "[lb]");
            var colon = line.LastIndexOf(": ", StringComparison.Ordinal);
            var arrow = line.IndexOf(" → ", StringComparison.Ordinal);
            if (colon < 0 || arrow < colon)
            {
                lines[i] = line;
                continue;
            }
            var label = line.Substring(0, colon + 2);
            var before = line.Substring(colon + 2, arrow - colon - 2);
            var after = line.Substring(arrow + 3);
            lines[i] = $"{label}[color=#{current}]{before}[/color] → [color=#{next}][b]{after}[/b][/color]";
        }
        return string.Join("\n", lines);
    }

    private static string BuildUpgradePreview(BloodBlessingRecord record)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return UiComponents.TextOr("altar.preview_unavailable", "Efeitos indisponíveis para comparação.");

        var lines = new List<string>();
        var unchangedLines = new List<string>();
        var hasScalingLine = false;
        if (record.Id == "orlok_rats_blessing")
        {
            var currentCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record);
            var nextCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record with { Level = record.Level + 1 });
            lines.Add($"{UiComponents.TextOr("altar.preview_rats", "Ratos simultâneos")}: {currentCapacity} → {nextCapacity}");
            hasScalingLine = true;
        }

        for (var i = 0; i < definition.Effects.Length; i++)
        {
            var effect = definition.Effects[i];
            if (record.Id == "orlok_rats_blessing" && effect.Type == "RatSummonOnPestilentKill")
                continue;
            // Efeito booleano (Set/SetBool): o nível não o altera, e os parâmetros (duração, dano/s, raio) não escalam
            // (RunState.ApplyBlessingEffect só multiplica o valor de efeitos numéricos). Mostra os parâmetros atuais,
            // sem seta, em vez de "ativo → ativo".
            if (effect.BooleanValue || effect.Operation is "Set" or "SetBool")
            {
                unchangedLines.Add(FormatUnchangedEffect(effect));
                continue;
            }
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
            hasScalingLine = true;
        }

        lines.AddRange(unchangedLines);
        if (!hasScalingLine)
            lines.Add(UiComponents.TextOr("altar.preview_no_numbers", "Esta melhoria não altera valores numéricos."));
        return string.Join("\n", lines);
    }

    // "Nome: mantido (4 s · 10/s · até 3 acúmulos)": parâmetros fixos do efeito booleano, sem seta (não escalam com o nível).
    private static string FormatUnchangedEffect(BloodBlessingEffectDefinition effect)
    {
        var details = new List<string>();
        // Parâmetros "bleed*" (ex.: poça que também sangra) viram um grupo "sangramento 5 s, 10 dano/s, …".
        var bleedDetails = new List<string>();
        foreach (var parameter in effect.NumberParameters)
        {
            var isBleed = parameter.Key.Length > 5 && parameter.Key.StartsWith("bleed", StringComparison.OrdinalIgnoreCase);
            var text = FormatEffectParameter(isBleed ? parameter.Key.Substring(5) : parameter.Key, parameter.Value);
            if (!string.IsNullOrEmpty(text))
                (isBleed ? bleedDetails : details).Add(text);
        }
        if (bleedDetails.Count > 0)
            details.Add(string.Format(UiComponents.TextOr("altar.param_bleed", "sangramento {0}"), string.Join(", ", bleedDetails)));
        var unchanged = UiComponents.TextOr("altar.value_unchanged", "mantido");
        var suffix = details.Count > 0 ? $" ({string.Join(" · ", details)})" : "";
        return $"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {unchanged}{suffix}";
    }

    private static string FormatEffectParameter(string key, float value)
    {
        var lower = key.ToLowerInvariant();
        if (lower.Contains("duration") || lower.Contains("delay"))
            return string.Format(UiComponents.TextOr("altar.param_seconds", "{0} s"), $"{value:0.#}");
        if (lower.Contains("damagepersecond"))
            return string.Format(UiComponents.TextOr("altar.param_dps", "{0} dano/s"), $"{value:0.#}");
        if (lower.Contains("maxstacks"))
            return string.Format(UiComponents.TextOr("altar.param_stacks", "até {0} acúmulos"), $"{value:0}");
        if (lower.Contains("radius"))
            return string.Format(UiComponents.TextOr("altar.param_radius", "raio {0} m"), $"{value:0.#}");
        return "";
    }

    private static string BuildUpgradeTooltip(BloodBlessingRecord record, string preview)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return preview;
        return $"{definition.DisplayName} [{UiComponents.RarityLabel(record.Rarity)}]\nN{record.Level} → N{record.Level + 1}\n\n{preview}";
    }

    private static string FormatUpgradeValue(BloodBlessingEffectDefinition effect, float value)
    {
        if (effect.Operation is "Set" or "SetBool")
            return value > 0.0f ? UiComponents.TextOr("altar.value_active", "ativo") : UiComponents.TextOr("altar.value_inactive", "inativo");
        if (effect.Type.Contains("Chance", StringComparison.OrdinalIgnoreCase))
            value = Mathf.Clamp(value, 0.0f, 1.0f);
        if (effect.Type == "PestilenceSpreadTargets")
            value = Mathf.Round(value);
        if (effect.Operation.Contains("Multiplier") || effect.Type.Contains("Chance"))
            return $"{value * 100:+0.##;-0.##;0}%";
        return $"{value:+0.##;-0.##;0}";
    }

    private void SacrificeForUpgrade(string blessingId)
    {
        if (!UiModalStack.IsOpen(_upgradeOverlay))
            return;
        if (!RunState.TryUpgradeBloodBlessing(blessingId, out var upgraded))
        {
            SetMessage(UiComponents.TextOr("altar.cannot_upgrade", "Essa bênção não pode mais ser aprimorada."));
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
        SetMessage(string.Format(
            UiComponents.TextOr("altar.upgraded_message", "{0} aprimorada para N{1} ({2} preservada). O altar consumiu {3} de vida máxima."),
            upgraded.Name, upgraded.Level, UiComponents.RarityLabel(upgraded.Rarity), $"{sacrificedMaximum:0.#}"));
        RefreshPrompts();
        RefreshStatus();
    }

    private void CloseUpgradeOverlay()
    {
        if (_upgradeOverlay != null)
            UiModalStack.Pop(_upgradeOverlay);
    }

    // Rodapé "[✕] Aprimorar": escolhe só o card com foco (a escolha cobra o sacrifício).
    // Sem card focado (ex.: clique no rodapé), só foca o primeiro card.
    private void ChooseFocusedUpgrade()
    {
        foreach (var card in _upgradeCards)
        {
            if (card.ActionButton != null && card.ActionButton.HasFocus())
            {
                card.Choose();
                return;
            }
        }
        if (_upgradeCards.Count > 0)
            _upgradeCards[0].FocusTarget?.GrabFocus();
    }

    // Refresh sem foco preso em nó morto (§5.1 regra 10): RemoveChild + QueueFree.
    private void ClearUpgradeCards()
    {
        foreach (var child in _upgradeChoices.GetChildren())
        {
            _upgradeChoices.RemoveChild(child);
            child.QueueFree();
        }
        _upgradeCards.Clear();
    }

    // Cima/baixo com wrap; esquerda/direita andam para o card anterior/seguinte (igual à oferta de bênção).
    private void SetUpgradeChoiceNeighbors()
    {
        var items = new List<Control>();
        foreach (var card in _upgradeCards)
            items.Add(card.FocusTarget);
        ControllerUiSupport.SetVerticalCycle(items);
        foreach (var item in items)
        {
            item.FocusNeighborLeft = item.FocusNeighborTop;
            item.FocusNeighborRight = item.FocusNeighborBottom;
        }
    }

    // ---------- Interface ----------

    private void BuildInterface()
    {
        var layer = new CanvasLayer { Name = "ChamberInterface", ProcessMode = ProcessModeEnum.Always };
        AddChild(layer);
        _uiRoot = new Control
        {
            Name = "ChamberUi",
            Theme = UIThemeFactory.Create(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ProcessMode = ProcessModeEnum.Always
        };
        _uiRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(_uiRoot);

        // Título + estado da Câmara dentro da moldura do título do HUD (800×76, topo 16; ver HUD.StatusBars).
        // Mensagens da fonte/do altar viram toast do HUD (NotifySkillMessage), não texto aqui.
        var status = new VBoxContainer
        {
            Name = "ChamberStatus",
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            OffsetLeft = -390.0f, OffsetTop = 22.0f, OffsetRight = 390.0f, OffsetBottom = 86.0f,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        status.AddThemeConstantOverride("separation", 0);
        _uiRoot.AddChild(status);
        _statusRoot = status;
        _statusTitleLabel = new Label
        {
            Text = UiComponents.TextOr("chamber.title", "Câmara de Hemomancia"),
            ThemeTypeVariation = UiVariation.SectionGoldLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _statusTitleLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
        status.AddChild(_statusTitleLabel);
        _statusDetailLabel = new Label
        {
            ThemeTypeVariation = UiVariation.SecondaryLabel,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        // Texto essencial sobre o mundo: TextPrimary e outline do HUD (§2.3).
        _statusDetailLabel.AddThemeColorOverride("font_color", UiPalette.TextPrimary);
        _statusDetailLabel.AddThemeConstantOverride("outline_size", UiFont.OutlineHud);
        status.AddChild(_statusDetailLabel);

        _altarWindow = UiComponents.Window(_uiRoot, "BlessingUpgradeOverlay", WindowSize.Large,
            UiComponents.TextOr("altar.title", "Altar do Sacrifício"));
        _upgradeOverlay = _altarWindow.Root;
        _upgradeOverlay.Visible = false;
        var scroll = UiComponents.ScrollList(0, out _upgradeChoices);
        _altarWindow.Body.AddChild(scroll);
        _altarWindow.Footer.Add(UiAction.Cancel, UiComponents.TextOr("ui.common.back", "Voltar"), CloseUpgradeOverlay, left: true);
        _altarWindow.Footer.Add(UiAction.Accept, UiComponents.TextOr("ui.common.upgrade", "Aprimorar"), ChooseFocusedUpgrade);
    }

    // Esconde o título da Câmara enquanto houver tela modal/pausa (ele fica num CanvasLayer acima do HUD).
    private void RefreshStatusVisibility()
    {
        if (_statusRoot == null)
            return;
        var blocked = GetTree().Paused || UiModalStack.AnyOpen;
        if (_statusRoot.Visible == blocked)
            _statusRoot.Visible = !blocked;
    }

    private void SetMessage(string message)
    {
        GetTree().CallGroup("hud", "NotifySkillMessage", message);
    }

    private void RefreshStatus()
    {
        if (_statusDetailLabel == null) return;
        var fountain = _fountainUsed
            ? UiComponents.TextOr("chamber.status.fountain_used", "Fonte: exaurida")
            : UiComponents.TextOr("chamber.status.fountain_ready", "Fonte: disponível");
        var altar = _altarUsed
            ? UiComponents.TextOr("chamber.status.altar_used", "Altar: saciado")
            : UiComponents.TextOr("chamber.status.altar_ready", "Altar: disponível");
        _statusDetailLabel.Text = $"{fountain}  ·  {altar}";
    }

    // ---------- Rótulos 3D (helper comum InteractPrompt, guia §8.8) ----------

    private void SetupPromptLabels()
    {
        StylePromptLabel(_fountainPrompt);
        StylePromptLabel(_altarPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("BloodFountain"), _fountainPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("SacrificeAltar"), _altarPrompt);
        _deviceChangedHandler = _ => RefreshPrompts();
        UiInput.DeviceChanged += _deviceChangedHandler;
    }

    // Mesmo visual de InteractPrompt.CreatePrompt, aplicado aos Label3D "Prompt" que já existem na cena.
    private static void StylePromptLabel(Label3D label) => InteractPrompt.StylePrompt(label);

    private void RefreshPrompts()
    {
        if (_fountainPrompt != null && IsInstanceValid(_fountainPrompt))
        {
            _fountainPrompt.Visible = _insideFountain;
            var heal = FountainHealPreview();
            if (_fountainUsed)
                _fountainPrompt.Text = UiComponents.TextOr("chamber.fountain.exhausted", "Fonte exaurida");
            else if (heal <= 0)
                _fountainPrompt.Text = UiComponents.TextOr("chamber.fountain.full", "Vida cheia");
            else
                InteractPrompt.SetText(_fountainPrompt, string.Format(
                    UiComponents.TextOr("chamber.fountain.prompt", "Beber: recuperar {0} de vida"), heal));
        }
        if (_altarPrompt != null && IsInstanceValid(_altarPrompt))
        {
            _altarPrompt.Visible = _insideAltar;
            if (_altarUsed)
                _altarPrompt.Text = UiComponents.TextOr("chamber.altar.sated", "Altar saciado");
            else
                InteractPrompt.SetText(_altarPrompt, string.Format(
                    UiComponents.TextOr("chamber.altar.prompt", "Sacrificar {0}% da vida máx. por poder"), SacrificePercentText()));
        }
    }

    // ---------- Pré-visualização (debug, --ui-preview=altar; ver UiPreview.cs) ----------

    // Garante bênçãos aprimoráveis (fictícias: Rara N1, Épica N2, Stirps N1) e abre o altar. False = tela desconhecida.
    internal bool OpenPreviewScreen(string screen)
    {
        if (screen != "altar" || !OS.IsDebugBuild())
            return false;
        var hasEligible = false;
        foreach (var record in RunState.RunBlessings)
            hasEligible |= record.Level < BloodBlessingProgression.MaximumLevel;
        if (!hasEligible)
            SeedPreviewBlessings();
        _altarUsed = false;
        _upgradeOfferIds.Clear();
        OpenUpgradeOverlay();
        return true;
    }

    private static void SeedPreviewBlessings()
    {
        var wanted = new[] { "Rare", "Epic", "Stirps" };
        var usedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var definition in BloodBlessingCatalog.All)
        {
            if (added >= wanted.Length)
                break;
            var isStirps = definition.Rarity.Equals("Stirps", StringComparison.OrdinalIgnoreCase);
            if (isStirps != (wanted[added] == "Stirps"))
                continue;
            if (!string.IsNullOrWhiteSpace(definition.ExclusiveGroup) && !usedGroups.Add(definition.ExclusiveGroup))
                continue;
            var rarity = wanted[added];
            var power = 1.0f;
            foreach (var entry in BloodBlessingCatalog.AllRarities)
                if (entry.Id.Equals(rarity, StringComparison.OrdinalIgnoreCase) || entry.DisplayName.Equals(rarity, StringComparison.OrdinalIgnoreCase))
                    power = entry.PowerMultiplier;
            if (!RunState.AddBloodBlessing(new BloodBlessingOffer(definition, rarity, power)))
                continue;
            if (rarity == "Epic")
                RunState.TryUpgradeBloodBlessing(definition.Id, out _);
            added++;
        }
    }
}
