using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Autoload do dialogo: selecao, gatilhos agendados, barks e o roteiro corrente (CurrentPlayback).
/// A UI (HUD) abre via CallGroup("hud", "OpenDialogue" / "ShowDialogueBark" / "HideDialogueBark"); sem HUD
/// (ou sem esses metodos) nada abre e nada e marcado como visto.
/// </summary>
public partial class DialogueManager : Node
{
    [Signal] public delegate void ConversationStartedEventHandler(string conversationId);
    [Signal] public delegate void ConversationEndedEventHandler(string conversationId, string choiceId);
    [Signal] public delegate void BarkShownEventHandler(string conversationId);

    private const double BarkGlobalCooldownSeconds = 18.0;
    private const int MaxBarksPerRoom = 2;
    private const double BarkRoomEnterDelay = 1.2;
    private const double BarkRoomClearedDelay = 0.8;
    private const double BossPreDelay = 0.6;
    private const double BossPostDelay = 1.6;
    private const float LowHealthRatio = 0.3f;
    private const float LowHealthRearmRatio = 0.5f;
    private const double RetryInterval = 0.5;
    private const double RetryWindow = 10.0;
    private const double ProbeInterval = 0.5;

    private enum Scheduled { BarkRoomEnter, BarkRoomCleared, BossPre, BossPost }

    private sealed class Pending
    {
        public Scheduled Kind;
        public double Due;
        public double Deadline;
    }

    private sealed record BarkLine(string SpeakerId, string SpeakerName, string Text, string Emotion, float Seconds);

    private readonly RandomNumberGenerator _rng = new();
    private readonly List<Pending> _pending = new();
    private readonly Queue<BarkLine> _barkQueue = new();
    private double _clock;
    private double _lastBark = -1000.0;
    private double _barkNextAt;
    private double _nextProbe;
    private int _barksThisRoom;
    private bool _lowHealthArmed = true;
    private ulong _openedAtMsec;
    private DialogueDefinition _peeked;
    private string _peekSpeaker;
    private int _peekPlays;
    private int _peekSeen;
    private RunEndInfo _peekLastRun;

    public static DialogueManager Instance { get; private set; }
    /// <summary>null = nenhuma conversa modal aberta.</summary>
    public DialoguePlayback CurrentPlayback { get; private set; }
    public bool IsConversationOpen => CurrentPlayback != null;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _rng.Randomize();
        _ = DialogueCatalog.All; // forca a carga (erros aparecem no boot)
        if (OS.IsDebugBuild() && Array.IndexOf(OS.GetCmdlineUserArgs(), "--dlg-selftest") >= 0)
            CallDeferred(nameof(RunSelfTest));
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public void SetRandomSeed(ulong seed) => _rng.Seed = seed;

    public override void _Process(double delta)
    {
        // Relogio proprio: nao avanca com a arvore pausada (conversa modal, pausa, oferta).
        var tree = GetTree();
        if (CurrentPlayback != null && !UiModalStack.AnyOpen && Time.GetTicksMsec() - _openedAtMsec > 1500)
            EndPlayback(CurrentPlayback); // a UI nao assumiu a conversa: nao deixa travada
        if (tree == null || tree.Paused) return;
        _clock += delta;

        if (_barkQueue.Count > 0 && _clock >= _barkNextAt)
            ShowNextBarkLine();

        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            var item = _pending[i];
            if (_clock < item.Due) continue;
            _pending.RemoveAt(i);
            RunScheduled(item);
        }

        if (_clock >= _nextProbe)
        {
            _nextProbe = _clock + ProbeInterval;
            ProbeLowHealth();
        }
    }

    // ---------- NPC ----------

    /// <summary>Conversa que a NPC diria agora, sem marcar nada. null = nada a dizer.</summary>
    public DialogueDefinition PeekNpcConversation(string speakerId)
    {
        // Escolha congelada: o Peek de proximidade e o TryTalkTo usam a mesma conversa enquanto ela seguir elegivel.
        if (_peeked != null && _peekSpeaker == speakerId && _peekPlays == DialogueProgress.GetPlays("npc")
            && ReferenceEquals(_peekLastRun, DialogueProgress.LastRun) && DialogueProgress.GetSeen(_peeked.Id) == _peekSeen
            && DialogueConditions.EvaluateAll(_peeked.Requirements, BuildContext()))
            return _peeked;
        var result = SelectForNpc(DialogueCatalog.All, speakerId, _rng);
        _peeked = result;
        _peekSpeaker = speakerId;
        _peekPlays = DialogueProgress.GetPlays("npc");
        _peekLastRun = DialogueProgress.LastRun;
        _peekSeen = result != null ? DialogueProgress.GetSeen(result.Id) : 0;
        return result;
    }

    /// <summary>Existe conversa essential/specific nao vista e elegivel (marcador "!").</summary>
    public bool HasPriorityConversation(string speakerId)
    {
        var context = BuildContext();
        foreach (var definition in NpcPool(DialogueCatalog.All, speakerId))
        {
            if (definition.PriorityClass == DialoguePriorityClass.Evergreen || DialogueProgress.IsSeen(definition.Id)) continue;
            if (DialogueConditions.EvaluateAll(definition.Requirements, context)) return true;
        }
        return false;
    }

    public bool TryTalkTo(string speakerId)
    {
        if (!CanOpenModal()) return false;
        var definition = PeekNpcConversation(speakerId);
        return definition != null && StartConversation(definition);
    }

    /// <summary>Selecao do NPC (4.6). Publico-estatico para o autoteste usar listas em memoria.</summary>
    public static DialogueDefinition SelectForNpc(IEnumerable<DialogueDefinition> all, string speakerId, RandomNumberGenerator rng)
        => DialogueSelector.Select(NpcPool(all, speakerId), BuildContextStatic(), rng, true);

    private static IEnumerable<DialogueDefinition> NpcPool(IEnumerable<DialogueDefinition> all, string speakerId)
    {
        var last = DialogueProgress.LastRun;
        foreach (var definition in all)
        {
            if (definition.SpeakerId != speakerId) continue;
            var ok = definition.Category == DialogueCategory.Hub
                || (last != null && definition.Category == (last.Outcome == RunOutcome.Died ? DialogueCategory.Death : DialogueCategory.Return));
            if (ok) yield return definition;
        }
    }

    // ---------- Gatilhos de gameplay ----------

    public void Notify(DialogueEvent gameEvent, int argument = 0)
    {
        switch (gameEvent)
        {
            case DialogueEvent.RoomEntered:
                _barksThisRoom = 0;
                _pending.Clear();
                _lowHealthArmed = true;
                _barkQueue.Clear();
                GetTree()?.CallGroup("hud", "HideDialogueBark");   // nao renomear
                if (!RunState.IsRunActive) return;
                if (RunState.IsBossRoom) Schedule(Scheduled.BossPre, BossPreDelay);
                else Schedule(Scheduled.BarkRoomEnter, BarkRoomEnterDelay);
                break;
            case DialogueEvent.RoomCleared:
                if (RunState.IsRunActive) Schedule(Scheduled.BarkRoomCleared, BarkRoomClearedDelay);
                break;
            case DialogueEvent.BossDefeated:
                Schedule(Scheduled.BossPost, BossPostDelay);
                break;
            case DialogueEvent.BossPhaseChanged:
                TryBark(DialogueTrigger.BossPhase);
                break;
        }
    }

    private void Schedule(Scheduled kind, double delay)
        => _pending.Add(new Pending { Kind = kind, Due = _clock + delay, Deadline = _clock + delay + RetryWindow });

    private void RunScheduled(Pending item)
    {
        switch (item.Kind)
        {
            case Scheduled.BarkRoomEnter: TryBark(DialogueTrigger.RoomEnter); break;
            case Scheduled.BarkRoomCleared: TryBark(DialogueTrigger.RoomCleared); break;
            case Scheduled.BossPre: RunBossConversation(item, DialogueCategory.BossPre, true); break;
            case Scheduled.BossPost: RunBossConversation(item, DialogueCategory.BossPost, false); break;
        }
    }

    private void RunBossConversation(Pending item, DialogueCategory category, bool requireActiveRun)
    {
        if (requireActiveRun && !RunState.IsRunActive) return;
        var definition = DialogueSelector.Select(DialogueCatalog.Where(category), BuildContext(), _rng, true);   // fallback: boss generico repetivel nunca emudece
        if (definition == null) return;
        if (StartConversation(definition)) return;
        // Outra janela aberta (oferta, ficha): tenta de novo ate o prazo.
        if (_clock + RetryInterval <= item.Deadline)
        {
            item.Due = _clock + RetryInterval;
            _pending.Add(item);
        }
    }

    // ---------- Execucao ----------

    /// <summary>Forcado: ignora requisitos e cooldown.</summary>
    public bool TryStartConversation(string conversationId)
        => DialogueCatalog.TryGet(conversationId, out var definition) && CanOpenModal() && StartConversation(definition);

    public DialoguePlayback CreatePlayback(DialogueDefinition definition, bool preview)
    {
        var playback = new DialoguePlayback(definition, preview, flag => DialogueProgress.SetFlag(flag));
        if (preview) return playback;
        DialogueProgress.MarkSeen(definition);
        foreach (var flag in definition.SetsFlags) DialogueProgress.SetFlag(flag);
        CurrentPlayback = playback;
        _openedAtMsec = Time.GetTicksMsec();
        EmitSignal(SignalName.ConversationStarted, definition.Id);
        return playback;
    }

    /// <summary>O HUD chama ao fechar a janela. Idempotente.</summary>
    public void EndPlayback(DialoguePlayback playback)
    {
        if (playback == null || playback != CurrentPlayback) return;
        CurrentPlayback = null;
        EmitSignal(SignalName.ConversationEnded, playback.Definition.Id, playback.ChosenChoiceId ?? "");
    }

    private bool StartConversation(DialogueDefinition definition)
    {
        if (!CanOpenModal()) return false;
        CreatePlayback(definition, false);
        _barkQueue.Clear();
        GetTree().CallGroup("hud", "HideDialogueBark");   // nao renomear (contrato com HUD.Dialogue.cs)
        GetTree().CallGroup("hud", "OpenDialogue", definition.Id);   // nao renomear
        return true;
    }

    // Recusa sem marcar nada: conversa aberta, modal aberta, sem HUD com OpenDialogue ou jogador morto.
    private bool CanOpenModal()
    {
        if (CurrentPlayback != null || UiModalStack.AnyOpen) return false;
        var tree = GetTree();
        if (tree == null || tree.Paused) return false;
        var hud = tree.GetFirstNodeInGroup("hud");
        if (hud == null || !hud.HasMethod("OpenDialogue")) return false;
        return !PlayerIsDead(tree);
    }

    private static bool PlayerIsDead(SceneTree tree)
        => tree.GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health") is { IsDead: true };

    // ---------- Barks ----------

    private void TryBark(DialogueTrigger trigger)
    {
        var tree = GetTree();
        if (tree == null || CurrentPlayback != null || UiModalStack.AnyOpen || !RunState.IsRunActive) return;
        if (!BarkAllowed(_clock, _lastBark, _barksThisRoom)) return;
        var hud = tree.GetFirstNodeInGroup("hud");
        if (hud == null || !hud.HasMethod("ShowDialogueBark")) return;
        var health = tree.GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
        if (health == null || health.IsDead) return;

        // Relogio por oportunidade: avanca mesmo sem bark escolhido, para o cooldown nunca travar.
        DialogueProgress.AdvancePlays("bark");
        var definition = DialogueSelector.Select(DialogueCatalog.Where(DialogueCategory.Bark, null, trigger), BuildContext(), _rng, false);
        if (definition == null) return;

        DialogueProgress.MarkSeen(definition);
        foreach (var flag in definition.SetsFlags) DialogueProgress.SetFlag(flag);
        _lastBark = _clock;
        _barksThisRoom++;
        _barkQueue.Clear();
        foreach (var line in definition.Lines)
        {
            var speakerId = string.IsNullOrEmpty(line.SpeakerId) ? definition.SpeakerId : line.SpeakerId;
            var text = FormatText(line.TextKey);
            var seconds = (float)Math.Clamp(1.8 + 0.045 * text.Length, 2.5, 6.0);
            var name = DialogueCatalog.TryGetSpeaker(speakerId, out var speaker) ? speaker.DisplayName : speakerId;
            _barkQueue.Enqueue(new BarkLine(speakerId, name, text, line.Emotion ?? "", seconds));
        }
        _barkNextAt = _clock;
        ShowNextBarkLine();
        EmitSignal(SignalName.BarkShown, definition.Id);
    }

    /// <summary>Regras globais de bark (4.7): 18 s entre barks e no maximo 2 por sala.</summary>
    public static bool BarkAllowed(double now, double lastBark, int barksThisRoom)
        => now - lastBark >= BarkGlobalCooldownSeconds && barksThisRoom < MaxBarksPerRoom;

    private void ShowNextBarkLine()
    {
        if (_barkQueue.Count == 0) return;
        if (!RunState.IsRunActive) { _barkQueue.Clear(); return; }
        var line = _barkQueue.Dequeue();
        GetTree().CallGroup("hud", "ShowDialogueBark", line.SpeakerId, line.SpeakerName, line.Text, line.Emotion, line.Seconds);   // nao renomear
        _barkNextAt = _clock + line.Seconds;
    }

    private void ProbeLowHealth()
    {
        if (!RunState.IsRunActive) return;
        var health = GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
        if (health == null || health.MaxHealth <= 0.0f) return;
        var ratio = health.CurrentHealth / health.MaxHealth;
        if (_lowHealthArmed && ratio < LowHealthRatio && !health.IsDead)
        {
            _lowHealthArmed = false;
            TryBark(DialogueTrigger.LowHealth);
        }
        else if (ratio > LowHealthRearmRatio)
            _lowHealthArmed = true;
    }

    // ---------- Contexto, textos e retratos ----------

    private DialogueContext BuildContext() => BuildContextStatic();

    private static DialogueContext BuildContextStatic()
    {
        var ratio = -1.0f;
        var tree = Instance?.GetTree();
        var health = tree?.GetFirstNodeInGroup("player")?.GetNodeOrNull<Health>("Health");
        if (health != null && health.MaxHealth > 0.0f) ratio = health.CurrentHealth / health.MaxHealth;
        return new DialogueContext(ratio);
    }

    /// <summary>Texto da chave com {0} runs, {1} mortes, {2} mapa, {3} boss do mapa de contexto (3.3).</summary>
    public static string FormatText(string textKey)
    {
        var map = RunState.IsRunActive ? RunState.CurrentMapIndex : (DialogueProgress.LastRun?.MapIndex ?? 0);
        return Localization.TextFormat(textKey, DialogueProgress.RunsStarted, DialogueProgress.Deaths,
            CampaignCatalog.Get(map).Name, BossCatalog.Get(map).DisplayName);
    }

    /// <summary>Variante pedida ou "default"; campos ausentes ja vem herdados do catalogo. null se o falante nao existe.</summary>
    public static PortraitSpec ResolvePortrait(string speakerId, string variant)
    {
        if (!DialogueCatalog.TryGetSpeaker(speakerId, out var speaker)) return null;
        if (!string.IsNullOrEmpty(variant) && variant != "neutral" && speaker.Portraits.TryGetValue(variant, out var spec)) return spec;
        return speaker.Portraits.TryGetValue("default", out var fallback) ? fallback : null;
    }
}
