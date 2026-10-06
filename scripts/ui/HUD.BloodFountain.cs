using Godot;
using System;
using System.Collections.Generic;

// Fonte de Sangue: árvores Bestialis/Humanitas (guia de UI §2.1, §3.6, §5, §8.14).
// Janela Full na UiModalStack (não pausa, só bloqueia input), nós em StyleBox por estado, detalhes seguindo foco/hover
// num DetailPanel à direita, zoom/pan por mouse, teclado e controle. Regras de compra (custo, rank, pré-requisitos,
// exclusividade) não mudam.
public partial class HUD
{
	// Layout lógico do canvas (antes do zoom), compacto para as 6 linhas caberem na altura entre 85% e 100% de zoom
	// (revisão T6: com o detalhe embaixo só ~2,5 linhas apareciam a 100%). Nome em 1 linha com reticências (o nome
	// completo fica no painel de detalhes); nome e custo em Body 17 para ficarem ≥14 px na tela a 85%.
	private const float SkillColumnWidth = 180.0f;
	private const float SkillNodeWidth = 96.0f;
	private const float SkillNodeHeight = 52.0f;
	private const float SkillCostHeight = 20.0f;
	private const float SkillNameWidth = 164.0f;
	private const float SkillNameHeight = 24.0f;
	private const float SkillBlockSpacing = 2.0f;
	private const float SkillRowPitch = 110.0f;
	private const float SkillTreePadding = UiMetrics.S2;
	private const float SkillTreeHeaderHeight = 32.0f;
	private const float SkillTreeGap = UiMetrics.S6;
	private const float SkillLaneStep = 6.0f;
	private const float SkillDetailWidth = 400.0f;
	private const float SkillZoomMin = 0.4f;
	private const float SkillZoomMax = 1.5f;
	// Zoom inicial: "caber na altura", limitado a 85–100%.
	private const float SkillZoomFitMin = 0.85f;
	private const float SkillZoomFitMax = 1.0f;
	private const float SkillZoomStep = 0.1f;
	private const float SkillZoomSpeed = 1.2f;   // por segundo com gatilho/tecla segurados
	private const float SkillPanSpeed = 900.0f;  // px/s com o analógico direito no máximo
	private const float SkillViewMargin = UiMetrics.S5;
	private const int SkillPreviewCoins = 700;

	// Ações lidas por quadro (sem converter string → StringName a cada chamada).
	private static readonly StringName SkillPanLeftAction = "ui_scroll_left";
	private static readonly StringName SkillPanRightAction = "ui_scroll_right";
	private static readonly StringName SkillPanUpAction = "ui_scroll_up";
	private static readonly StringName SkillPanDownAction = "ui_scroll_down";
	private static readonly StringName SkillZoomInAction = "ui_zoom_in";
	private static readonly StringName SkillZoomOutAction = "ui_zoom_out";

	private static float SkillBlockHeight => SkillNodeHeight + SkillBlockSpacing + SkillCostHeight + SkillBlockSpacing + SkillNameHeight;

	private sealed class SkillNodeView
	{
		public string Id;
		public Vector2 Origin;
		public int Column;
		public int Row;
		public int TreeIndex;
		public Button Button;
		public Label Sigla;
		public HBoxContainer BadgeSlot;
		public Control Lock;
		public Control Strike;
		public Control Dashed;
		public HBoxContainer CostRow;
		public Label CostLabel;
		public Label NameLabel;
		public SkillNodeState State;
		public Vector2 Center => Origin + new Vector2(SkillNodeWidth, SkillNodeHeight) * 0.5f;
	}

	private sealed class SkillLinkPath
	{
		public string FromId;
		public Vector2[] Points;
	}

	private UiWindow _bloodFountainWindow;
	private Control _skillTreeViewport;
	private Control _skillLinkLayer;
	private UiDetailPanel _skillDetailPanel;
	private Label _bloodFountainCoinsLabel;
	private UiHint _bloodFountainZoomHint;
	private readonly Dictionary<string, SkillNodeView> _skillNodeViews = new();
	private readonly List<SkillNodeView> _skillNodeOrder = new();
	private readonly List<SkillLinkPath> _skillLinks = new();
	private Vector2 _skillTreeContentSize;
	private string _focusedSkillId;
	private string _hoveredSkillId;
	// Preview (--ui-preview=blood_fountain|skill_popup): compra e cheat só simulam, nada é gravado.
	private bool _bloodFountainPreviewMode;
	// --ui-zoom= do preview (≤ 0 = zoom inicial automático).
	private float _bloodFountainPreviewZoom = -1.0f;
	// Zoom inicial pendente até a área da árvore ter tamanho (primeiro layout após abrir).
	private bool _skillTreeFitPending;

	private void BuildBloodFountainOverlay(Control root)
	{
		_bloodFountainWindow = UiComponents.Window(root, "BloodFountainOverlay", WindowSize.Full, "Fonte de Sangue");
		_bloodFountainOverlay = _bloodFountainWindow.Root;
		_bloodFountainOverlay.Visible = false;
		_bloodFountainWindow.SetStatus(null);

		// Cabeçalho: moedas à direita (CoinGlyph + número).
		var header = _bloodFountainWindow.HeaderRight;
		header.AddChild(new Label
		{
			Text = "Moedas de Sangue",
			ThemeTypeVariation = UiVariation.SecondaryLabel,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		});
		header.AddChild(UiComponents.CoinGlyph(24));
		_bloodFountainCoinsLabel = new Label
		{
			Name = "BloodFountainCoins",
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		header.AddChild(_bloodFountainCoinsLabel);

		// Corpo: árvore à esquerda (ocupa o resto) e coluna de detalhes à direita (como na ficha).
		var split = new HBoxContainer
		{
			Name = "BloodFountainSplit",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		split.AddThemeConstantOverride("separation", UiMetrics.S5);
		_bloodFountainWindow.Body.AddChild(split);

		// Área da árvore: recorta o canvas; zoom/pan mudam só Scale/Position do canvas.
		_skillTreeViewport = new Control
		{
			Name = "SkillTreeViewport",
			ClipContents = true,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Stop,
			FocusMode = Control.FocusModeEnum.None
		};
		_skillTreeViewport.GuiInput += OnSkillTreeViewportInput;
		_skillTreeViewport.Resized += OnSkillTreeViewportResized;
		split.AddChild(_skillTreeViewport);

		_skillTreeCanvas = new Control { Name = "SkillTreeCanvas", MouseFilter = Control.MouseFilterEnum.Ignore };
		_skillTreeViewport.AddChild(_skillTreeCanvas);
		BuildSkillTrees();

		// Coluna direita: painel de detalhes (segue foco e hover) + ferramentas de dev (só debug).
		var detailColumn = new VBoxContainer
		{
			Name = "SkillDetailColumn",
			CustomMinimumSize = new Vector2(SkillDetailWidth, 0.0f),
			SizeFlagsVertical = Control.SizeFlags.ExpandFill
		};
		detailColumn.AddThemeConstantOverride("separation", UiMetrics.S3);
		split.AddChild(detailColumn);
		_skillDetailPanel = UiComponents.DetailPanel(DetailPanelDock.Right);
		_skillDetailPanel.Root.Name = "SkillDetail";
		_skillDetailPanel.Root.CustomMinimumSize = new Vector2(SkillDetailWidth, 0.0f);
		// Ícone (réplica do nó) em 48: sobra largura para o título em 1 linha ("Presas que Bebem o Sangue").
		_skillDetailPanel.IconSlot.CustomMinimumSize = new Vector2(UiMetrics.IconM, UiMetrics.IconM);
		detailColumn.AddChild(_skillDetailPanel.Root);
		if (OS.IsDebugBuild())
		{
			// Ferramenta de teste: fora do build de debug ela permitiria farmar moedas (§7).
			var dev = UiComponents.DevSection();
			_bloodCoinCheatButton = UiComponents.MenuButton("+500 moedas", AddCheatBloodCoins, ButtonVariant.Secondary, ButtonSize.Small);
			_bloodCoinCheatButton.Name = "BloodCoinCheatButton";
			dev.Body.AddChild(_bloodCoinCheatButton);
			detailColumn.AddChild(dev.Root);
		}

		var footer = _bloodFountainWindow.Footer;
		footer.Add(UiAction.Cancel, UiComponents.TextOr("ui.common.close", "Fechar"), CloseBloodFountain, left: true);
		footer.Add(UiAction.Scroll, "Mover");
		footer.Add(UiAction.ZoomOut, "", () => AdjustBloodFountainZoom(-SkillZoomStep));
		_bloodFountainZoomHint = footer.Add(UiAction.ZoomIn, "", () => AdjustBloodFountainZoom(SkillZoomStep));
		footer.Add(UiAction.Accept, "Comprar", BuyFocusedSkill);

		LinkSkillNodeNeighbors();
		ApplySkillTreeTransform();
	}

	// ---------- Montagem das árvores ----------

	private void BuildSkillTrees()
	{
		_skillNodeViews.Clear();
		_skillNodeOrder.Clear();
		_skillLinks.Clear();

		var trees = new[] { SkillTree.Bestialis, SkillTree.Humanitas };
		var columns = 3;
		var rows = 1;
		foreach (var definition in SkillCatalog.All)
		{
			columns = Mathf.Max(columns, definition.GridColumn + 1);
			rows = Mathf.Max(rows, definition.GridRow + 1);
		}

		var treeWidth = columns * SkillColumnWidth + SkillTreePadding * 2.0f;
		var treeHeight = SkillTreeHeaderHeight + SkillTreePadding + (rows - 1) * SkillRowPitch + SkillBlockHeight + SkillTreePadding;
		_skillTreeContentSize = new Vector2(trees.Length * treeWidth + (trees.Length - 1) * SkillTreeGap, treeHeight);
		_skillTreeCanvas.Size = _skillTreeContentSize;

		// Ordem de desenho: painéis das árvores → ligações → nós.
		for (var i = 0; i < trees.Length; i++)
			BuildSkillTreePanel(trees[i], i * (treeWidth + SkillTreeGap), treeWidth, treeHeight);

		_skillLinkLayer = new Control { Name = "SkillLinks", MouseFilter = Control.MouseFilterEnum.Ignore, Size = _skillTreeContentSize };
		_skillLinkLayer.Draw += DrawSkillLinks;
		_skillTreeCanvas.AddChild(_skillLinkLayer);

		for (var i = 0; i < trees.Length; i++)
		{
			var treeLeft = i * (treeWidth + SkillTreeGap);
			var skills = new List<SkillDefinition>(SkillCatalog.GetTree(trees[i]));
			// Ordem de leitura (linha, coluna): base do Tab/FocusNext e da busca do foco inicial.
			skills.Sort((a, b) => a.GridRow != b.GridRow ? a.GridRow.CompareTo(b.GridRow) : a.GridColumn.CompareTo(b.GridColumn));
			foreach (var definition in skills)
				CreateSkillNode(definition, i, GetSkillNodeOrigin(treeLeft, definition));
		}

		BuildSkillLinks();
	}

	private void BuildSkillTreePanel(SkillTree tree, float left, float width, float height)
	{
		var panel = new Panel
		{
			Name = $"{tree}TreePanel",
			Position = new Vector2(left, 0.0f),
			Size = new Vector2(width, height),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		panel.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.Border, UiMetrics.BorderThin, 0, 0, UiMetrics.RadiusCard));
		_skillTreeCanvas.AddChild(panel);

		// §8.14: cabeçalho Bestialis em DecorCrimson com texto TextPrimary; Humanitas em Gold.
		var bestialis = tree == SkillTree.Bestialis;
		var headerStyle = bestialis
			? UIThemeFactory.ControlBox(UiPalette.DecorCrimson, UiPalette.DecorCrimson, UiMetrics.BorderThin, UiMetrics.S3, UiMetrics.S1, UiMetrics.RadiusCard)
			: UIThemeFactory.ControlBox(UiPalette.PanelRaised, UiPalette.Gold, UiMetrics.BorderThin, UiMetrics.S3, UiMetrics.S1, UiMetrics.RadiusCard);
		if (!bestialis)
			headerStyle.BorderWidthBottom = 3;
		var header = new PanelContainer
		{
			Name = $"{tree}TreeHeader",
			Position = Vector2.Zero,
			Size = new Vector2(width, SkillTreeHeaderHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		header.AddThemeStyleboxOverride("panel", headerStyle);
		var title = new Label
		{
			Text = tree.ToString(),
			ThemeTypeVariation = UiVariation.SectionLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		title.AddThemeColorOverride("font_color", bestialis ? UiPalette.TextPrimary : UiPalette.Gold);
		header.AddChild(title);
		panel.AddChild(header);
	}

	private static Vector2 GetSkillNodeOrigin(float treeLeft, SkillDefinition definition)
	{
		var x = treeLeft + SkillTreePadding + definition.GridColumn * SkillColumnWidth + (SkillColumnWidth - SkillNodeWidth) * 0.5f;
		var y = SkillTreeHeaderHeight + SkillTreePadding + definition.GridRow * SkillRowPitch;
		return new Vector2(x, y);
	}

	private void CreateSkillNode(SkillDefinition definition, int treeIndex, Vector2 origin)
	{
		var id = definition.Id;
		var safeName = id.Replace(".", "_");
		var view = new SkillNodeView
		{
			Id = id,
			Origin = origin,
			Column = definition.GridColumn,
			Row = definition.GridRow,
			TreeIndex = treeIndex
		};

		view.Button = new Button
		{
			Name = $"{safeName}Node",
			Position = origin,
			CustomMinimumSize = new Vector2(SkillNodeWidth, SkillNodeHeight),
			Size = new Vector2(SkillNodeWidth, SkillNodeHeight),
			FocusMode = Control.FocusModeEnum.All,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		_skillTreeCanvas.AddChild(view.Button);

		view.Sigla = new Label
		{
			Text = definition.IconText,
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		view.Sigla.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		view.Sigla.OffsetBottom = -20.0f;
		view.Button.AddChild(view.Sigla);

		view.BadgeSlot = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		view.BadgeSlot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
		view.BadgeSlot.OffsetTop = -22.0f;
		view.BadgeSlot.OffsetBottom = -2.0f;
		view.Button.AddChild(view.BadgeSlot);

		view.Lock = UiComponents.LockGlyph(18);
		view.Lock.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
		view.Lock.OffsetLeft = -22.0f;
		view.Lock.OffsetTop = 4.0f;
		view.Lock.OffsetRight = -4.0f;
		view.Lock.OffsetBottom = 22.0f;
		view.Button.AddChild(view.Lock);

		view.Strike = UiComponents.StrikeOverlay();
		view.Button.AddChild(view.Strike);
		view.Dashed = UiComponents.DashedOutline(new Color(UiPalette.TextSecondary, 0.5f));
		view.Button.AddChild(view.Dashed);

		var labelLeft = origin.X + SkillNodeWidth * 0.5f - SkillNameWidth * 0.5f;
		var costTop = origin.Y + SkillNodeHeight + SkillBlockSpacing;
		view.CostRow = new HBoxContainer
		{
			Name = $"{safeName}Cost",
			Alignment = BoxContainer.AlignmentMode.Center,
			Position = new Vector2(labelLeft, costTop),
			Size = new Vector2(SkillNameWidth, SkillCostHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		view.CostRow.AddThemeConstantOverride("separation", UiMetrics.S1);
		view.CostRow.AddChild(UiComponents.CoinGlyph(16));
		view.CostLabel = new Label { ThemeTypeVariation = UiVariation.SecondaryLabel, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		view.CostLabel.AddThemeFontSizeOverride("font_size", UiFont.Body);
		view.CostRow.AddChild(view.CostLabel);
		_skillTreeCanvas.AddChild(view.CostRow);

		// 1 linha com reticências; o nome completo aparece no painel de detalhes.
		view.NameLabel = new Label
		{
			Name = $"{safeName}Label",
			Text = definition.DisplayName,
			ThemeTypeVariation = UiVariation.BodyLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Top,
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Position = new Vector2(labelLeft, costTop + SkillCostHeight + SkillBlockSpacing),
			Size = new Vector2(SkillNameWidth, SkillNameHeight),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_skillTreeCanvas.AddChild(view.NameLabel);

		view.Button.Pressed += () => BuySkill(id);
		view.Button.FocusEntered += () => OnSkillNodeFocused(id);
		view.Button.MouseEntered += () =>
		{
			_hoveredSkillId = id;
			ShowSkillDetail(id);
		};
		view.Button.MouseExited += () =>
		{
			if (_hoveredSkillId != id)
				return;
			_hoveredSkillId = null;
			ShowSkillDetail(_focusedSkillId);
		};
		view.Button.GuiInput += input => OnSkillNodeInput(view, input);

		_skillNodeViews[id] = view;
		_skillNodeOrder.Add(view);
	}

	// ---------- Ligações (pré-requisitos) ----------

	private void BuildSkillLinks()
	{
		var laneCount = new Dictionary<string, int>();
		foreach (var child in _skillNodeOrder)
		{
			if (!SkillCatalog.TryGet(child.Id, out var definition))
				continue;
			foreach (var prerequisiteId in definition.Prerequisites)
			{
				// Pré-requisito de outra árvore não tem ligação desenhada (só aparece no detalhe).
				if (!_skillNodeViews.TryGetValue(prerequisiteId, out var parent) || parent.TreeIndex != child.TreeIndex)
					continue;
				_skillLinks.Add(new SkillLinkPath { FromId = prerequisiteId, Points = RouteSkillLink(parent, child, laneCount) });
			}
		}
	}

	// Rota ortogonal do bloco do pai (nó + custo + nome) ao topo do filho, sem atravessar outros nós:
	// desce pela coluna do pai ou do filho quando ela está livre; senão usa uma faixa entre colunas.
	private Vector2[] RouteSkillLink(SkillNodeView parent, SkillNodeView child, Dictionary<string, int> laneCount)
	{
		var start = new Vector2(parent.Center.X, parent.Origin.Y + SkillBlockHeight);
		var end = new Vector2(child.Center.X, child.Origin.Y);
		if (child.Row <= parent.Row)
			return new[] { parent.Center, child.Center };

		var gap = SkillRowPitch - SkillBlockHeight;
		var belowParent = start.Y + gap * 0.5f;
		var aboveChild = end.Y - gap * 0.5f;

		if (parent.Column != child.Column)
		{
			if (IsSkillColumnFree(parent.TreeIndex, parent.Column, parent.Row + 1, child.Row - 1))
				return new[] { start, new Vector2(start.X, aboveChild), new Vector2(end.X, aboveChild), end };
			if (IsSkillColumnFree(parent.TreeIndex, child.Column, parent.Row + 1, child.Row - 1))
				return new[] { start, new Vector2(start.X, belowParent), new Vector2(end.X, belowParent), end };
		}
		else if (child.Row == parent.Row + 1 || IsSkillColumnFree(parent.TreeIndex, parent.Column, parent.Row + 1, child.Row - 1))
		{
			return new[] { start, end };
		}

		// Faixa vertical na divisa entre colunas (lado com menos nós no trecho; empate alterna).
		var column = child.Column;
		var rightFree = IsSkillColumnFree(child.TreeIndex, column + 1, parent.Row, child.Row);
		var leftFree = IsSkillColumnFree(child.TreeIndex, column - 1, parent.Row, child.Row);
		var rightKey = $"{child.TreeIndex}:{column}:R";
		var leftKey = $"{child.TreeIndex}:{column}:L";
		laneCount.TryGetValue(rightKey, out var rightUsed);
		laneCount.TryGetValue(leftKey, out var leftUsed);
		var useRight = rightFree != leftFree ? rightFree : rightUsed <= leftUsed;
		var key = useRight ? rightKey : leftKey;
		var used = useRight ? rightUsed : leftUsed;
		laneCount[key] = used + 1;
		var boundary = child.Center.X + (useRight ? 1.0f : -1.0f) * SkillColumnWidth * 0.5f;
		var laneX = boundary + (useRight ? 1.0f : -1.0f) * used * SkillLaneStep;
		return new[]
		{
			start,
			new Vector2(start.X, belowParent),
			new Vector2(laneX, belowParent),
			new Vector2(laneX, aboveChild),
			new Vector2(end.X, aboveChild),
			end
		};
	}

	private bool IsSkillColumnFree(int treeIndex, int column, int fromRow, int toRow)
	{
		if (fromRow > toRow)
			return true;
		foreach (var view in _skillNodeOrder)
			if (view.TreeIndex == treeIndex && view.Column == column && view.Row >= fromRow && view.Row <= toRow)
				return false;
		return true;
	}

	private void DrawSkillLinks()
	{
		if (_skillLinkLayer == null)
			return;
		// Comprados por cima: 4 px Gold; pendentes: 3 px Border (§2.1).
		for (var pass = 0; pass < 2; pass++)
		{
			var purchasedPass = pass == 1;
			foreach (var link in _skillLinks)
			{
				var purchased = CharacterSheet.HasSkill(link.FromId);
				if (purchased != purchasedPass)
					continue;
				_skillLinkLayer.DrawPolyline(link.Points, UiComponents.SkillLinkColor(purchased), UiComponents.SkillLinkWidth(purchased), true);
			}
		}
	}

	// ---------- Foco e navegação ----------

	// Direcional vai ao nó mais próximo na direção (distância no eixo + 2,5× o desvio), atravessando as duas árvores.
	private void LinkSkillNodeNeighbors()
	{
		Control lastBottom = null;
		foreach (var view in _skillNodeOrder)
		{
			var button = view.Button;
			button.FocusNeighborLeft = PathToNearestSkillNode(view, Vector2.Left);
			button.FocusNeighborRight = PathToNearestSkillNode(view, Vector2.Right);
			button.FocusNeighborTop = PathToNearestSkillNode(view, Vector2.Up);
			var down = FindNearestSkillNode(view, Vector2.Down);
			if (down != null)
				button.FocusNeighborBottom = button.GetPathTo(down.Button);
			else if (_bloodCoinCheatButton != null)
			{
				button.FocusNeighborBottom = button.GetPathTo(_bloodCoinCheatButton);
				lastBottom = button;
			}
			else
				button.FocusNeighborBottom = button.GetPathTo(button);
		}
		var count = _skillNodeOrder.Count;
		for (var i = 0; i < count; i++)
		{
			var button = _skillNodeOrder[i].Button;
			button.FocusNext = button.GetPathTo(_skillNodeOrder[(i + 1) % count].Button);
			button.FocusPrevious = button.GetPathTo(_skillNodeOrder[(i - 1 + count) % count].Button);
		}
		if (_bloodCoinCheatButton != null && lastBottom != null)
			_bloodCoinCheatButton.FocusNeighborTop = _bloodCoinCheatButton.GetPathTo(lastBottom);
	}

	private NodePath PathToNearestSkillNode(SkillNodeView from, Vector2 direction)
	{
		var target = FindNearestSkillNode(from, direction);
		// Sem vizinho: fica no próprio nó (não escapa para controles fora da árvore).
		return from.Button.GetPathTo(target?.Button ?? from.Button);
	}

	private SkillNodeView FindNearestSkillNode(SkillNodeView from, Vector2 direction)
	{
		SkillNodeView best = null;
		var bestScore = float.MaxValue;
		foreach (var other in _skillNodeOrder)
		{
			if (other == from)
				continue;
			var delta = other.Center - from.Center;
			var along = delta.Dot(direction);
			if (along <= 1.0f)
				continue;
			var score = along + Mathf.Abs(delta.Cross(direction)) * 2.5f;
			if (score < bestScore)
			{
				bestScore = score;
				best = other;
			}
		}
		return best;
	}

	private void OnSkillNodeFocused(string id)
	{
		_focusedSkillId = id;
		ShowSkillDetail(id);
		// Foco por clique não mexe no pan (o clique terminaria fora do botão).
		if (Input.GetMouseButtonMask() == 0 && _skillNodeViews.TryGetValue(id, out var view))
			EnsureSkillNodeVisible(view);
	}

	// Foco inicial: primeiro nó comprável; senão o primeiro disponível; senão o primeiro.
	private SkillNodeView FindInitialSkillNode()
	{
		SkillNodeView available = null;
		foreach (var view in _skillNodeOrder)
		{
			if (view.State is SkillNodeState.Available or SkillNodeState.Purchased)
				return view;
			if (available == null && view.State == SkillNodeState.AvailableNoCoins)
				available = view;
		}
		return available ?? (_skillNodeOrder.Count > 0 ? _skillNodeOrder[0] : null);
	}

	private void OnSkillNodeInput(SkillNodeView view, InputEvent input)
	{
		// Clique direito: atalho opcional para o mesmo painel de detalhes (§8.14).
		if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
		{
			view.Button.GrabFocus();
			ShowSkillDetail(view.Id);
			view.Button.AcceptEvent();
			return;
		}
		if (HandleSkillTreePointer(input, false))
			view.Button.AcceptEvent();
	}

	// ---------- Estado e atualização ----------

	private void RefreshBloodFountain()
	{
		if (_bloodFountainWindow == null)
			return;

		_bloodFountainCoinsLabel.Text = CharacterSheet.BloodCoins.ToString();
		RefreshSkillTreeButtons();
		_skillLinkLayer?.QueueRedraw();
		ShowSkillDetail(_hoveredSkillId ?? _focusedSkillId);
	}

	private void RefreshSkillTreeButtons()
	{
		foreach (var view in _skillNodeOrder)
			RefreshSkillNode(view);
	}

	private static SkillNodeState GetSkillNodeState(SkillDefinition definition)
	{
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		var maxRank = GetSkillMaxRank(definition);
		var canAfford = CharacterSheet.BloodCoins >= GetSkillUpgradeCost(definition);
		if (!definition.IsImplemented)
			return SkillNodeState.Planned;
		if (currentRank >= maxRank)
			return SkillNodeState.PurchasedMax;
		if (currentRank > 0)
			return canAfford ? SkillNodeState.Purchased : SkillNodeState.PurchasedNoCoins;
		if (SkillCatalog.HasExclusiveConflict(definition))
			return SkillNodeState.Conflict;
		if (!SkillCatalog.HasPrerequisites(definition))
			return SkillNodeState.Locked;
		return canAfford ? SkillNodeState.Available : SkillNodeState.AvailableNoCoins;
	}

	private void RefreshSkillNode(SkillNodeView view)
	{
		if (!SkillCatalog.TryGet(view.Id, out var definition))
			return;

		var state = GetSkillNodeState(definition);
		view.State = state;
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		var maxRank = GetSkillMaxRank(definition);

		var normal = UiComponents.SkillNodeStyle(state);
		var hover = (StyleBoxFlat)normal.Duplicate();
		if (state != SkillNodeState.Planned)
			hover.BgColor = UiPalette.PanelRaised;
		view.Button.AddThemeStyleboxOverride("normal", normal);
		view.Button.AddThemeStyleboxOverride("disabled", normal);
		view.Button.AddThemeStyleboxOverride("hover", hover);
		view.Button.AddThemeStyleboxOverride("pressed", hover);
		view.Button.AddThemeStyleboxOverride("hover_pressed", hover);

		view.Sigla.AddThemeColorOverride("font_color", state switch
		{
			SkillNodeState.AvailableNoCoins => UiPalette.TextSecondary,
			SkillNodeState.Locked or SkillNodeState.Conflict or SkillNodeState.Planned => UiPalette.TextDisabled,
			_ => UiPalette.Gold
		});
		view.NameLabel.AddThemeColorOverride("font_color", state switch
		{
			SkillNodeState.AvailableNoCoins => UiPalette.TextSecondary,
			SkillNodeState.Locked or SkillNodeState.Conflict or SkillNodeState.Planned => UiPalette.TextDisabled,
			_ => UiPalette.TextPrimary
		});

		foreach (var child in view.BadgeSlot.GetChildren())
		{
			view.BadgeSlot.RemoveChild(child);
			child.QueueFree();
		}
		var badge = state switch
		{
			SkillNodeState.PurchasedMax => UiComponents.Badge("MÁX", UiPalette.GoldBright),
			SkillNodeState.Purchased or SkillNodeState.PurchasedNoCoins => UiComponents.Badge($"{currentRank}/{maxRank}", UiPalette.Gold),
			SkillNodeState.Planned => UiComponents.Badge("EM BREVE", UiPalette.PanelInset, UiPalette.TextSecondary),
			SkillNodeState.Available or SkillNodeState.AvailableNoCoins => UiComponents.Badge($"0/{maxRank}", UiPalette.PanelInset, UiPalette.TextSecondary),
			_ => null
		};
		if (badge != null)
			view.BadgeSlot.AddChild(badge);

		view.Lock.Visible = state == SkillNodeState.Locked;
		view.Strike.Visible = state == SkillNodeState.Conflict;
		view.Dashed.Visible = state == SkillNodeState.Planned;

		view.CostRow.Visible = state != SkillNodeState.PurchasedMax;
		view.CostLabel.Text = GetSkillUpgradeCost(definition).ToString();
		view.CostLabel.AddThemeColorOverride("font_color", UiComponents.SkillNodeCostColor(state));
	}

	// ---------- Painel de detalhes (§3.6) ----------

	private void ShowSkillDetail(string skillId)
	{
		if (_skillDetailPanel == null)
			return;
		if (string.IsNullOrEmpty(skillId) || !SkillCatalog.TryGet(skillId, out var definition))
		{
			_skillDetailPanel.Clear();
			return;
		}

		var state = GetSkillNodeState(definition);
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		var maxRank = GetSkillMaxRank(definition);
		var cost = GetSkillUpgradeCost(definition);
		var coins = CharacterSheet.BloodCoins;

		var (badgeText, badgeColor) = state switch
		{
			SkillNodeState.PurchasedMax => ("Nível máximo", UiPalette.GoldBright),
			SkillNodeState.Purchased or SkillNodeState.PurchasedNoCoins => ($"Adquirida {currentRank}/{maxRank}", UiPalette.Gold),
			SkillNodeState.Available => ("Disponível", UiPalette.TextPrimary),
			SkillNodeState.AvailableNoCoins => ("Disponível", UiPalette.Border),
			SkillNodeState.Conflict => ("Exclusiva", UiPalette.PanelInset),
			SkillNodeState.Planned => ("Em breve", UiPalette.PanelInset),
			_ => ("Bloqueada", UiPalette.PanelInset)
		};

		var body = EscapeSkillBbcode(definition.Description);
		var requirements = FormatSkillRequirementsBbcode(definition);
		if (!string.IsNullOrEmpty(requirements))
			body += "\n" + requirements;
		if (state == SkillNodeState.Conflict)
			body += $"\n[color=#{UiPalette.Warn.ToHtml(false)}]Exclusiva: não pode ser combinada com {EscapeSkillBbcode(FindExclusiveConflictName(definition))}.[/color]";
		if (state == SkillNodeState.Planned)
			body += currentRank > 0
				? "\nAdquirida em save anterior; efeito ainda em planejamento."
				: "\nEfeito em planejamento; compra indisponível.";

		string actionText;
		Color actionColor;
		UiAction? actionGlyph = null;
		switch (state)
		{
			case SkillNodeState.PurchasedMax:
				actionText = "Nível máximo atingido";
				actionColor = UiPalette.TextSecondary;
				break;
			case SkillNodeState.Planned:
				actionText = "Compra indisponível (em planejamento)";
				actionColor = UiPalette.TextSecondary;
				break;
			case SkillNodeState.Conflict:
				actionText = "Bloqueada por escolha exclusiva";
				actionColor = UiPalette.TextSecondary;
				break;
			case SkillNodeState.Locked:
				actionText = "Bloqueada: compre os pré-requisitos";
				actionColor = UiPalette.TextSecondary;
				break;
			case SkillNodeState.PurchasedNoCoins:
			case SkillNodeState.AvailableNoCoins:
				actionText = $"Moedas insuficientes: custa {cost}, faltam {cost - coins}";
				actionColor = UiPalette.Warn;
				break;
			default:
				actionText = currentRank > 0
					? $"Aprimorar para {currentRank + 1}/{maxRank} · {cost} moedas"
					: $"Comprar · {cost} moedas";
				actionColor = UiPalette.Gold;
				actionGlyph = UiAction.Accept;
				break;
		}

		_skillDetailPanel.Show(new DetailData
		{
			IconText = definition.IconText,
			Title = definition.DisplayName,
			BadgeText = badgeText,
			BadgeColor = badgeColor,
			Meta = $"{definition.Tree} · Passiva · Tier {definition.Tier} · Nível {currentRank}/{maxRank}",
			BodyBbcode = body,
			Flavor = definition.FlavorText,
			ActionText = actionText,
			ActionGlyph = actionGlyph
		});
		_skillDetailPanel.ActionLabel.AddThemeColorOverride("font_color", actionColor);

		// Ícone do painel = réplica do nó (sigla no estilo do estado), em vez do monograma genérico.
		foreach (var child in _skillDetailPanel.IconSlot.GetChildren())
		{
			_skillDetailPanel.IconSlot.RemoveChild(child);
			child.QueueFree();
		}
		var tile = new PanelContainer
		{
			CustomMinimumSize = new Vector2(UiMetrics.IconM, UiMetrics.IconM),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		tile.AddThemeStyleboxOverride("panel", UiComponents.SkillNodeStyle(state));
		var sigla = new Label
		{
			Text = definition.IconText,
			ThemeTypeVariation = UiVariation.SectionGoldLabel,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		if (state is SkillNodeState.Locked or SkillNodeState.Conflict or SkillNodeState.Planned)
			sigla.AddThemeColorOverride("font_color", UiPalette.TextDisabled);
		tile.AddChild(sigla);
		_skillDetailPanel.IconSlot.AddChild(tile);
	}

	private static string FormatSkillRequirementsBbcode(SkillDefinition definition)
	{
		if (definition.Prerequisites.Length == 0)
			return "";
		var owned = UiPalette.TextPrimary.ToHtml(false);
		var missing = UiPalette.Warn.ToHtml(false);
		var label = UiPalette.TextSecondary.ToHtml(false);
		var parts = new List<string>();
		foreach (var prerequisiteId in definition.Prerequisites)
		{
			var name = SkillCatalog.TryGet(prerequisiteId, out var prerequisite) ? prerequisite.DisplayName : prerequisiteId;
			var has = CharacterSheet.HasSkill(prerequisiteId);
			parts.Add($"[color=#{(has ? owned : missing)}]{EscapeSkillBbcode(name)} ({(has ? "adquirida" : "falta")})[/color]");
		}
		return $"[color=#{label}]Requer:[/color] {string.Join(", ", parts)}";
	}

	private static string FindExclusiveConflictName(SkillDefinition definition)
	{
		foreach (var candidate in SkillCatalog.All)
			if (candidate.Id != definition.Id && candidate.ExclusiveGroup == definition.ExclusiveGroup && CharacterSheet.HasSkill(candidate.Id))
				return candidate.DisplayName;
		return "outra escolha já adquirida";
	}

	private static string EscapeSkillBbcode(string text) => (text ?? "").Replace("[", "[lb]");

	// Compatibilidade: abre a Fonte (se preciso) com o nó focado e o painel de detalhes dele.
	private void ShowSkillDescription(SkillDefinition definition)
	{
		if (definition == null)
			return;
		if (!IsVisible(_bloodFountainOverlay))
			OpenBloodFountain();
		if (_skillNodeViews.TryGetValue(definition.Id, out var view))
			view.Button.GrabFocus();
		ShowSkillDetail(definition.Id);
	}

	// Usado também pelo compêndio (HUD.Compendium.cs).
	private string FormatPrerequisites(SkillDefinition definition)
	{
		if (definition.Prerequisites.Length == 0)
			return "nenhum";

		var text = "";
		for (var i = 0; i < definition.Prerequisites.Length; i++)
		{
			if (SkillCatalog.TryGet(definition.Prerequisites[i], out var prerequisite))
				text += prerequisite.DisplayName;
			else
				text += definition.Prerequisites[i];

			if (i < definition.Prerequisites.Length - 1)
				text += ", ";
		}

		return text;
	}

	// ---------- Compra (regras inalteradas) ----------

	private void BuyFocusedSkill()
	{
		if (!string.IsNullOrEmpty(_focusedSkillId))
			BuySkill(_focusedSkillId);
	}

	private void BuySkill(string skillId)
	{
		if (!SkillCatalog.TryGet(skillId, out var definition))
		{
			SetBloodFountainStatus("Habilidade desconhecida.", true);
			return;
		}

		if (!definition.IsImplemented)
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda está em planejamento.", true);
			return;
		}

		var currentRank = CharacterSheet.GetSkillRank(skillId);
		var maxRank = GetSkillMaxRank(definition);
		if (currentRank >= maxRank)
		{
			SetBloodFountainStatus($"{definition.DisplayName} já está no nível máximo ({maxRank}).", true);
			return;
		}

		if (!SkillCatalog.HasPrerequisites(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} ainda está bloqueada: faltam pré-requisitos.", true);
			return;
		}

		if (SkillCatalog.HasExclusiveConflict(definition))
		{
			SetBloodFountainStatus($"{definition.DisplayName} conflita com outra escolha já adquirida.", true);
			return;
		}

		var cost = GetSkillUpgradeCost(definition);
		if (_bloodFountainPreviewMode)
		{
			// Preview de debug: valida igual, mas não gasta moedas nem grava o save.
			SetBloodFountainStatus(CharacterSheet.BloodCoins >= cost
				? $"Prévia: compra de {definition.DisplayName} simulada (nada foi gravado)."
				: $"Moedas insuficientes para {definition.DisplayName}.", CharacterSheet.BloodCoins < cost);
			return;
		}

		if (!CharacterSheet.SpendBloodCoins(cost))
		{
			SetBloodFountainStatus($"Moedas insuficientes para {definition.DisplayName}.", true);
			return;
		}

		if (!CharacterSheet.AddSkill(skillId))
		{
			CharacterSheet.AddBloodCoins(cost);
			SetBloodFountainStatus($"Não foi possível adquirir {definition.DisplayName}.", true);
			return;
		}

		// §8.2: contador de moedas do HUD pelo caminho único (antes era texto fixo aqui).
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
		RefreshCharacterSheet();
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		RefreshBloodFountain();
		SetBloodFountainStatus($"{definition.DisplayName} aprimorada para o nível {currentRank + 1}/{maxRank}.", false);
	}

	private static int GetSkillMaxRank(SkillDefinition definition)
	{
		return Mathf.Max(CompendiumTuning.GetInt($"{definition.Id}.max_rank", definition.MaxRank), 1);
	}

	private static int GetSkillUpgradeCost(SkillDefinition definition)
	{
		var baseCost = CompendiumTuning.GetInt($"{definition.Id}.cost", definition.Cost);
		var currentRank = CharacterSheet.GetSkillRank(definition.Id);
		return Mathf.RoundToInt(baseCost * (1.0f + currentRank * 0.5f));
	}

	private void AddCheatBloodCoins()
	{
		if (_bloodFountainPreviewMode)
		{
			SetBloodFountainStatus("Prévia: +500 moedas simuladas (nada foi gravado).", false);
			return;
		}
		CharacterSheet.AddBloodCoins(500);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
		SetBloodFountainStatus("+500 moedas adicionadas para teste.", false);
	}

	// Status de uma linha sob o título; aviso (moedas, bloqueio) em Warn.
	private void SetBloodFountainStatus(string text, bool warning)
	{
		if (_bloodFountainWindow == null)
			return;
		_bloodFountainWindow.SetStatus(text);
		if (warning)
			_bloodFountainWindow.Status.AddThemeColorOverride("font_color", UiPalette.Warn);
		else
			_bloodFountainWindow.Status.RemoveThemeColorOverride("font_color");
	}

	// ---------- Abrir / fechar ----------

	public void OpenBloodFountain()
	{
		if (_bloodFountainOverlay == null)
			return;

		// Já aberta (ex.: interact relido pelo objeto 3D): só atualiza, sem roubar o foco.
		if (UiModalStack.IsOpen(_bloodFountainOverlay))
		{
			RefreshBloodFountain();
			return;
		}

		_hoveredSkillId = null;
		_isDraggingBloodFountain = false;
		_skillTreeFitPending = true;
		SetBloodFountainStatus(_bloodFountainPreviewMode ? "Prévia: compras e cheat são simulados; nada é gravado." : null, false);
		RefreshBloodFountain();
		var initial = FindInitialSkillNode();
		// §5.1: Fonte não pausa (só bloqueia input do jogo); Esc/B fecha pela pilha.
		UiModalStack.Push(_bloodFountainOverlay, false, initial?.Button, CloseBloodFountain);
		_bloodFountainWindow.FadeIn();
		// O tamanho da área da árvore só existe depois do layout: zoom inicial e nó focado no próximo quadro
		// (ou no primeiro Resized, se o layout ainda não tiver rodado).
		Callable.From(FitSkillTreeView).CallDeferred();
	}

	private void CloseBloodFountain()
	{
		_isDraggingBloodFountain = false;
		_hoveredSkillId = null;
		if (_bloodFountainOverlay != null)
			UiModalStack.Pop(_bloodFountainOverlay);
	}

	// Preview de debug (HUD.OpenPreviewScreen). skill_popup foca um nó bloqueado/sem moedas para mostrar o detalhe.
	private void OpenBloodFountainPreview(bool focusBlockedNode)
	{
		_bloodFountainPreviewMode = true;
		SeedBloodFountainPreviewData();
		// --ui-zoom=0.5 (opcional): captura da árvore inteira.
		foreach (var argument in OS.GetCmdlineUserArgs())
			if (argument.StartsWith("--ui-zoom=") && float.TryParse(argument.Substring(10), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var zoom))
				_bloodFountainPreviewZoom = Mathf.Clamp(zoom, SkillZoomMin, SkillZoomMax);
		OpenBloodFountain();
		if (!focusBlockedNode)
			return;
		SkillNodeView target = null;
		foreach (var view in _skillNodeOrder)
		{
			if (view.State is SkillNodeState.Locked or SkillNodeState.Conflict)
			{
				target = view;
				break;
			}
			if (target == null && view.State is SkillNodeState.AvailableNoCoins or SkillNodeState.PurchasedNoCoins)
				target = view;
		}
		if (target != null)
			target.Button.GrabFocus();
	}

	// Dados fictícios só quando nenhum slot está carregado (cena aberta direto): nada é gravado sem slot.
	private void SeedBloodFountainPreviewData()
	{
		if (SaveGameManager.Instance != null && SaveGameManager.Instance.CurrentSlot > 0)
			return;
		if (CharacterSheet.AcquiredSkills.Count > 0)
			return;
		var seed = new (string Id, int Ranks)[]
		{
			(SkillCatalog.VampireFangsId, 2),
			("bestialis.opening_strike", 1),
			(SkillCatalog.SurvivalInstinctId, 5),
			("humanitas.thick_skin", 1)
		};
		foreach (var (id, ranks) in seed)
			for (var i = 0; i < ranks; i++)
				CharacterSheet.AddSkill(id);
		if (CharacterSheet.BloodCoins < SkillPreviewCoins)
			CharacterSheet.AddBloodCoins(SkillPreviewCoins - CharacterSheet.BloodCoins);
		OnBloodCoinsChanged(CharacterSheet.BloodCoins);
	}

	// ---------- Zoom e pan ----------

	// Chamado por HUD._Process: analógico direito move, gatilhos/teclas −/= dão zoom contínuo.
	private void ProcessBloodFountainInput(float delta)
	{
		if (_skillTreeViewport == null || !IsVisible(_bloodFountainOverlay) || UiModalStack.Top != _bloodFountainOverlay)
			return;

		var pan = Input.GetVector(SkillPanLeftAction, SkillPanRightAction, SkillPanUpAction, SkillPanDownAction);
		if (pan != Vector2.Zero)
			PanBloodFountain(-pan * SkillPanSpeed * delta);

		var zoom = Input.GetActionStrength(SkillZoomInAction) - Input.GetActionStrength(SkillZoomOutAction);
		if (!Mathf.IsZeroApprox(zoom))
			SetSkillTreeZoom(_bloodFountainZoom * (1.0f + zoom * SkillZoomSpeed * delta), _skillTreeViewport.Size * 0.5f);
	}

	private void OnSkillTreeViewportInput(InputEvent input)
	{
		if (HandleSkillTreePointer(input, true))
			_skillTreeViewport.AcceptEvent();
	}

	// Roda = zoom no ponteiro; arrastar (esquerdo no fundo, meio em qualquer lugar) = pan.
	private bool HandleSkillTreePointer(InputEvent input, bool allowLeftDrag)
	{
		if (input is InputEventMouseButton button)
		{
			if (button.Pressed && (button.ButtonIndex == MouseButton.WheelUp || button.ButtonIndex == MouseButton.WheelDown))
			{
				var step = button.ButtonIndex == MouseButton.WheelUp ? SkillZoomStep : -SkillZoomStep;
				SetSkillTreeZoom(_bloodFountainZoom + step, _skillTreeViewport.GetLocalMousePosition());
				return true;
			}
			if (button.ButtonIndex == MouseButton.Middle || (allowLeftDrag && button.ButtonIndex == MouseButton.Left))
			{
				_isDraggingBloodFountain = button.Pressed;
				_lastBloodFountainMousePosition = _skillTreeViewport.GetLocalMousePosition();
				return true;
			}
			return false;
		}
		if (input is InputEventMouseMotion && _isDraggingBloodFountain)
		{
			if (Input.GetMouseButtonMask() == 0)
			{
				_isDraggingBloodFountain = false;
				return false;
			}
			// Delta no espaço da área (o evento chega transformado pelo zoom quando vem de um nó).
			var position = _skillTreeViewport.GetLocalMousePosition();
			PanBloodFountain(position - _lastBloodFountainMousePosition);
			_lastBloodFountainMousePosition = position;
			return true;
		}
		return false;
	}

	private void AdjustBloodFountainZoom(float delta)
	{
		if (_skillTreeViewport == null)
			return;
		SetSkillTreeZoom(_bloodFountainZoom + delta, _skillTreeViewport.Size * 0.5f);
	}

	// Zoom mantendo `pivot` (coordenadas da área) parado na tela.
	private void SetSkillTreeZoom(float zoom, Vector2 pivot)
	{
		zoom = Mathf.Clamp(zoom, SkillZoomMin, SkillZoomMax);
		if (Mathf.IsEqualApprox(zoom, _bloodFountainZoom))
			return;
		_skillTreePan = pivot - (pivot - _skillTreePan) * (zoom / _bloodFountainZoom);
		_bloodFountainZoom = zoom;
		ClampSkillTreePan();
		ApplySkillTreeTransform();
	}

	private void PanBloodFountain(Vector2 delta)
	{
		_skillTreePan += delta;
		ClampSkillTreePan();
		ApplySkillTreeTransform();
	}

	// Conteúdo menor que a área fica centrado; maior, pode ir até a margem em cada borda.
	private void ClampSkillTreePan()
	{
		if (_skillTreeViewport == null)
			return;
		var view = _skillTreeViewport.Size;
		if (view.X <= 1.0f || view.Y <= 1.0f)
			return;
		var content = _skillTreeContentSize * _bloodFountainZoom;
		_skillTreePan.X = content.X <= view.X
			? (view.X - content.X) * 0.5f
			: Mathf.Clamp(_skillTreePan.X, view.X - content.X - SkillViewMargin, SkillViewMargin);
		_skillTreePan.Y = content.Y <= view.Y
			? (view.Y - content.Y) * 0.5f
			: Mathf.Clamp(_skillTreePan.Y, view.Y - content.Y - SkillViewMargin, SkillViewMargin);
	}

	private void ApplySkillTreeTransform()
	{
		if (_skillTreeCanvas == null)
			return;

		_skillTreeCanvas.Scale = Vector2.One * _bloodFountainZoom;
		_skillTreeCanvas.Position = _skillTreePan;
		_bloodFountainZoomHint?.SetLabel($"Zoom {Mathf.RoundToInt(_bloodFountainZoom * 100.0f)}%");
	}

	// Leva o bloco do nó (nó + custo + nome) para dentro da área visível, com margem.
	private void EnsureSkillNodeVisible(SkillNodeView view)
	{
		if (_skillTreeViewport == null || view == null)
			return;
		var area = _skillTreeViewport.Size;
		if (area.X <= 1.0f || area.Y <= 1.0f)
			return;
		var left = view.Center.X - SkillColumnWidth * 0.5f;
		var top = view.Origin.Y - SkillViewMargin;
		var block = new Rect2(left, top, SkillColumnWidth, SkillBlockHeight + SkillViewMargin * 2.0f);
		var screenPosition = _skillTreePan + block.Position * _bloodFountainZoom;
		var screenEnd = _skillTreePan + block.End * _bloodFountainZoom;
		if (screenEnd.X > area.X)
			_skillTreePan.X -= screenEnd.X - area.X;
		if (screenPosition.X < 0.0f)
			_skillTreePan.X -= screenPosition.X;
		if (screenEnd.Y > area.Y)
			_skillTreePan.Y -= screenEnd.Y - area.Y;
		if (screenPosition.Y < 0.0f)
			_skillTreePan.Y -= screenPosition.Y;
		ClampSkillTreePan();
		ApplySkillTreeTransform();
	}

	private void OnSkillTreeViewportResized()
	{
		if (_skillTreeFitPending && IsVisible(_bloodFountainOverlay) && ApplyInitialSkillTreeZoom())
			return;
		ClampSkillTreePan();
		ApplySkillTreeTransform();
		if (!string.IsNullOrEmpty(_focusedSkillId) && _skillNodeViews.TryGetValue(_focusedSkillId, out var view) && view.Button.HasFocus())
			EnsureSkillNodeVisible(view);
	}

	private void FitSkillTreeView()
	{
		if (!IsInstanceValid(this) || !IsVisible(_bloodFountainOverlay))
			return;
		OnSkillTreeViewportResized();
	}

	// Zoom inicial "caber na altura" (85–100%), canvas no topo e nó focado visível. False = área ainda sem tamanho.
	private bool ApplyInitialSkillTreeZoom()
	{
		var area = _skillTreeViewport.Size;
		if (area.X <= 1.0f || area.Y <= 1.0f || _skillTreeContentSize.Y <= 1.0f)
			return false;
		_skillTreeFitPending = false;
		var zoom = _bloodFountainPreviewZoom > 0.0f
			? _bloodFountainPreviewZoom
			: Mathf.Clamp(area.Y / _skillTreeContentSize.Y, SkillZoomFitMin, SkillZoomFitMax);
		_bloodFountainZoom = Mathf.Clamp(zoom, SkillZoomMin, SkillZoomMax);
		_skillTreePan = new Vector2((area.X - _skillTreeContentSize.X * _bloodFountainZoom) * 0.5f, 0.0f);
		ClampSkillTreePan();
		ApplySkillTreeTransform();
		if (!string.IsNullOrEmpty(_focusedSkillId) && _skillNodeViews.TryGetValue(_focusedSkillId, out var view) && view.Button.HasFocus())
			EnsureSkillNodeVisible(view);
		return true;
	}
}
