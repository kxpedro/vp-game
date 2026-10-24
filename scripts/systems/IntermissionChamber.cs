using Godot;
using System;
using System.Collections.Generic;

// Câmara de Hemomancia (guia de UI §8.19): fonte de cura + altar que aprimora uma bênção pagando vida máxima
// + Loja de Artefatos (balcão criado em código, geometria provisória; a tela é o overlay HUD.Shop.cs).
// O altar é um Window(Large) com ChoiceCard (o mesmo card das ofertas de bênção/artefato) e entra na UiModalStack.
public partial class IntermissionChamber : Node3D
{
    [Export] public float FountainHealFraction { get; set; } = 0.35f;
    // Fracao da vida maxima sacrificada no altar (altar.sacrifice_fraction; padrao 0,05). Nenhuma cena sobrescreve o valor.
    public float BlessingSacrificeFraction => CompendiumTuning.GetFloat("altar.sacrifice_fraction", 0.05f);

    private const string RoomScenePath = "res://scenes/game/TestArena.tscn";

    private PlayerController _player;
    private Health _health;
    private Area3D _fountainArea;
    private Area3D _altarArea;
    private Label3D _fountainPrompt;
    private Label3D _altarPrompt;
    private Area3D _shopArea;
    private Label3D _shopPrompt;
    private Label3D _shopTitle;
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
    private bool _insideShop;
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
        BuildShopStand();
        SetupPromptLabels();

        _fountainArea.BodyEntered += body => SetFountainPresence(body, true);
        _fountainArea.BodyExited += body => SetFountainPresence(body, false);
        _altarArea.BodyEntered += body => SetAltarPresence(body, true);
        _altarArea.BodyExited += body => SetAltarPresence(body, false);
        _shopArea.BodyEntered += body => SetShopPresence(body, true);
        _shopArea.BodyExited += body => SetShopPresence(body, false);
        _health.HealthChanged += (_, _) =>
        {
            RefreshStatus();
            RefreshPrompts();
        };
        BuildInterface();
        RefreshPrompts();
        RefreshStatus();
        BuildRewardDoors();
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged += OnLanguageChanged;
        // Debug: --ui-preview=altar abre o altar com bênçãos fictícias (ver OpenPreviewScreen).
        UiPreview.Attach(this);
    }

    // Portas da entrada do mapa seguinte (origem 0, docs/design/portas-recompensas.md, 7.3). Não há recompensa pendente
    // na Câmara (o boss não tem porta), então as portas já nascem abertas. Antes do mapa 4 não há conjunto: a saída é única.
    private void BuildRewardDoors()
    {
        var exit = GetNodeOrNull<Node3D>("ExitPortal");
        if (exit == null)
            return;
        // Parede norte (NorthWall, sala de 24x24) virada para dentro (+Z), com o portão do hub. Sem conjunto (mapa 4),
        // a saída única fica nesse mesmo portão.
        var wall = GetNode<Node3D>("NorthWall");
        var thickness = (wall.GetNode<CollisionShape3D>("Collision").Shape as BoxShape3D)?.Size.Z ?? 0.45f;
        ScenePortal.PlaceOnWall(exit, new Vector3(0.0f, 0.18f, wall.Position.Z), new Vector3(0.0f, 0.0f, 1.0f), thickness);
        var pixelWall = PixelSceneSetup.ExitWallOf(this);
        ScenePortal.SetupExitGate(exit, pixelWall);
        var set = RunState.GetOrRollRoomRewardSet(Mathf.Clamp(RunState.CurrentMapIndex, 0, 3), 0);
        if (set != null)
            ScenePortal.BuildRewardDoors(exit, set, RoomScenePath, ScenePortal.RunPortalAction.None, true, pixelWall);
    }

    // Troca de idioma (pausa): reescreve título, rodapé do altar, estado e rótulos 3D. O altar aberto refaz os cards.
    private void OnLanguageChanged(string language)
    {
        if (_statusTitleLabel != null)
            _statusTitleLabel.Text = Localization.Text("chamber.title");
        if (_shopTitle != null && IsInstanceValid(_shopTitle))
            _shopTitle.Text = Localization.Text("chamber.shop.title");
        if (_altarWindow != null)
        {
            _altarWindow.SetTitle(Localization.Text("altar.title"));
            BuildAltarFooter();
        }
        RefreshStatus();
        RefreshPrompts();
        if (_upgradeOverlay != null && UiModalStack.IsOpen(_upgradeOverlay))
        {
            UiModalStack.Pop(_upgradeOverlay);
            OpenUpgradeOverlay();
        }
    }

    private void BuildAltarFooter()
    {
        _altarWindow.Footer.Clear();
        _altarWindow.Footer.Add(UiAction.Cancel, Localization.Text("ui.common.back"), CloseUpgradeOverlay, left: true);
        _altarWindow.Footer.Add(UiAction.Accept, Localization.Text("ui.common.upgrade"), ChooseFocusedUpgrade);
    }

    public override void _ExitTree()
    {
        if (_deviceChangedHandler != null)
            UiInput.DeviceChanged -= _deviceChangedHandler;
        if (Localization.Instance != null)
            Localization.Instance.LanguageChanged -= OnLanguageChanged;
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
        else if (_insideShop)
            OpenShop();
    }

    // A tela da loja vive no HUD (HUD.Shop.cs), que guarda o estoque sorteado da visita.
    private void OpenShop()
    {
        GetTree().CallGroup("hud", "OpenShop");
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

    private void SetShopPresence(Node3D body, bool inside)
    {
        if (!body.IsInGroup("player")) return;
        _insideShop = inside;
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
            SetMessage(Localization.Text("chamber.fountain.used_message"));
            return;
        }
        if (_health.CurrentHealth >= _health.MaxHealth - 0.01f)
        {
            SetMessage(Localization.Text("chamber.fountain.full_message"));
            return;
        }

        var before = _health.CurrentHealth;
        _health.Heal(_health.MaxHealth * FountainHealFraction);
        _fountainUsed = true;
        SetMessage(Localization.TextFormat("chamber.fountain.healed_message",
            Mathf.RoundToInt(_health.CurrentHealth - before)));
        RefreshPrompts();
        RefreshStatus();
    }

    // ---------- Altar (guia §8.19): Window(Large) + ChoiceCard + HintBar, pausa pela UiModalStack ----------

    public void OpenUpgradeOverlay()
    {
        if (_altarUsed)
        {
            SetMessage(Localization.Text("altar.used_message"));
            return;
        }

        var eligible = new List<BloodBlessingRecord>();
        foreach (var blessing in RunState.RunBlessings)
            if (blessing.Level < BloodBlessingProgression.MaximumLevel)
                eligible.Add(blessing);
        if (eligible.Count == 0)
        {
            SetMessage(Localization.Text("altar.no_eligible"));
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
            SetMessage(Localization.Text("altar.no_eligible"));
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
        return Localization.TextFormat("altar.status",
            SacrificePercentText(),
            $"{cost:0.#}",
            Mathf.CeilToInt(maximum));
    }

    private ChoiceCardData BuildUpgradeCardData(BloodBlessingRecord blessing)
    {
        BloodBlessingCatalog.TryGet(blessing.Id, out var definition);
        var metaParts = new List<string>();
        if (definition != null && !string.IsNullOrEmpty(definition.Family))
            metaParts.Add(Localization.TextFormat("offer.blessing.lineage", FormatLineage(definition)));
        metaParts.Add(Localization.TextFormat("offer.blessing.level_up", blessing.Level, blessing.Level + 1));
        return new ChoiceCardData
        {
            Width = 1080.0f,
            Icon = LoadIcon(definition),
            Title = definition?.DisplayName ?? blessing.Name,
            RarityId = blessing.Rarity,
            Meta = string.Join("  ·  ", metaParts),
            PreviewBbcode = FormatPreviewBbcode(BuildUpgradePreview(blessing)),
            ActionText = Localization.Text("ui.common.upgrade"),
            CostText = Localization.TextFormat("altar.cost", SacrificePercentText()),
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
            return Localization.Text("altar.preview_unavailable");

        var lines = new List<string>();
        var unchangedLines = new List<string>();
        var hasScalingLine = false;
        if (record.Id == "orlok_rats_blessing")
        {
            var currentCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record);
            var nextCapacity = BloodBlessingCatalog.GetSummonedRatCapacity(record with { Level = record.Level + 1 });
            lines.Add($"{Localization.Text("altar.preview_rats")}: {currentCapacity} → {nextCapacity}");
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
            lines.Add(Localization.Text("altar.preview_no_numbers"));
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
            details.Add(Localization.TextFormat("altar.param_bleed", string.Join(", ", bleedDetails)));
        var unchanged = Localization.Text("altar.value_unchanged");
        var suffix = details.Count > 0 ? $" ({string.Join(" · ", details)})" : "";
        return $"{ArtifactEffectRegistry.FriendlyName(effect.Type)}: {unchanged}{suffix}";
    }

    private static string FormatEffectParameter(string key, float value)
    {
        var lower = key.ToLowerInvariant();
        if (lower.Contains("duration") || lower.Contains("delay"))
            return Localization.TextFormat("altar.param_seconds", $"{value:0.#}");
        if (lower.Contains("damagepersecond"))
            return Localization.TextFormat("altar.param_dps", $"{value:0.#}");
        if (lower.Contains("maxstacks"))
            return Localization.TextFormat("altar.param_stacks", $"{value:0}");
        if (lower.Contains("radius"))
            return Localization.TextFormat("altar.param_radius", $"{value:0.#}");
        return "";
    }

    private static string BuildUpgradeTooltip(BloodBlessingRecord record, string preview)
    {
        if (!BloodBlessingCatalog.TryGet(record.Id, out var definition))
            return preview;
        return $"{definition.DisplayName} [{UiComponents.RarityLabel(record.Rarity)}]\n{Localization.TextFormat("offer.blessing.level_up", record.Level, record.Level + 1)}\n\n{preview}";
    }

    private static string FormatUpgradeValue(BloodBlessingEffectDefinition effect, float value)
    {
        if (effect.Operation is "Set" or "SetBool")
            return value > 0.0f ? Localization.Text("altar.value_active") : Localization.Text("altar.value_inactive");
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
            SetMessage(Localization.Text("altar.cannot_upgrade"));
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
        SetMessage(Localization.TextFormat("altar.upgraded_message",
            upgraded.DisplayName, upgraded.Level, UiComponents.RarityLabel(upgraded.Rarity), $"{sacrificedMaximum:0.#}"));
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
            Text = Localization.Text("chamber.title"),
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
            Localization.Text("altar.title"));
        _upgradeOverlay = _altarWindow.Root;
        _upgradeOverlay.Visible = false;
        var scroll = UiComponents.ScrollList(0, out _upgradeChoices);
        _altarWindow.Body.AddChild(scroll);
        BuildAltarFooter();
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
            ? Localization.Text("chamber.status.fountain_used")
            : Localization.Text("chamber.status.fountain_ready");
        var altar = _altarUsed
            ? Localization.Text("chamber.status.altar_used")
            : Localization.Text("chamber.status.altar_ready");
        _statusDetailLabel.Text = $"{fountain}  ·  {altar}";
    }

    // ---------- Rótulos 3D (helper comum InteractPrompt, guia §8.8) ----------

    private void SetupPromptLabels()
    {
        StylePromptLabel(_fountainPrompt);
        StylePromptLabel(_altarPrompt);
        StylePromptLabel(_shopPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("BloodFountain"), _fountainPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("SacrificeAltar"), _altarPrompt);
        InteractPrompt.StyleTitlesAndAnchor(GetNode("ArtifactShop"), _shopPrompt);
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
                _fountainPrompt.Text = Localization.Text("chamber.fountain.exhausted");
            else if (heal <= 0)
                _fountainPrompt.Text = Localization.Text("chamber.fountain.full");
            else
                InteractPrompt.SetText(_fountainPrompt, Localization.TextFormat("chamber.fountain.prompt", heal));
        }
        if (_altarPrompt != null && IsInstanceValid(_altarPrompt))
        {
            _altarPrompt.Visible = _insideAltar;
            if (_altarUsed)
                _altarPrompt.Text = Localization.Text("chamber.altar.sated");
            else
                InteractPrompt.SetText(_altarPrompt, Localization.TextFormat("chamber.altar.prompt", SacrificePercentText()));
        }
        if (_shopPrompt != null && IsInstanceValid(_shopPrompt))
        {
            _shopPrompt.Visible = _insideShop;
            InteractPrompt.SetText(_shopPrompt, Localization.Text("chamber.shop.prompt"));
        }
    }

    // ---------- Loja de Artefatos (balcão em geometria provisória, criado em código) ----------

    // Posição livre na Câmara: entre a fonte (x −4,2) e o altar (x +4,2), no caminho do portal (z −9,2). Sem colisão,
    // como a fonte e o altar. A Area3D fica na frente do balcão (+z), de onde o jogador chega.
    private void BuildShopStand()
    {
        var stand = new Node3D { Name = "ArtifactShop", Position = new Vector3(0.0f, 0.15f, -4.6f) };
        AddChild(stand);

        var stone = new StandardMaterial3D { AlbedoColor = new Color(0.13f, 0.10f, 0.11f), Roughness = 0.86f, MetallicSpecular = 0.3f };
        var bloodWood = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.20f, 0.04f, 0.06f),
            Roughness = 0.6f,
            EmissionEnabled = true,
            Emission = new Color(0.45f, 0.02f, 0.04f),
            EmissionEnergyMultiplier = 0.18f
        };
        var blood = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.65f, 0.015f, 0.035f),
            Roughness = 0.42f,
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.01f, 0.025f),
            EmissionEnergyMultiplier = 0.9f
        };
        var brass = new StandardMaterial3D { AlbedoColor = new Color(0.62f, 0.46f, 0.18f), Metallic = 0.8f, Roughness = 0.35f };

        AddShopPart(stand, "Counter", new BoxMesh { Size = new Vector3(2.8f, 0.95f, 1.1f) }, stone, new Vector3(0.0f, 0.475f, 0.0f));
        AddShopPart(stand, "CounterTop", new BoxMesh { Size = new Vector3(3.0f, 0.14f, 1.3f) }, bloodWood, new Vector3(0.0f, 1.02f, 0.0f));
        AddShopPart(stand, "BackPanel", new BoxMesh { Size = new Vector3(3.0f, 1.5f, 0.18f) }, stone, new Vector3(0.0f, 1.84f, -0.55f));
        AddShopPart(stand, "PillarLeft", new BoxMesh { Size = new Vector3(0.28f, 2.4f, 0.28f) }, stone, new Vector3(-1.5f, 1.2f, -0.55f));
        AddShopPart(stand, "PillarRight", new BoxMesh { Size = new Vector3(0.28f, 2.4f, 0.28f) }, stone, new Vector3(1.5f, 1.2f, -0.55f));
        AddShopPart(stand, "Banner", new BoxMesh { Size = new Vector3(1.1f, 1.2f, 0.04f) }, blood, new Vector3(0.0f, 1.95f, -0.44f));
        // Oferendas sobre o balcão: gota de sangue, cálice e um relicário.
        AddShopPart(stand, "OfferingBlood", new SphereMesh { Radius = 0.16f, Height = 0.32f }, blood, new Vector3(-0.8f, 1.25f, 0.05f));
        AddShopPart(stand, "OfferingChalice", new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.08f, Height = 0.3f, RadialSegments = 16 }, brass, new Vector3(0.0f, 1.24f, 0.05f));
        AddShopPart(stand, "OfferingRelic", new BoxMesh { Size = new Vector3(0.32f, 0.2f, 0.22f) }, brass, new Vector3(0.8f, 1.19f, 0.05f), new Vector3(0.0f, 25.0f, 0.0f));
        stand.AddChild(new OmniLight3D
        {
            Name = "ShopGlow",
            Position = new Vector3(0.0f, 1.8f, 0.8f),
            LightColor = new Color(1.0f, 0.12f, 0.1f),
            LightEnergy = 1.1f,
            OmniRange = 3.6f
        });

        _shopTitle = new Label3D { Name = "Title", Text = Localization.Text("chamber.shop.title"), Position = new Vector3(0.0f, 2.75f, 0.0f) };
        stand.AddChild(_shopTitle);
        _shopPrompt = new Label3D { Name = "Prompt", Visible = false, Position = new Vector3(0.0f, 3.3f, 0.0f) };
        stand.AddChild(_shopPrompt);

        _shopArea = new Area3D { Name = "InteractionArea", Position = new Vector3(0.0f, 0.0f, 0.8f) };
        _shopArea.AddChild(new CollisionShape3D
        {
            Name = "Collision",
            Position = new Vector3(0.0f, 0.65f, 0.0f),
            Shape = new CylinderShape3D { Height = 2.4f, Radius = 2.2f }
        });
        stand.AddChild(_shopArea);
    }

    private static void AddShopPart(Node3D parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 rotationDegrees = default)
    {
        parent.AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            RotationDegrees = rotationDegrees
        });
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
