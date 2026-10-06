using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Mapeamentos de controle para a UI e helpers de foco (guia de UI §5.3/§5.4).
/// </summary>
public static class ControllerUiSupport
{
    private static bool _configured;

    // Garante ui_* no joypad e registra as ações novas da UI (teclado + joypad) sem editar project.godot.
    public static void EnsureMappings()
    {
        if (_configured) return;
        _configured = true;
        AddButton("ui_accept", 0);
        AddButton("ui_cancel", 1);
        AddButton("ui_up", 11);
        AddButton("ui_down", 12);
        AddButton("ui_left", 13);
        AddButton("ui_right", 14);
        AddAxis("ui_up", 1, -1.0f);
        AddAxis("ui_down", 1, 1.0f);
        AddAxis("ui_left", 0, -1.0f);
        AddAxis("ui_right", 0, 1.0f);

        // Ações novas (só lidas por telas abertas; o input do jogador está bloqueado nelas).
        AddButton("ui_alt", 3);
        AddKey("ui_alt", Key.R);
        AddButton("ui_tab_prev", 9);
        AddKey("ui_tab_prev", Key.Q);
        AddButton("ui_tab_next", 10);
        AddKey("ui_tab_next", Key.E);
        AddAxis("ui_zoom_out", 4, 1.0f, 0.3f);
        AddKey("ui_zoom_out", Key.Minus);
        AddKey("ui_zoom_out", Key.KpSubtract);
        AddAxis("ui_zoom_in", 5, 1.0f, 0.3f);
        AddKey("ui_zoom_in", Key.Equal);
        AddKey("ui_zoom_in", Key.KpAdd);
        AddAxis("ui_scroll_up", 3, -1.0f);
        AddAxis("ui_scroll_down", 3, 1.0f);
        AddAxis("ui_scroll_left", 2, -1.0f);
        AddAxis("ui_scroll_right", 2, 1.0f);
    }

    // Fallback: foca o primeiro Control focável, visível e habilitado, em ordem de árvore.
    public static void FocusFirst(Control root)
    {
        TryFocusFirst(root);
    }

    // Foco inicial explícito: usa `preferred` se puder receber foco, senão FocusFirst(fallbackRoot).
    public static bool FocusInitial(Control preferred, Control fallbackRoot)
    {
        if (CanTakeFocus(preferred))
        {
            preferred.GrabFocus();
            return true;
        }
        return TryFocusFirst(fallbackRoot);
    }

    // Retorno de foco ao fechar: volta para `previous` se ainda válido e visível, senão FocusFirst(fallbackRoot).
    public static bool RestoreFocus(Control previous, Control fallbackRoot) => FocusInitial(previous, fallbackRoot);

    // True se o controle existe, está na árvore, visível, focável e (se botão) habilitado.
    public static bool CanTakeFocus(Control control)
    {
        if (control == null || !GodotObject.IsInstanceValid(control) || !control.IsInsideTree()) return false;
        if (!IsKeyboardFocusable(control) || !control.IsVisibleInTree()) return false;
        return control is not BaseButton { Disabled: true };
    }

    // Só FocusMode All (com herança de FocusBehaviorRecursive) conta: Click não navega por d-pad e
    // Accessibility (RichTextLabel/ScrollContainer no 4.7) só aceita foco com leitor de tela ativo.
    public static bool IsKeyboardFocusable(Control control) =>
        control != null && control.GetFocusModeWithOverride() == Control.FocusModeEnum.All;

    // True se o foco atual do viewport está em `root` ou em um descendente.
    public static bool IsFocusInside(Control root)
    {
        if (root == null || !GodotObject.IsInstanceValid(root) || !root.IsInsideTree()) return false;
        var owner = root.GetViewport()?.GuiGetFocusOwner();
        return owner != null && (owner == root || root.IsAncestorOf(owner));
    }

    // Torna o controle alcançável por d-pad/Tab (ex.: "Trocar oferta" fora da grade de cards).
    public static void MakeFocusable(Control control)
    {
        if (control != null && control.FocusMode != Control.FocusModeEnum.All)
            control.FocusMode = Control.FocusModeEnum.All;
    }

    /// <summary>
    /// Vizinhança de foco explícita em grade (cards, árvore de skills). Itens em ordem de leitura;
    /// `wrap` dá a volta nas bordas; sem wrap, as bordas ficam vazias (o Godot escolhe pela geometria,
    /// o que permite sair da grade para botões vizinhos). Chame depois de adicionar os itens à árvore.
    /// </summary>
    public static void SetGridNeighbors(IList<Control> items, int columns, bool wrap)
    {
        if (items == null || items.Count == 0) return;
        if (columns < 1) columns = 1;
        var count = items.Count;
        var rows = (count + columns - 1) / columns;
        for (var i = 0; i < count; i++)
        {
            var item = items[i];
            if (item == null) continue;
            var row = i / columns;
            var col = i % columns;
            var rowStart = row * columns;
            var rowEnd = Math.Min(rowStart + columns, count) - 1;

            var left = col > 0 ? i - 1 : (wrap ? rowEnd : -1);
            var right = i < rowEnd ? i + 1 : (wrap ? rowStart : -1);
            // Linha incompleta: desce para o último item em vez de sair da grade.
            var up = row > 0 ? i - columns : (wrap ? Math.Min((rows - 1) * columns + col, count - 1) : -1);
            var down = row < rows - 1 ? Math.Min(i + columns, count - 1) : (wrap ? col : -1);
            var next = i < count - 1 ? i + 1 : (wrap ? 0 : -1);
            var previous = i > 0 ? i - 1 : (wrap ? count - 1 : -1);

            item.FocusNeighborLeft = PathTo(item, items, left, i);
            item.FocusNeighborRight = PathTo(item, items, right, i);
            item.FocusNeighborTop = PathTo(item, items, up, i);
            item.FocusNeighborBottom = PathTo(item, items, down, i);
            item.FocusNext = PathTo(item, items, next, i);
            item.FocusPrevious = PathTo(item, items, previous, i);
        }
    }

    // Lista vertical com ciclo (cima no primeiro vai ao último), como as ofertas de bênção.
    public static void SetVerticalCycle(IList<Control> items) => SetGridNeighbors(items, 1, true);

    // Fileira horizontal com ciclo (cards lado a lado).
    public static void SetHorizontalCycle(IList<Control> items)
    {
        if (items == null || items.Count == 0) return;
        SetGridNeighbors(items, items.Count, true);
    }

    // Liga `target` abaixo da fileira `row`: baixo em cada item vai a `target`; cima em `target` volta a `returnTo` (ou row[0]).
    public static void LinkBelow(IList<Control> row, Control target, Control returnTo = null)
    {
        if (row == null || target == null) return;
        MakeFocusable(target);
        foreach (var item in row)
            if (item != null) item.FocusNeighborBottom = item.GetPathTo(target);
        var back = returnTo ?? FirstNonNull(row);
        if (back != null) target.FocusNeighborTop = target.GetPathTo(back);
    }

    // Liga `target` acima da fileira `row` (simétrico de LinkBelow).
    public static void LinkAbove(IList<Control> row, Control target, Control returnTo = null)
    {
        if (row == null || target == null) return;
        MakeFocusable(target);
        foreach (var item in row)
            if (item != null) item.FocusNeighborTop = item.GetPathTo(target);
        var back = returnTo ?? FirstNonNull(row);
        if (back != null) target.FocusNeighborBottom = target.GetPathTo(back);
    }

    private static bool TryFocusFirst(Control root)
    {
        if (root == null || !GodotObject.IsInstanceValid(root)) return false;
        foreach (var child in root.FindChildren("*", "Control", true, false))
        {
            if (child is not Control control || !IsKeyboardFocusable(control) || !control.IsVisibleInTree()) continue;
            if (control is BaseButton { Disabled: true }) continue;
            control.GrabFocus();
            return true;
        }
        return false;
    }

    private static NodePath PathTo(Control from, IList<Control> items, int index, int self)
    {
        if (index < 0 || index == self || items[index] == null) return new NodePath();
        return from.GetPathTo(items[index]);
    }

    private static Control FirstNonNull(IList<Control> items)
    {
        foreach (var item in items)
            if (item != null) return item;
        return null;
    }

    private static void AddButton(string action, int buttonIndex)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var input in InputMap.ActionGetEvents(action))
            if (input is InputEventJoypadButton button && (int)button.ButtonIndex == buttonIndex) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { Device = -1, ButtonIndex = (JoyButton)buttonIndex });
    }

    private static void AddAxis(string action, int axisIndex, float value, float deadzone = 0.5f)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action, deadzone);
        foreach (var input in InputMap.ActionGetEvents(action))
            if (input is InputEventJoypadMotion motion && (int)motion.Axis == axisIndex && Mathf.IsEqualApprox(motion.AxisValue, value)) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Device = -1, Axis = (JoyAxis)axisIndex, AxisValue = value });
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach (var input in InputMap.ActionGetEvents(action))
            if (input is InputEventKey existing && (existing.Keycode == key || existing.PhysicalKeycode == key)) return;
        InputMap.ActionAddEvent(action, new InputEventKey { Device = -1, Keycode = key });
    }
}

/// <summary>
/// Pilha única de telas modais (guia de UI §5.1/§5.2). Topo da pilha = camada desenhada por cima = primeira a fechar.
/// Cada entrada guarda a camada, se pausa (árvore e/ou música), se é cancelável e o foco anterior (restaurado ao fechar).
///
/// Pausa: `GetTree().Paused` e `CallGroup("audio_manager","SetPaused", bool)` são escritos só aqui
/// (<see cref="RefreshPause"/>/<see cref="ApplyPause(bool)"/>). Árvore e música são calculadas separadamente:
/// o game over pausa a árvore sem pausar a música dele. Telas ainda não migradas entram como fonte
/// de pausa (<see cref="RegisterPauseSource"/>), de modo que Push/Pop não despausem uma tela legada.
///
/// Ligação do HUD (T3), mantendo os nomes:
///   _Ready: UiModalStack.RegisterPauseSource(_legacyPauseSource, _legacyAudioPauseSource) (delegates em campos; Unregister no _ExitTree);
///   SetModalPause(p)         → UiModalStack.ApplyPause(p);
///   RefreshModalPause()      → UiModalStack.RefreshPause();  (as fontes legadas devolvem os flags atuais)
///   CloseTopmostOverlay()    → if (UiModalStack.CancelTop()) return true; depois a cadeia legada;
///   IsGameplayInputBlocked() → UiModalStack.AnyOpen || checagens atuais.
/// </summary>
public static class UiModalStack
{
    private sealed class Entry
    {
        public Control Layer;
        public bool Pauses;
        public bool PausesAudio;
        public bool Mandatory;
        public Action OnCancel;
        public Control PreviousFocus;
        public Action OnTreeExiting;
    }

    private struct PauseSource
    {
        public Func<bool> Tree;
        public Func<bool> Audio;
    }

    private static readonly List<Entry> Entries = new();
    private static readonly List<PauseSource> PauseSources = new();
    private static bool _refreshing;

    // Há alguma camada aberta (válida, na árvore e visível)?
    public static bool AnyOpen
    {
        get
        {
            PruneAndRefresh();
            return Entries.Count > 0;
        }
    }

    // Alguma camada aberta pausa a árvore?
    public static bool AnyPausing
    {
        get
        {
            PruneAndRefresh();
            return StackPausesTree();
        }
    }

    // Camada do topo (ou null).
    public static Control Top
    {
        get
        {
            PruneAndRefresh();
            return Entries.Count > 0 ? Entries[Entries.Count - 1].Layer : null;
        }
    }

    // Pausa da árvore pedida pela pilha ou por alguma fonte legada.
    public static bool IsPauseRequested
    {
        get
        {
            Prune();
            return StackPausesTree() || AnySourceTree();
        }
    }

    // Pausa da música pedida pela pilha ou por alguma fonte legada.
    public static bool IsAudioPauseRequested
    {
        get
        {
            Prune();
            return StackPausesAudio() || AnySourceAudio();
        }
    }

    /// <summary>
    /// Abre (ou traz ao topo) uma camada: Visible + MoveToFront, guarda o foco atual para retorno,
    /// aplica o foco inicial e recalcula a pausa. A camada precisa estar na árvore.
    /// `onCancel`: o que ui_cancel faz (padrão: Pop); se não chamar Pop, a camada continua aberta (ex.: voltar a uma subpágina).
    /// `mandatory`: ui_cancel não fecha; `onCancel` (se houver) roda como aviso e o foco volta para dentro da camada.
    /// `pausesAudio`: só vale com `pauses`; false mantém a música tocando (game over).
    /// </summary>
    public static void Push(Control layer, bool pauses, Control initialFocus = null, Action onCancel = null, bool mandatory = false, bool pausesAudio = true)
    {
        if (layer == null || !GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree()) return;
        Prune();
        var focusOwner = layer.GetViewport()?.GuiGetFocusOwner();
        var index = IndexOf(layer);
        Entry entry;
        if (index >= 0)
        {
            // Já aberta: só reordena para o topo e mantém o foco de retorno original.
            entry = Entries[index];
            Entries.RemoveAt(index);
        }
        else
        {
            entry = new Entry { Layer = layer };
            entry.PreviousFocus = IsInside(layer, focusOwner) ? null : focusOwner;
            entry.OnTreeExiting = () => Forget(entry);
            layer.TreeExiting += entry.OnTreeExiting;
        }
        entry.Pauses = pauses;
        entry.PausesAudio = pauses && pausesAudio;
        entry.Mandatory = mandatory;
        entry.OnCancel = onCancel;
        Entries.Add(entry);

        layer.Visible = true;
        layer.MoveToFront();
        ControllerUiSupport.FocusInitial(initialFocus, layer);
        RefreshPause();
    }

    // Fecha a camada (não precisa ser o topo): esconde, restaura o foco (se era o topo) e recalcula a pausa.
    // Recalcula a pausa mesmo se a camada já tiver saído da pilha (ex.: escondida antes do Pop).
    public static void Pop(Control layer)
    {
        var index = IndexOf(layer);
        if (index < 0)
        {
            if (layer != null && GodotObject.IsInstanceValid(layer))
                layer.Visible = false;
            RefreshPause();
            return;
        }
        var entry = Entries[index];
        var wasTop = index == Entries.Count - 1;
        Remove(index);

        // Quem estava acima herda o foco de retorno se ele apontava para dentro da camada fechada.
        if (!wasTop && index < Entries.Count && IsInside(layer, Entries[index].PreviousFocus))
            Entries[index].PreviousFocus = entry.PreviousFocus;

        if (GodotObject.IsInstanceValid(layer))
            layer.Visible = false;

        if (wasTop)
        {
            Prune();
            var top = Entries.Count > 0 ? Entries[Entries.Count - 1].Layer : null;
            if (top == null || IsInside(top, entry.PreviousFocus))
                ControllerUiSupport.RestoreFocus(entry.PreviousFocus, top);
            else
                ControllerUiSupport.FocusFirst(top);
        }
        RefreshPause();
    }

    /// <summary>
    /// ui_cancel: age sobre o topo. Retorna false se a pilha estiver vazia (o chamador segue a cadeia legada).
    /// Modal não cancelável: não fecha, roda `onCancel` como aviso, refoca dentro dele e retorna true (input consumido).
    /// </summary>
    public static bool CancelTop()
    {
        PruneAndRefresh();
        if (Entries.Count == 0) return false;
        var top = Entries[Entries.Count - 1];
        if (top.Mandatory)
        {
            top.OnCancel?.Invoke();
            if (GodotObject.IsInstanceValid(top.Layer) && !ControllerUiSupport.IsFocusInside(top.Layer))
                ControllerUiSupport.FocusFirst(top.Layer);
            return true;
        }
        if (top.OnCancel != null)
            top.OnCancel();
        else
            Pop(top.Layer);
        return true;
    }

    public static bool IsOpen(Control layer)
    {
        PruneAndRefresh();
        return IndexOf(layer) >= 0;
    }

    public static bool IsMandatory(Control layer)
    {
        var index = IndexOf(layer);
        return index >= 0 && Entries[index].Mandatory;
    }

    // Game over / troca de cena: esconde e remove todas as camadas. Não mexe em foco nem pausa
    // (o chamador dá Push no game over ou despausa antes de trocar de cena).
    public static void Clear()
    {
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var layer = Entries[i].Layer;
            Remove(i);
            if (GodotObject.IsInstanceValid(layer))
                layer.Visible = false;
        }
    }

    // Recalcula a pausa (árvore e música, separadas) a partir da pilha e das fontes legadas e aplica.
    public static void RefreshPause()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            ApplyPause(IsPauseRequested, IsAudioPauseRequested);
        }
        finally
        {
            _refreshing = false;
        }
    }

    // Escreve a pausa direto, árvore e música juntas (SetModalPause legado).
    public static void ApplyPause(bool paused) => ApplyPause(paused, paused);

    // Escreve a pausa direto. Único ponto que deve tocar em SceneTree.Paused e no SetPaused do áudio.
    public static void ApplyPause(bool pauseTree, bool pauseAudio)
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        tree.Paused = pauseTree;
        tree.CallGroup("audio_manager", "SetPaused", pauseAudio);
    }

    /// <summary>
    /// Fonte de pausa de telas não migradas (ex.: flags do HUD). `pausesTree` pausa a árvore; `pausesAudio`
    /// pausa a música (null = mesmo valor de `pausesTree`). Guarde os delegates em campos; desregistre pelo `pausesTree`.
    /// </summary>
    public static void RegisterPauseSource(Func<bool> pausesTree, Func<bool> pausesAudio = null)
    {
        if (pausesTree == null) return;
        UnregisterPauseSource(pausesTree);
        PauseSources.Add(new PauseSource { Tree = pausesTree, Audio = pausesAudio });
    }

    public static void UnregisterPauseSource(Func<bool> pausesTree)
    {
        if (pausesTree == null) return;
        for (var i = PauseSources.Count - 1; i >= 0; i--)
            if (PauseSources[i].Tree.Equals(pausesTree)) PauseSources.RemoveAt(i);
    }

    private static bool StackPausesTree()
    {
        foreach (var entry in Entries)
            if (entry.Pauses) return true;
        return false;
    }

    private static bool StackPausesAudio()
    {
        foreach (var entry in Entries)
            if (entry.PausesAudio) return true;
        return false;
    }

    private static bool AnySourceTree()
    {
        for (var i = PauseSources.Count - 1; i >= 0; i--)
        {
            if (DropIfDead(i)) continue;
            if (PauseSources[i].Tree()) return true;
        }
        return false;
    }

    private static bool AnySourceAudio()
    {
        for (var i = PauseSources.Count - 1; i >= 0; i--)
        {
            if (DropIfDead(i)) continue;
            var source = PauseSources[i];
            if ((source.Audio ?? source.Tree)()) return true;
        }
        return false;
    }

    // Dono já liberado (troca de cena sem Unregister): descarta a fonte.
    private static bool DropIfDead(int index)
    {
        if (PauseSources[index].Tree.Target is GodotObject owner && !GodotObject.IsInstanceValid(owner))
        {
            PauseSources.RemoveAt(index);
            return true;
        }
        return false;
    }

    // Prune nos getters públicos: se saiu uma camada que pausava, recalcula a pausa
    // (evita jogo pausado sem tela quando a camada foi escondida antes do Pop).
    private static void PruneAndRefresh()
    {
        if (Prune() && !_refreshing)
            RefreshPause();
    }

    // Remove entradas cuja camada foi liberada, saiu da árvore ou foi escondida por código legado.
    // Retorna true se alguma removida pausava (árvore ou música).
    private static bool Prune()
    {
        var removedPausing = false;
        for (var i = Entries.Count - 1; i >= 0; i--)
        {
            var entry = Entries[i];
            var layer = entry.Layer;
            if (GodotObject.IsInstanceValid(layer) && layer.IsInsideTree() && layer.Visible) continue;
            removedPausing |= entry.Pauses || entry.PausesAudio;
            Remove(i);
        }
        return removedPausing;
    }

    // Camada saindo da árvore: só esquece a entrada (sem foco/pausa, para não vazar entre cenas).
    private static void Forget(Entry entry)
    {
        var index = Entries.IndexOf(entry);
        if (index >= 0) Remove(index);
    }

    private static void Remove(int index)
    {
        var entry = Entries[index];
        Entries.RemoveAt(index);
        if (entry.OnTreeExiting != null && GodotObject.IsInstanceValid(entry.Layer))
            entry.Layer.TreeExiting -= entry.OnTreeExiting;
    }

    private static int IndexOf(Control layer)
    {
        if (layer == null) return -1;
        for (var i = 0; i < Entries.Count; i++)
            if (Entries[i].Layer == layer) return i;
        return -1;
    }

    private static bool IsInside(Control root, Control control)
    {
        if (root == null || control == null || !GodotObject.IsInstanceValid(root) || !GodotObject.IsInstanceValid(control)) return false;
        return control == root || root.IsAncestorOf(control);
    }
}
