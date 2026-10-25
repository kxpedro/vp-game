using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class HUD
{
	public void OpenBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(false);
	}

	public void OpenMandatoryBloodBlessingOffer()
	{
		OpenBloodBlessingOfferInternal(true);
	}

	// "mandatory" = primeira bênção da run (sem absorção). Toda oferta é obrigatória: não fecha sem escolher.
	private async void OpenBloodBlessingOfferInternal(bool mandatory)
	{
		if (_bloodBlessingOverlay == null) return;
		// Reentrada: absorção em curso ou oferta já aberta (reabrir re-sortearia as opções).
		if (_blessingAbsorptionRunning || UiModalStack.IsOpen(_bloodBlessingOverlay)) return;
		if (mandatory)
		{
			AppendGameLog("A fonte primordial revelou a primeira Bênção de Sangue desta run.");
			BuildBloodBlessingChoices();
			return;
		}
		// Sangue da sala já sugado ao limpá-la (AbsorbRoomBloodOnClear): a oferta abre direto, sem segunda absorção.
		if (!HasRoomBloodPools())
		{
			BuildBloodBlessingChoices();
			return;
		}
		_blessingAbsorptionRunning = true;
		UiModalStack.RefreshPause();
		_bloodAbsorptionOverlay.Visible = true;
		_bloodAbsorptionOverlay.Modulate = Colors.White;
		_bloodAbsorptionOverlay.Color = new Color(0.16f, 0.0f, 0.018f, 0.0f);
		PlaySanguisBanner(0.4f, 2.55f, 0.5f);
		var vignetteTween = _bloodAbsorptionOverlay.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.11f, 0.5f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.035f, 2.35f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.0f, 0.55f);

		await SuckRoomBlood(3.45f);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		_bloodAbsorptionOverlay.Visible = false;
		_blessingAbsorptionRunning = false;
		// Morreu durante a absorção: a oferta não abre por cima do game over.
		if (_isGameOver) return;
		AppendGameLog("O sangue dos inimigos foi absorvido e condensado em uma Bênção de Sangue.");
		BuildBloodBlessingChoices();
	}

	// Recompensa da sala que não é Bênção (RunManager): só a sucção do sangue, sem título, sem pausar o jogo.
	public async void AbsorbRoomBlood()
	{
		if (_blessingAbsorptionRunning || _roomBloodSuctionRunning) return;
		_roomBloodSuctionRunning = true;
		await SuckRoomBlood(1.8f);
		if (!IsInstanceValid(this)) return;
		_roomBloodSuctionRunning = false;
	}

	private bool _roomBloodSuctionRunning;

	private bool HasRoomBloodPools()
	{
		foreach (var node in GetTree().GetNodesInGroup(BloodPool.Group))
			if (node is BloodPool pool && !pool.IsQueuedForDeletion())
				return true;
		return false;
	}

	// Sala limpa (RunManager.AbsorbBloodThenSpawnPickups): o Virgil suga o sangue de todos os inimigos com o título
	// "Sanguis Purgatus", sem pausar; ao terminar avisa o RunManager, que solta a herança da sala no chão.
	public async void AbsorbRoomBloodOnClear()
	{
		if (_roomBloodSuctionRunning)
		{
			GetTree().CallGroup("run_manager", "OnRoomBloodAbsorbed");
			return;
		}
		_roomBloodSuctionRunning = true;
		_bloodAbsorptionOverlay.Visible = true;
		_bloodAbsorptionOverlay.Modulate = Colors.White;
		_bloodAbsorptionOverlay.Color = new Color(0.16f, 0.0f, 0.018f, 0.0f);
		PlaySanguisBanner(0.35f, 1.6f, 0.45f);
		var vignetteTween = _bloodAbsorptionOverlay.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.11f, 0.45f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.035f, 1.5f);
		vignetteTween.TweenProperty(_bloodAbsorptionOverlay, "color:a", 0.0f, 0.45f);
		await SuckRoomBlood(2.4f);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		_roomBloodSuctionRunning = false;
		if (!_blessingAbsorptionRunning)
			_bloodAbsorptionOverlay.Visible = false;
		AppendGameLog("O sangue dos inimigos foi absorvido.");
		GetTree().CallGroup("run_manager", "OnRoomBloodAbsorbed");
	}

	// O Virgil suga o sangue da sala: cada poça (BloodPool) seca enquanto um filete contínuo de sangue sobe dela e
	// escorre em espiral até ele. O filete é uma fita desenhada de frente para a câmera (ImmediateMesh), refeita a cada
	// quadro entre a cauda (ainda presa à poça) e a ponta; a ponta solta respingos (AttachSanguisTrail).
	private async Task SuckRoomBlood(float duration)
	{
		var player = GetTree().GetFirstNodeInGroup("player") as Node3D;
		var scene = GetTree().CurrentScene;
		var pools = new List<BloodPool>();
		foreach (var node in GetTree().GetNodesInGroup(BloodPool.Group))
			if (node is BloodPool pool && !pool.IsQueuedForDeletion())
				pools.Add(pool);

		var streams = new List<BloodStream>();
		var tailLag = duration * 0.4f;
		for (var i = 0; i < pools.Count && scene != null; i++)
		{
			var pool = pools[i];
			pool.RemoveFromGroup(BloodPool.Group);
			var delay = Mathf.Min(i * 0.024f, duration * 0.05f);
			var head = new Node3D { Name = "BloodStreamHead", ProcessMode = ProcessModeEnum.Always };
			scene.AddChild(head);
			head.GlobalPosition = pool.GlobalPosition;
			AttachSanguisTrail(head);
			streams.Add(new BloodStream
			{
				Pool = pool,
				Head = head,
				Start = pool.GlobalPosition + Vector3.Up * 0.02f,
				Phase = (float)GD.RandRange(0.0, Mathf.Tau),
				Delay = delay,
				Flight = Mathf.Max(duration - delay - tailLag, 0.1f),
				DrainStarted = false,
			});
		}

		MeshInstance3D streamNode = null;
		ImmediateMesh streamMesh = null;
		if (streams.Count > 0)
		{
			streamMesh = new ImmediateMesh();
			streamNode = new MeshInstance3D
			{
				Name = "BloodStreams",
				Mesh = streamMesh,
				ProcessMode = ProcessModeEnum.Always,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					VertexColorUseAsAlbedo = true,
					CullMode = BaseMaterial3D.CullModeEnum.Disabled,
				},
			};
			scene.AddChild(streamNode);
			streamNode.GlobalTransform = Transform3D.Identity;
		}

		var elapsed = 0.0f;
		while (elapsed < duration && player != null && IsInstanceValid(player))
		{
			var delta = Mathf.Max((float)GetProcessDeltaTime(), 0.001f);
			var destination = player.GlobalPosition + Vector3.Up * 0.95f;
			var camera = GetViewport()?.GetCamera3D();
			var toCamera = camera != null ? camera.GlobalBasis.Z : Vector3.Back;
			streamMesh?.ClearSurfaces();
			var drawing = false;
			foreach (var stream in streams)
			{
				var headProgress = Smooth((elapsed - stream.Delay) / stream.Flight);
				var tailProgress = Smooth((elapsed - stream.Delay - tailLag) / stream.Flight);
				if (!stream.DrainStarted && headProgress > 0.0f)
				{
					stream.DrainStarted = true;
					if (IsInstanceValid(stream.Pool))
						stream.Pool.Drain(tailLag + stream.Flight * 0.15f);
				}
				if (IsInstanceValid(stream.Head))
					stream.Head.GlobalPosition = StreamPoint(stream, destination, headProgress);
				if (headProgress <= 0.0f || tailProgress >= 1.0f)
					continue;
				if (!drawing)
				{
					streamMesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
					drawing = true;
				}
				DrawBloodStream(streamMesh, stream, destination, tailProgress, headProgress, toCamera);
			}
			if (drawing)
				streamMesh.SurfaceEnd();
			elapsed += delta;
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (!IsInstanceValid(this) || !IsInsideTree()) return;
		}
		streamMesh?.ClearSurfaces();
		if (player != null && IsInstanceValid(player) && streams.Count > 0)
			SpawnSanguisArrivalPulse(player.GlobalPosition + Vector3.Up * 0.92f);
		foreach (var stream in streams)
			if (IsInstanceValid(stream.Head))
				stream.Head.Visible = false;
		await ToSignal(GetTree().CreateTimer(0.42f, true), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || !IsInsideTree()) return;
		if (streamNode != null && IsInstanceValid(streamNode))
			streamNode.QueueFree();
		foreach (var stream in streams)
		{
			if (IsInstanceValid(stream.Head))
				stream.Head.QueueFree();
			if (IsInstanceValid(stream.Pool))
				stream.Pool.QueueFree();
		}
	}

	private sealed class BloodStream
	{
		public BloodPool Pool;
		public Node3D Head;
		public Vector3 Start;
		public float Phase;
		public float Delay;
		public float Flight;
		public bool DrainStarted;
	}

	// Camadas do filete, de fora para dentro: borda escura, corpo e reflexo (paleta da poça, AAP-64).
	private static readonly Color StreamRim = new("3b1725");
	private static readonly Color StreamBody = new("b4202a");
	private static readonly Color StreamShine = new("e86a73");
	private const float StreamWidth = 0.11f;
	private const int StreamSegments = 28;

	private static float Smooth(float t)
	{
		t = Mathf.Clamp(t, 0.0f, 1.0f);
		return t * t * (3.0f - 2.0f * t);
	}

	// Ponto do caminho em espiral da poça até o Virgil (progress 0 = poça, 1 = peito do Virgil). Nunca desce do chão.
	private static Vector3 StreamPoint(BloodStream stream, Vector3 destination, float progress)
	{
		var start = stream.Start;
		var planar = destination - start;
		planar.Y = 0.0f;
		var direction = planar.LengthSquared() > 0.001f ? planar.Normalized() : Vector3.Forward;
		var tangent = new Vector3(-direction.Z, 0.0f, direction.X);
		var distance = start.DistanceTo(destination);
		var spiralRadius = Mathf.Sin(progress * Mathf.Pi) * Mathf.Min(distance * 0.2f, 1.2f) * (1.0f - progress * 0.3f);
		var angle = stream.Phase + progress * Mathf.Tau * 1.15f;
		var spiralOffset = tangent * Mathf.Cos(angle) * spiralRadius
			+ Vector3.Up * Mathf.Abs(Mathf.Sin(angle)) * spiralRadius * 0.45f;
		// Sobe da poça logo no início, como um fio puxado do chão.
		var lift = Vector3.Up * Mathf.Sin(Mathf.Min(progress * 2.0f, 1.0f) * Mathf.Pi * 0.5f) * 0.35f * (1.0f - progress);
		return start.Lerp(destination, progress) + spiralOffset + lift;
	}

	// Fita do filete entre tail e head, virada para a câmera, em três camadas. Mais grossa junto da poça, afina na ponta
	// e engrossa de leve na própria ponta (a "cabeça" do líquido).
	private static void DrawBloodStream(ImmediateMesh mesh, BloodStream stream, Vector3 destination, float tail, float head, Vector3 toCamera)
	{
		var points = new Vector3[StreamSegments + 1];
		for (var k = 0; k <= StreamSegments; k++)
			points[k] = StreamPoint(stream, destination, Mathf.Lerp(tail, head, k / (float)StreamSegments));
		DrawStreamLayer(mesh, points, toCamera, 1.0f, 0.0f, StreamRim);
		DrawStreamLayer(mesh, points, toCamera, 0.62f, 0.01f, StreamBody);
		DrawStreamLayer(mesh, points, toCamera, 0.18f, 0.02f, StreamShine, 0.04f);
	}

	private static void DrawStreamLayer(ImmediateMesh mesh, Vector3[] points, Vector3 toCamera, float widthScale, float lift, Color color, float sideShift = 0.0f)
	{
		for (var k = 0; k < points.Length - 1; k++)
		{
			var a = points[k] + toCamera * lift;
			var b = points[k + 1] + toCamera * lift;
			var tangent = b - a;
			if (tangent.LengthSquared() < 0.000001f)
				continue;
			var side = tangent.Cross(toCamera).Normalized();
			var wa = StreamHalfWidth(k / (float)(points.Length - 1)) * widthScale;
			var wb = StreamHalfWidth((k + 1) / (float)(points.Length - 1)) * widthScale;
			var shift = side * sideShift * widthScale;
			var a0 = a - side * wa + shift;
			var a1 = a + side * wa + shift;
			var b0 = b - side * wb + shift;
			var b1 = b + side * wb + shift;
			mesh.SurfaceSetColor(color);
			mesh.SurfaceAddVertex(a0);
			mesh.SurfaceAddVertex(a1);
			mesh.SurfaceAddVertex(b1);
			mesh.SurfaceAddVertex(a0);
			mesh.SurfaceAddVertex(b1);
			mesh.SurfaceAddVertex(b0);
		}
	}

	// u = 0 na cauda (poça) e 1 na ponta.
	private static float StreamHalfWidth(float u)
	{
		var body = Mathf.Lerp(1.0f, 0.55f, u);
		var bulb = Mathf.Max(0.0f, (u - 0.85f) / 0.15f) * 0.35f;
		return StreamWidth * 0.5f * (body + bulb);
	}

	private static void AttachSanguisTrail(Node3D essence)
	{
		var moteMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.34f, 0.008f, 0.018f, 0.72f),
			EmissionEnabled = true,
			Emission = new Color(0.28f, 0.004f, 0.012f),
			EmissionEnergyMultiplier = 0.85f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var particleMaterial = new ParticleProcessMaterial
		{
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = 0.045f,
			Gravity = new Vector3(0.0f, -0.08f, 0.0f),
			InitialVelocityMin = 0.01f,
			InitialVelocityMax = 0.09f,
			ScaleMin = 0.35f,
			ScaleMax = 1.0f,
			Color = new Color(0.72f, 0.035f, 0.05f, 0.78f)
		};
		var trail = new GpuParticles3D
		{
			Name = "SanguisTrail",
			Amount = 14,
			Lifetime = 0.52f,
			Randomness = 0.62f,
			LocalCoords = false,
			ProcessMaterial = particleMaterial,
			DrawPass1 = new SphereMesh
			{
				Radius = 0.026f,
				Height = 0.052f,
				RadialSegments = 6,
				Rings = 3,
				Material = moteMaterial
			},
			VisibilityAabb = new Aabb(new Vector3(-2.0f, -2.0f, -2.0f), Vector3.One * 4.0f)
		};
		essence.AddChild(trail);
		trail.Emitting = true;
	}

	private async void SpawnSanguisArrivalPulse(Vector3 position)
	{
		var scene = GetTree().CurrentScene;
		if (scene == null)
			return;

		var root = new Node3D { Name = "SanguisArrivalPulse" };
		scene.AddChild(root);
		root.GlobalPosition = position;
		var pulseMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.32f, 0.006f, 0.018f, 0.26f),
			EmissionEnabled = true,
			Emission = new Color(0.22f, 0.003f, 0.01f),
			EmissionEnergyMultiplier = 0.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
		};
		var pulse = new MeshInstance3D
		{
			Name = "AbsorptionPulse",
			Mesh = new SphereMesh
			{
				Radius = 0.42f,
				Height = 0.84f,
				RadialSegments = 16,
				Rings = 8,
				Material = pulseMaterial
			},
			Scale = Vector3.One * 0.08f
		};
		root.AddChild(pulse);
		var pulseTween = root.CreateTween().SetParallel();
		pulseTween.TweenProperty(pulse, "scale", Vector3.One * 1.55f, 0.38f)
			.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		pulseTween.TweenProperty(pulse, "transparency", 1.0f, 0.38f)
			.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

		var random = new RandomNumberGenerator();
		random.Randomize();
		for (var i = 0; i < 11; i++)
		{
			var angle = random.RandfRange(0.0f, Mathf.Tau);
			var radius = random.RandfRange(0.22f, 0.68f);
			var start = new Vector3(Mathf.Cos(angle) * radius, random.RandfRange(-0.3f, 0.34f), Mathf.Sin(angle) * radius);
			var moteMaterial = new StandardMaterial3D
			{
				AlbedoColor = new Color(0.5f, 0.01f, 0.025f, 0.82f),
				EmissionEnabled = true,
				Emission = new Color(0.38f, 0.006f, 0.018f),
				EmissionEnergyMultiplier = 0.9f,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
			};
			var mote = new MeshInstance3D
			{
				Mesh = new SphereMesh
				{
					Radius = random.RandfRange(0.018f, 0.036f),
					Height = random.RandfRange(0.04f, 0.085f),
					RadialSegments = 6,
					Rings = 3,
					Material = moteMaterial
				},
				Position = start
			};
			root.AddChild(mote);
			var moteTween = root.CreateTween().SetParallel();
			moteTween.TweenProperty(mote, "position", Vector3.Zero, random.RandfRange(0.18f, 0.3f))
				.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			moteTween.TweenProperty(mote, "scale", Vector3.One * 0.08f, 0.3f);
			moteTween.TweenProperty(mote, "transparency", 1.0f, 0.32f);
		}

		await ToSignal(GetTree().CreateTimer(0.48f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(root))
			root.QueueFree();
	}

	// ---------- Oferta (guia §8.17): Window(Large, obrigatória) + ChoiceCard + HintBar ----------

	// Mesma largura máxima dos cards de artefato (guia §3.4); usada pela Bigorna.
	private const float BlessingCardWidth = 1080.0f;

	// Largura da coluna de termos à direita dos cards.
	private const float BlessingTermPanelWidth = 330.0f;
	// Janela da oferta (só os cards); o painel de termos fica fora dela, à direita.
	private const float BlessingWindowWidth = 880.0f;

	private UiWindow _blessingWindow;
	private VBoxContainer _blessingTermPanel;
	private UiHint _blessingRerollHint;
	private readonly List<UiChoiceCard> _blessingCards = new();
	// Termos de cada card (o painel lateral mostra só os do card focado/sob o mouse).
	private readonly List<List<BlessingKeyword>> _blessingCardGlossaries = new();
	private string _blessingOfferFamily = "";
	private bool _blessingAbsorptionRunning;
	private bool _blessingChoiceLocked;
	// Preview (debug): o reroll aparece e só simula (não consome o uso do mapa).
	private bool _blessingOfferPreview;

	private const string BlessingVirgilPath = "res://assets/ui/virgil_oferta/virgil_espada.png";
	private const float BlessingVirgilSize = 520.0f;
	// Fim do desenho (espada incluída) dentro do quadro 256x256: 176/256. Alinha a ponta direita com a borda da janela.
	private const float BlessingVirgilRightEdge = 176.0f / 256.0f;

	// Virgil de pé à esquerda da janela, olhando as opções (estilo Hades): fora da moldura, encostado na borda inferior.
	private void AddBlessingVirgil()
	{
		if (!ResourceLoader.Exists(BlessingVirgilPath)) return;
		var virgil = new TextureRect
		{
			Name = "BlessingVirgil", Texture = ResourceLoader.Load<Texture2D>(BlessingVirgilPath),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest, MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
			// Sobrepõe 20 px da moldura; a base alinha com a base da janela (720 px de altura, centrada).
			OffsetLeft = -BlessingWindowWidth * 0.5f + 20 - BlessingVirgilSize * BlessingVirgilRightEdge,
			OffsetRight = -BlessingWindowWidth * 0.5f + 20 + BlessingVirgilSize * (1.0f - BlessingVirgilRightEdge),
			OffsetTop = 360 - BlessingVirgilSize, OffsetBottom = 360
		};
		_blessingWindow.Root.AddChild(virgil);
	}

	private void BuildBloodBlessingOverlay(Control root)
	{
		_blessingWindow = UiComponents.Window(root, "BloodBlessingOverlay", WindowSize.Large, Localization.Text("offer.blessing.title"));
		_blessingWindow.Mandatory = true;
		_bloodBlessingOverlay = _blessingWindow.Root;
		_bloodBlessingOverlay.Visible = false;
		_bloodBlessingStatusLabel = _blessingWindow.Status;
		_blessingWindow.SetStatus(Localization.Text("hud.blessing_choice"));

		// Janela mais estreita só com os 3 cards (sem scroll). Os termos ficam num painel à parte, fora da moldura, ao lado
		// direito da janela (estilo Hades): só aparece quando o card está sob o mouse/foco.
		_blessingWindow.Panel.OffsetLeft = -BlessingWindowWidth * 0.5f;
		_blessingWindow.Panel.OffsetRight = BlessingWindowWidth * 0.5f;
		_blessingWindow.Panel.CustomMinimumSize = new Vector2(BlessingWindowWidth, 560);

		_bloodBlessingList = new VBoxContainer
		{
			Name = "BlessingCards", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_bloodBlessingList.AddThemeConstantOverride("separation", UiMetrics.S3);
		_blessingWindow.Body.AddChild(_bloodBlessingList);

		AddBlessingVirgil();

		_blessingTermPanel = new VBoxContainer
		{
			Name = "BlessingTerms", MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false,
			AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 0.5f, AnchorBottom = 0.5f,
			OffsetLeft = BlessingWindowWidth * 0.5f + UiMetrics.S4,
			OffsetRight = BlessingWindowWidth * 0.5f + UiMetrics.S4 + BlessingTermPanelWidth,
			OffsetTop = -190, OffsetBottom = 190,
			GrowVertical = Control.GrowDirection.End
		};
		_blessingTermPanel.AddThemeConstantOverride("separation", UiMetrics.S3);
		_blessingWindow.Root.AddChild(_blessingTermPanel);

		// Rodapé sem "Voltar" (oferta obrigatória). Direita: ui_alt (trocar), ui_accept (escolher).
		_blessingRerollHint = _blessingWindow.Footer.Add(UiAction.Alt, "", RerollBlessingOffer);
		_blessingRerollHint.Visible = false;
		_blessingWindow.Footer.Add(UiAction.Accept, Localization.Text("ui.common.choose"), ChooseFocusedBlessing);
		// ui_alt (R / Y / △): HUD.RegisterModalShortcuts → HandleBlessingOfferShortcut (tecla/controle só chegam ao botão focado).

		_bloodAbsorptionOverlay = new ColorRect
		{
			Name = "BloodAbsorptionOverlay", Color = Colors.Transparent,
			AnchorRight = 1, AnchorBottom = 1, Visible = false, ProcessMode = ProcessModeEnum.Always
		};
		root.AddChild(_bloodAbsorptionOverlay);
		BuildSanguisBanner(_bloodAbsorptionOverlay);
	}

	// Banner "Sanguis Purgatus" no estilo Souls: faixa escura atravessando a tela, título serifado dourado com
	// brilho (contornos empilhados e translúcidos) e eco (cópias que crescem e somem na entrada e na saída).
	private const float SanguisBannerHeight = 132f;
	private const int SanguisBannerFontSize = 54;
	private static readonly Color SanguisGold = new Color(0.96f, 0.80f, 0.42f, 1f);
	private static readonly Color SanguisGlow = new Color(1.00f, 0.66f, 0.22f, 1f);

	private const string SanguisBandShader = @"
shader_type canvas_item;
uniform vec4 band_color : source_color = vec4(0.015, 0.004, 0.006, 0.9);
uniform float edge_fade = 0.30;
void fragment() {
	float v = 1.0 - abs(UV.y * 2.0 - 1.0);
	float h = 1.0 - abs(UV.x * 2.0 - 1.0);
	float alpha = smoothstep(0.0, 0.6, v) * smoothstep(0.0, edge_fade, h);
	COLOR = vec4(band_color.rgb, band_color.a * alpha);
}";

	private static Font CreateSanguisFont()
	{
		// Serifada do sistema (sem fonte embarcada): Trajan/Cinzel se houver; no Windows cai em Palatino ou Book Antiqua.
		var serif = new SystemFont
		{
			FontNames = new[] { "Trajan Pro", "Cinzel", "Palatino Linotype", "Book Antiqua", "Constantia", "Georgia", "Times New Roman", "serif" },
			FontWeight = 600
		};
		// Letras espaçadas, como nos títulos dos Souls.
		return new FontVariation { BaseFont = serif, SpacingGlyph = 4 };
	}

	private Label CreateSanguisLabel(Control parent, Font font, Color fontColor, int outlineSize, Color outlineColor)
	{
		var label = new Label
		{
			Text = Localization.Text("hud.absorption.title"), Uppercase = true,
			AnchorRight = 1, AnchorBottom = 1,
			HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", SanguisBannerFontSize);
		label.AddThemeColorOverride("font_color", fontColor);
		label.AddThemeConstantOverride("outline_size", outlineSize);
		label.AddThemeColorOverride("font_outline_color", outlineColor);
		label.Resized += () => label.PivotOffset = label.Size * 0.5f;
		parent.AddChild(label);
		_sanguisBannerLabels.Add(label);
		return label;
	}

	private void BuildSanguisBanner(Control overlay)
	{
		_sanguisBannerLabels.Clear();
		_sanguisBanner = new Control
		{
			Name = "SanguisBanner",
			AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0.5f, AnchorBottom = 0.5f,
			OffsetTop = -SanguisBannerHeight * 0.5f, OffsetBottom = SanguisBannerHeight * 0.5f,
			MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = Colors.Transparent
		};
		overlay.AddChild(_sanguisBanner);
		var band = new ColorRect
		{
			Name = "Band", AnchorRight = 1, AnchorBottom = 1, Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = new Shader { Code = SanguisBandShader } }
		};
		_sanguisBanner.AddChild(band);

		var font = CreateSanguisFont();
		var clear = new Color(SanguisGlow, 0f);
		// Brilho: só os contornos aparecem (letra transparente), do mais largo e fraco ao mais estreito e forte.
		CreateSanguisLabel(_sanguisBanner, font, clear, 30, new Color(SanguisGlow, 0.05f));
		CreateSanguisLabel(_sanguisBanner, font, clear, 18, new Color(SanguisGlow, 0.09f));
		CreateSanguisLabel(_sanguisBanner, font, clear, 9, new Color(SanguisGlow, 0.16f));
		_sanguisBannerEchoes = new[]
		{
			CreateSanguisLabel(_sanguisBanner, font, new Color(SanguisGold, 0.55f), 4, new Color(SanguisGlow, 0.12f)),
			CreateSanguisLabel(_sanguisBanner, font, new Color(SanguisGold, 0.40f), 4, new Color(SanguisGlow, 0.10f))
		};
		_sanguisBannerText = CreateSanguisLabel(_sanguisBanner, font, SanguisGold, 2, new Color(0.10f, 0.03f, 0.0f, 0.85f));
	}

	// Entrada: faixa e título surgem com fade, o título assenta (1,08 → 1) e dois ecos crescem e se dissolvem.
	// Saída: tudo some com fade, o título se afasta levemente e um eco se expande junto.
	private void PlaySanguisBanner(float fadeIn, float hold, float fadeOut)
	{
		if (_sanguisBanner == null || !IsInstanceValid(_sanguisBanner)) return;
		_sanguisBannerTween?.Kill();
		var title = Localization.Text("hud.absorption.title");
		foreach (var label in _sanguisBannerLabels)
		{
			label.Text = title;
			label.PivotOffset = label.Size * 0.5f;
			label.Scale = Vector2.One;
		}
		foreach (var echo in _sanguisBannerEchoes)
			echo.Modulate = Colors.Transparent;
		_sanguisBanner.Modulate = Colors.Transparent;
		_sanguisBannerText.Scale = new Vector2(1.08f, 1.08f);

		var exitAt = fadeIn + hold;
		var tween = _sanguisBanner.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process).SetParallel();
		_sanguisBannerTween = tween;
		tween.TweenProperty(_sanguisBanner, "modulate", Colors.White, fadeIn).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(_sanguisBannerText, "scale", Vector2.One, fadeIn + 0.5f).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);

		for (var i = 0; i < _sanguisBannerEchoes.Length; i++)
		{
			var echo = _sanguisBannerEchoes[i];
			var start = fadeIn * 0.4f + i * 0.2f;
			var grow = 1.18f - i * 0.06f;
			tween.TweenProperty(echo, "modulate", Colors.White, 0.12f).SetDelay(start);
			tween.TweenProperty(echo, "scale", new Vector2(grow, grow), 1.1f).SetDelay(start).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
			tween.TweenProperty(echo, "modulate", Colors.Transparent, 0.95f).SetDelay(start + 0.15f).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
		}

		var exitEcho = _sanguisBannerEchoes[0];
		tween.TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(exitEcho)) return;
			exitEcho.Scale = Vector2.One;
			exitEcho.Modulate = new Color(1f, 1f, 1f, 0.7f);
		})).SetDelay(exitAt);
		tween.TweenProperty(exitEcho, "scale", new Vector2(1.14f, 1.14f), fadeOut + 0.2f).SetDelay(exitAt).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
		tween.TweenProperty(exitEcho, "modulate", Colors.Transparent, fadeOut + 0.2f).SetDelay(exitAt);
		tween.TweenProperty(_sanguisBannerText, "scale", new Vector2(1.04f, 1.04f), fadeOut).SetDelay(exitAt).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(_sanguisBanner, "modulate", Colors.Transparent, fadeOut).SetDelay(exitAt).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Sine);
	}

	// Margem interna na lista de cards: o anel de foco (expand 3 + brilho) do card não é cortado pelo ScrollContainer.
	// Usado também pela Loja e pela bancada.
	private static void PadChoiceCardScroll(ScrollContainer scroll, VBoxContainer list)
	{
		scroll.RemoveChild(list);
		var margin = new MarginContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
		foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
			margin.AddThemeConstantOverride(side, UiMetrics.FocusExpand + 6);
		margin.AddChild(list);
		scroll.AddChild(margin);
	}

	private void BuildBloodBlessingChoices()
	{
		var rng = new RandomNumberGenerator(); rng.Randomize();
		// Bênção de porta (RunState.TakeDeliveredBlessingPlan): as 3 opções que o jogador viu na porta, reamostradas
		// só nas que a run já ganhou ou que não são mais elegíveis. Sem plano (Fonte, início da run) e no reroll: sorteio normal.
		var guaranteeBasicAttack = RunState.RunBlessings.Count == 0;
		var planned = RunState.TakeDeliveredBlessingPlan();
		List<BloodBlessingOffer> choices;
		string family;
		if (planned != null)
		{
			choices = BloodBlessingCatalog.ReviewPlannedOffers(rng, planned.Offers, planned.Family, guaranteeBasicAttack, out var replacedCount);
			family = planned.Family;
			if (OS.IsDebugBuild())
				GD.Print($"Porta de Bênção: {replacedCount} de {planned.Offers.Count} opções reamostradas (linhagem {family}).");
		}
		else
			choices = BloodBlessingCatalog.RollOffers(rng, CompendiumTuning.GetInt("reward.blessing.choices", 3), out family, guaranteeBasicAttack);
		if (choices.Count == 0)
		{
			// Sem bênção nova para oferecer: o sangue vira Moedas no SALDO DA RUN (economy.coins.blessing_fallback, padrão 25).
			var fallbackCoins = CompendiumTuning.GetInt("economy.coins.blessing_fallback", EconomyDefaults.BlessingFallbackCoins);
			if (GetTree().GetFirstNodeInGroup("run_manager") is RunManager runManager)
				runManager.AddBloodCoins(fallbackCoins);
			else
			{
				RunState.AddRunBloodCoins(fallbackCoins);
				NotifyBloodCoinsRewarded(fallbackCoins, RunState.RunBloodCoins);
			}
			var fallbackMessage = Localization.TextFormat("offer.blessing.all_found", fallbackCoins);
			AppendGameLog(fallbackMessage);
			ShowNotificationToast(fallbackMessage, true);
			ClearBlessingCards();
			UiModalStack.Pop(_bloodBlessingOverlay);
			GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
			return;
		}
		PresentBlessingOffers(choices, family);
	}

	// Monta os cards; na primeira vez abre a janela (pausa sem parar a música, obrigatória), no reroll só troca os cards e refoca o mesmo índice.
	private void PresentBlessingOffers(IReadOnlyList<BloodBlessingOffer> choices, string family)
	{
		var wasOpen = UiModalStack.IsOpen(_bloodBlessingOverlay);
		var previousIndex = wasOpen ? _focusedBlessingChoice : 0;
		ClearBlessingCards();
		_blessingChoiceLocked = false;
		_blessingOfferFamily = family ?? "";
		ResetBlessingOfferStatus();

		foreach (var offer in choices)
		{
			var glossary = new List<BlessingKeyword>();
			var card = UiComponents.ChoiceCard(BuildBlessingCardData(offer, glossary), () => SelectBloodBlessing(offer));
			_bloodBlessingList.AddChild(card.Root);
			var choiceIndex = _blessingChoiceButtons.Count;
			card.ActionButton.FocusEntered += () =>
			{
				_focusedBlessingChoice = choiceIndex;
				UpdateBlessingGlossary(choiceIndex);
			};
			card.Root.MouseEntered += () => UpdateBlessingGlossary(choiceIndex);
			card.Root.MouseExited += () => UpdateBlessingGlossary(_focusedBlessingChoice);
			_blessingCards.Add(card);
			_blessingCardGlossaries.Add(glossary);
			_blessingChoiceButtons.Add(card.ActionButton);
		}
		SetBlessingChoiceNeighbors();
		UpdateBlessingRerollHint();

		_focusedBlessingChoice = Mathf.Clamp(previousIndex, 0, _blessingChoiceButtons.Count - 1);
		UpdateBlessingGlossary(_focusedBlessingChoice);
		if (wasOpen)
		{
			FocusBlessingChoice(_focusedBlessingChoice);
			return;
		}
		// Pausa a árvore, mas a música da sala continua tocando durante a escolha.
		UiModalStack.Push(_bloodBlessingOverlay, true, _blessingChoiceButtons[_focusedBlessingChoice], WarnBlessingOfferMandatory, mandatory: true, pausesAudio: false);
		_blessingWindow.FadeIn();
	}

	private ChoiceCardData BuildBlessingCardData(BloodBlessingOffer offer, List<BlessingKeyword> glossary)
	{
		var blessing = offer.Definition;
		var replaced = RunState.GetBlessingInExclusiveGroup(blessing.ExclusiveGroup);
		var description = FormatBloodBlessingDescription(blessing, offer.PowerMultiplier, true);
		CollectBlessingKeywords(description, glossary);
		return new ChoiceCardData
		{
			Icon = LoadBlessingIcon(blessing),
			Title = blessing.DisplayName,
			RarityId = offer.Rarity,
			Meta = FormatBlessingOfferMeta(blessing),
			BodyBbcode = FormatEffectKeywords(description),
			// Aviso de substituição sempre visível (não depende de hover).
			Warning = replaced != null
				? Localization.TextFormat("offer.blessing.replaces", replaced.DisplayName, replaced.Level)
				: null,
			ActionText = Localization.Text("ui.common.choose"),
			// Sem botão "Escolher" ao lado: o clique na linha escolhe.
			WholeCardAction = true,
			Width = 0
		};
	}

	// Meta do card: linhagem · ataque afetado · nível (nova N1, ou evolução N→N+1 se já estiver na run).
	private static string FormatBlessingOfferMeta(BloodBlessingDefinition blessing)
	{
		var parts = new List<string>
		{
			Localization.TextFormat("offer.blessing.lineage", FormatBlessingLineage(blessing))
		};
		var attacks = AffectedAttackLabels(blessing);
		if (attacks.Count > 0)
			parts.Add(string.Join(", ", attacks));
		var owned = RunState.RunBlessings.FirstOrDefault(record => record.Id.Equals(blessing.Id, StringComparison.OrdinalIgnoreCase));
		parts.Add(owned == null
			? Localization.Text("offer.blessing.level_new")
			: Localization.TextFormat("offer.blessing.level_up", owned.Level, owned.Level + 1));
		return string.Join("  ·  ", parts);
	}

	private static List<string> AffectedAttackLabels(BloodBlessingDefinition blessing)
	{
		var labels = new List<string>();
		foreach (var tag in blessing.Tags ?? Array.Empty<string>())
		{
			var label = tag?.ToLowerInvariant() switch
			{
				"basic_attack" => Localization.Text("ui.attack.basic"),
				"quick_attack" => Localization.Text("ui.attack.quick"),
				"special_attack" => Localization.Text("ui.attack.special"),
				"dash" => Localization.Text("ui.attack.dash"),
				_ => null
			};
			if (label != null && !labels.Contains(label))
				labels.Add(label);
		}
		return labels;
	}

	private static Texture2D LoadBlessingIcon(BloodBlessingDefinition blessing)
	{
		if (string.IsNullOrEmpty(blessing.IconPath) || !ResourceLoader.Exists(blessing.IconPath))
			return null;
		return ResourceLoader.Load<Texture2D>(blessing.IconPath);
	}

	// Refresh sem foco preso em nó morto (§5.1 regra 10): RemoveChild + QueueFree.
	private void ClearBlessingCards()
	{
		if (_bloodBlessingList != null)
			foreach (var child in _bloodBlessingList.GetChildren())
			{
				_bloodBlessingList.RemoveChild(child);
				child.QueueFree();
			}
		_blessingCards.Clear();
		_blessingCardGlossaries.Clear();
		_blessingChoiceButtons.Clear();
	}

	// Cima/baixo com wrap pelos vizinhos de foco; esquerda/direita andam para o card anterior/seguinte.
	private void SetBlessingChoiceNeighbors()
	{
		var items = _blessingChoiceButtons.Cast<Control>().ToList();
		ControllerUiSupport.SetVerticalCycle(items);
		foreach (var item in items)
		{
			item.FocusNeighborLeft = item.FocusNeighborTop;
			item.FocusNeighborRight = item.FocusNeighborBottom;
		}
	}

	// Painel lateral: uma caixa por termo do card indicado (nome na cor do destaque + explicação do termo).
	private void UpdateBlessingGlossary(int index)
	{
		if (_blessingTermPanel == null) return;
		foreach (var child in _blessingTermPanel.GetChildren())
		{
			_blessingTermPanel.RemoveChild(child);
			child.QueueFree();
		}
		var glossary = index >= 0 && index < _blessingCardGlossaries.Count ? _blessingCardGlossaries[index] : null;
		_blessingTermPanel.Visible = glossary != null && glossary.Count > 0;
		if (!_blessingTermPanel.Visible) return;
		// A caixa "abre" com um fade curto a cada troca de card.
		_blessingTermPanel.Modulate = new Color(1, 1, 1, 0);
		_blessingTermPanel.CreateTween().SetPauseMode(Tween.TweenPauseMode.Process)
			.TweenProperty(_blessingTermPanel, "modulate:a", 1.0f, UiMotion.OverlayIn);
		foreach (var keyword in glossary)
		{
			var box = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
			box.AddThemeStyleboxOverride("panel", UIThemeFactory.ControlBox(UiPalette.PanelInset, UiPalette.GoldDim, UiMetrics.BorderThin, 12, 10));
			var text = new RichTextLabel
			{
				BbcodeEnabled = true, FitContent = true, ScrollActive = false,
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Text = $"[color={keyword.Color}][b]{keyword.Term}[/b][/color]\n[color=#{UiPalette.TextSecondary.ToHtml(false)}]{keyword.Hint}[/color]"
			};
			box.AddChild(text);
			_blessingTermPanel.AddChild(box);
		}
	}

	private void ResetBlessingOfferStatus()
	{
		_blessingWindow.Status.RemoveThemeColorOverride("font_color");
		_blessingWindow.SetStatus(string.IsNullOrEmpty(_blessingOfferFamily)
			? Localization.Text("hud.blessing_choice")
			: Localization.TextFormat("offer.blessing.status", _blessingOfferFamily));
	}

	// ui_cancel na oferta obrigatória: não fecha; avisa no status (Warn) e mantém o foco no card.
	private void WarnBlessingOfferMandatory()
	{
		_blessingWindow.SetStatus(Localization.Text("offer.blessing.must_choose"));
		_blessingWindow.Status.AddThemeColorOverride("font_color", UiPalette.Warn);
		FocusBlessingChoice(_focusedBlessingChoice);
	}

	// Atalhos com a oferta no topo da pilha (HUD.RouteModalShortcut): ui_alt = Espelho do Destino.
	private bool HandleBlessingOfferShortcut(InputEvent input)
	{
		if (input.IsEcho() || !InputMap.HasAction("ui_alt") || !input.IsActionPressed("ui_alt")) return false;
		if (_blessingRerollHint != null && _blessingRerollHint.Visible && !_blessingRerollHint.Button.Disabled)
			RerollBlessingOffer();
		return true;
	}

	// "Escolher" do rodapé: só escolhe o card que tem o foco; sem card focado, foca o primeiro (não escolhe às cegas).
	private void ChooseFocusedBlessing()
	{
		if (_blessingCards.Count == 0 || UiModalStack.Top != _bloodBlessingOverlay) return;
		var focused = GetViewport().GuiGetFocusOwner();
		var index = focused is Button button ? _blessingChoiceButtons.IndexOf(button) : -1;
		if (index < 0)
		{
			FocusBlessingChoice(0);
			return;
		}
		_blessingCards[index].Choose();
	}

	private void FocusBlessingChoice(int index)
	{
		if (_blessingChoiceButtons.Count == 0) return;
		_focusedBlessingChoice = Mathf.Clamp(index, 0, _blessingChoiceButtons.Count - 1);
		_blessingChoiceButtons[_focusedBlessingChoice].GrabFocus();
	}

	// Reroll = skill "Espelho do Destino" (RewardOfferReroll): sem custo em moedas, 1 uso por mapa (RunState).
	// Sem a skill a dica some; usada no mapa, fica visível e desabilitada com "(0)".
	private void UpdateBlessingRerollHint()
	{
		if (_blessingRerollHint == null) return;
		var hasSkill = _blessingOfferPreview || HasRewardRerollSkill();
		_blessingRerollHint.Visible = hasSkill;
		if (!hasSkill) return;
		var remaining = _blessingOfferPreview || (GetPlayerSkillController()?.CanRerollReward() ?? false) ? 1 : 0;
		_blessingRerollHint.SetLabel(Localization.TextFormat("offer.reroll_count", remaining));
		_blessingRerollHint.Button.Disabled = remaining == 0;
		_blessingRerollHint.Button.TooltipText = remaining > 0
			? Localization.Text("offer.reroll_tooltip")
			: Localization.Text("offer.reroll_used");
	}

	// Só leitura do catálogo/ficha: distingue "sem a skill" (oculta) de "usada neste mapa" (desabilitada).
	private static bool HasRewardRerollSkill()
	{
		foreach (var skillId in CharacterSheet.AcquiredSkills)
		{
			if (!SkillCatalog.TryGet(skillId, out var skill) || !skill.IsImplemented) continue;
			foreach (var effect in skill.Effects)
				if (effect.Type.Equals("RewardOfferReroll", StringComparison.OrdinalIgnoreCase))
					return true;
		}
		return false;
	}

	private void RerollBlessingOffer()
	{
		if (_blessingChoiceLocked || UiModalStack.Top != _bloodBlessingOverlay) return;
		if (!_blessingOfferPreview && GetPlayerSkillController()?.TryConsumeRewardReroll() != true) return;
		BuildBloodBlessingChoices();
		if (UiModalStack.IsOpen(_bloodBlessingOverlay))
			_blessingWindow.SetStatus(Localization.Text("offer.blessing.rerolled"));
	}

	private PlayerSkillController GetPlayerSkillController()
	{
		return _runtimePlayerController?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController")
			?? GetTree().GetFirstNodeInGroup("player")?.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
	}

	private static string FormatBlessingLineage(BloodBlessingDefinition blessing)
	{
		return blessing.ParentFamilies.Length > 0
			? $"{blessing.Family}: {string.Join(" + ", blessing.ParentFamilies)}"
			: blessing.Family;
	}

	private void SelectBloodBlessing(BloodBlessingOffer offer)
	{
		// Só escolhe com a oferta no topo (nada de escolha às cegas com outra tela por cima).
		if (_blessingChoiceLocked || UiModalStack.Top != _bloodBlessingOverlay) return;
		var blessing = offer.Definition;
		if (!RunState.AddBloodBlessing(offer)) return;
		_blessingChoiceLocked = true;
		GetTree().CallGroup("player", "ApplyRuntimeTuning");
		GetTree().CallGroup("audio_manager", "PlayEffect", "blessing_select");
		AppendGameLog($"Bênção adquirida: {blessing.DisplayName} [{offer.Rarity}].");
		ShowNotificationToast(Localization.TextFormat("hud.toast.blessing", UiComponents.RarityLabel(offer.Rarity), blessing.DisplayName), true, LoadBlessingIcon(blessing));
		// Os cards ficam até a próxima oferta (removê-los aqui seria no meio do sinal do próprio botão/card).
		UiModalStack.Pop(_bloodBlessingOverlay);
		RefreshCharacterSheet();
		RefreshBlessingHud();
		GetTree().CallGroup("run_manager", "UnlockExitAfterReward");
	}

	// Preview (debug, --ui-preview=blessing_offer | blessing_offer_replace). Nada é salvo; a run é volátil.
	internal void PreviewBlessingOffer(bool withReplacement)
	{
		_blessingOfferPreview = true;
		if (!withReplacement)
		{
			OpenMandatoryBloodBlessingOffer();
			return;
		}
		// Uma bênção do grupo exclusivo já na run e três do mesmo grupo na oferta: todos os cards mostram "Substitui".
		var group = BloodBlessingCatalog.All
			.Where(definition => !string.IsNullOrWhiteSpace(definition.ExclusiveGroup))
			.GroupBy(definition => definition.ExclusiveGroup)
			.OrderByDescending(entries => entries.Count())
			.FirstOrDefault()?.ToList();
		if (group == null || group.Count < 2)
		{
			OpenMandatoryBloodBlessingOffer();
			return;
		}
		if (RunState.GetBlessingInExclusiveGroup(group[0].ExclusiveGroup) == null)
			RunState.AddBloodBlessing(group[0]);
		var rarities = BloodBlessingCatalog.AllRarities;
		var offers = new List<BloodBlessingOffer>();
		for (var i = 1; i < group.Count && offers.Count < 3; i++)
		{
			var rarity = rarities.Count > 0 ? rarities[(i * 2) % rarities.Count] : new BloodBlessingRarity("common", "Common", 1.0f, 1.0f);
			offers.Add(new BloodBlessingOffer(group[i], rarity.DisplayName, rarity.PowerMultiplier));
		}
		PresentBlessingOffers(offers, group[1].Family);
	}

	// Montagem do texto: BloodBlessingText (scripts/systems/blessings/BloodBlessingDefinition.cs). O modelo vem de
	// content.json (blessing.<id>.desc) com {n} = valor do efeito n e marcação [kw=<id>]termo[/kw] para as palavras-chave.
	// highlightValues: números em negrito Gold e marcação preservada (oferta/ficha/compêndio, BBCode). Sem destaque, texto puro (tooltips).
	private static string FormatBloodBlessingDescription(BloodBlessingDefinition blessing, float effectivePowerMultiplier, bool highlightValues = false)
	{
		return BloodBlessingText.FormatDescription(blessing, effectivePowerMultiplier, highlightValues ? UiPalette.Gold.ToHtml(false) : null);
	}

	// Marcação [kw=...] -> BBCode colorido. Sem [hint]: o texto do card não recebe mouse e o glossário da oferta já explica o termo (§8.17).
	private static string FormatEffectKeywords(string text) => BloodBlessingText.FormatKeywords(text);

	private static float GetDisplayedBlessingValue(BloodBlessingEffectDefinition effect, float value) => BloodBlessingText.GetDisplayedValue(effect, value);

	// Número com até 2 casas e o separador decimal do idioma atual.
	private static string FormatLocalizedNumber(float value) => ArtifactEffectRegistry.FormatNumber(value);

	// Termos marcados no texto, em ordem de aparição e sem repetir (glossário da oferta).
	private static void CollectBlessingKeywords(string text, List<BlessingKeyword> glossary) => BloodBlessingText.CollectKeywords(text, glossary);
}
