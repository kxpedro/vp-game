using Godot;
using System.Threading.Tasks;

public partial class PlayerCombat : Node
{
	[Signal]
	public delegate void FuryChangedEventHandler(float currentFury, float maxFury, bool isActive);

	[Export] public NodePath AttackAreaPath { get; set; } = new("../AttackArea");
	[Export] public NodePath AttackShapePath { get; set; } = new("../AttackArea/CollisionShape3D");
	[Export] public float ComboResetTime { get; set; } = 0.9f;
	[Export] public float MaxFury { get; set; } = 100.0f;
	[Export] public float FuryDuration { get; set; } = 6.0f;
	[Export] public float FuryDamageMultiplier { get; set; } = 1.75f;
	[Export] public float AutoAimRadius { get; set; } = 4.25f;
	[Export] public float DashAttackDamageMultiplier { get; set; } = 1.35f;
	[Export] public float AttackArcDegrees { get; set; } = 118.0f;
	[Export] public float HitboxEdgeForgiveness { get; set; } = 1.15f;

	public float CurrentFury { get; private set; }
	public bool IsFuryActive { get; private set; }
	public bool IsAttackFacingLocked => _isAttacking;
	public bool IsDashAttacking { get; private set; }
	public Vector3 AttackFacingDirection => _attackDirection;

	private Area3D _attackArea;
	private CollisionShape3D _attackShape;
	private BoxShape3D _attackBox;
	private PlayerController _controller;
	private Health _health;
	private PlayerAnimator _animator;
	private BloodBlessingRuntime _blessings;
	private bool _isAttacking;
	private bool _queuedLightAttack;
	private bool _queuedDashAttack;
	private int _lightComboIndex;
	private float _comboResetTimer;
	private float _baseMaxFury = 100.0f;
	private Vector3 _attackDirection = Vector3.Forward;

	private WeaponDefinition EquippedWeapon => WeaponLoadout.EquippedWeapon;
	private WeaponAttackDefinition[] LightCombo => EquippedWeapon.LightCombo;

	public override void _Ready()
	{
		_baseMaxFury = MaxFury;
		MaxFury = GetTunedMaxFury();
		_attackArea = GetNode<Area3D>(AttackAreaPath);
		_attackShape = GetNode<CollisionShape3D>(AttackShapePath);
		_attackBox = _attackShape.Shape as BoxShape3D;
		var player = GetParent<PlayerController>();
		_controller = player;
		_health = player.GetNodeOrNull<Health>("Health");
		_animator = player.GetNodeOrNull<PlayerAnimator>("PlayerAnimator");
		_blessings = player.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
	}

	public override void _Process(double delta)
	{
		TickComboReset((float)delta);

		if (Input.IsActionJustPressed("attack_light"))
			BeginLightAttack();
		if (Input.IsActionJustPressed("attack_heavy"))
			BeginHeavyAttack();
	}

	public void AddFury(float amount)
	{
		if (IsFuryActive)
			return;

		MaxFury = GetTunedMaxFury();
		CurrentFury = Mathf.Min(CurrentFury + amount * CharacterSheet.GetBloodcraftMultiplier(), MaxFury);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
	}

	private void BeginLightAttack()
	{
		if (_controller != null && _controller.IsDodging)
		{
			_queuedDashAttack = true;
			return;
		}

		if (_controller != null && _controller.CanDashAttack)
		{
			BeginDashAttack(_controller.DodgeDirection);
			return;
		}

		if (_isAttacking)
		{
			_queuedLightAttack = true;
			return;
		}

		BeginAttack(GetTunedAttack(LightCombo[_lightComboIndex], $"weapon.{EquippedWeapon.Id}.light_{_lightComboIndex + 1}"), false);
	}

	private void BeginHeavyAttack()
	{
		if (_controller != null && _controller.IsDodging)
		{
			_queuedDashAttack = true;
			return;
		}

		if (_controller != null && _controller.CanDashAttack)
		{
			BeginDashAttack(_controller.DodgeDirection);
			return;
		}

		if (_isAttacking)
			return;

		_queuedLightAttack = false;
		_lightComboIndex = 0;
		BeginAttack(GetTunedAttack(EquippedWeapon.HeavyAttack, $"weapon.{EquippedWeapon.Id}.heavy"), true);
	}

	public bool TryReleaseQueuedDashAttack(Vector3 dashDirection)
	{
		if (!_queuedDashAttack)
			return false;

		_queuedDashAttack = false;
		_queuedLightAttack = false;
		_lightComboIndex = 0;
		BeginDashAttack(dashDirection);
		return true;
	}

	private async void BeginAttack(WeaponAttackDefinition attack, bool isHeavy)
	{
		if (_isAttacking)
			return;

		_isAttacking = true;
		_attackDirection = GetMouseAttackDirection();
		FaceAttackDirection();
		ConfigureAttackArea(attack);
		if (isHeavy)
			_animator?.PlayHeavyAttack();
		else
			_animator?.PlayLightAttack(_lightComboIndex);

		var attackSpeed = GetCurrentAttackSpeedMultiplier();
		var hitDelay = Mathf.Max((attack.HitDelay * GameTuning.GetAttackDelayMultiplier()) / attackSpeed, 0.01f);
		await Wait(hitDelay);
		if (!IsInsideTree())
			return;

		SpawnAttackSlash(attack, isHeavy);
		var currencyDamage = RunState.GetEffectTotal("DamagePerCurrencyHeld.Multiplier") * CharacterSheet.BloodCoins;
		var roomDamage = RunState.GetEffectTotal("DamageUntilRoomClear.Multiplier");
		var damageMultiplier = CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier * (1.0f + currencyDamage + roomDamage + RunState.ContextDamageBonus) * GameTuning.GetDamageMultiplier();
		var furyDamageMultiplier = CompendiumTuning.GetFloat("attack.fury.damage_multiplier", FuryDamageMultiplier);
		if (IsFuryActive) furyDamageMultiplier *= CharacterSheet.GetBloodcraftMultiplier();
		var hit = DealDamage(attack.Damage * damageMultiplier * (IsFuryActive ? furyDamageMultiplier : 1.0f), attack.Knockback, attack);
		CombatFeedback.Instance?.PlayPlayerHit(hit.TargetsHit, hit.IsCritical, isHeavy || _lightComboIndex == LightCombo.Length - 1);

		var recoveryDelay = Mathf.Max((attack.Recovery * GameTuning.GetAttackDelayMultiplier()) / GetCurrentAttackSpeedMultiplier(), 0.01f);
		await Wait(recoveryDelay);

		_isAttacking = false;
		_comboResetTimer = ComboResetTime;

		if (isHeavy)
		{
			_lightComboIndex = 0;
			return;
		}

		_lightComboIndex = (_lightComboIndex + 1) % LightCombo.Length;

		if (_queuedLightAttack)
		{
			_queuedLightAttack = false;
			BeginLightAttack();
		}
	}

	private async void BeginDashAttack(Vector3 dashDirection)
	{
		if (_isAttacking)
			return;

		_isAttacking = true;
		IsDashAttacking = true;
		_controller?.ConsumeDashAttackGrace();
		_health?.SetInvincible(true);
		_attackDirection = GetMouseAttackDirection(dashDirection);
		FaceAttackDirection();

		var attack = GetTunedAttack(EquippedWeapon.DashAttack, $"weapon.{EquippedWeapon.Id}.dash");
		ConfigureAttackArea(attack);
		_animator?.PlayDashAttack();

		var damageMultiplier = CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier * GameTuning.GetDamageMultiplier();
		var furyDamageMultiplier = CompendiumTuning.GetFloat("attack.fury.damage_multiplier", FuryDamageMultiplier);
		if (IsFuryActive) furyDamageMultiplier *= CharacterSheet.GetBloodcraftMultiplier();
		var dashDamageMultiplier = CompendiumTuning.GetFloat("attack.dash.damage_multiplier", DashAttackDamageMultiplier);
		SpawnDashAttackCircle(attack);
		var dashHits = DealCircularDamage(attack.Damage * dashDamageMultiplier * damageMultiplier * (IsFuryActive ? furyDamageMultiplier : 1.0f), attack.Knockback, attack.Range);
		CombatFeedback.Instance?.PlayPlayerHit(dashHits, false, true);

		var recoveryDelay = Mathf.Max((attack.Recovery * GameTuning.GetAttackDelayMultiplier()) / Mathf.Max(GameTuning.GetAttackSpeedMultiplier(), 0.01f), 0.01f);
		await Wait(recoveryDelay);

		_health?.SetInvincible(false);
		IsDashAttacking = false;
		_isAttacking = false;
		_comboResetTimer = ComboResetTime;
		_lightComboIndex = 0;
	}

	private HitResult DealDamage(float damage, float knockback, WeaponAttackDefinition attack)
	{
		var player = GetParent<Node3D>();
		var halfArcRadians = Mathf.DegToRad(AttackArcDegrees * 0.5f);
		var criticalChance = Mathf.Clamp(RunState.GetEffectTotal("CriticalChance.Flat") + RunState.ContextCriticalChance + (_blessings?.BonusCriticalChance() ?? 0.0f), 0, 1);
		var isCritical = RunState.ConsumeGuaranteedCritical() || GD.Randf() < criticalChance;
		var criticalMultiplier = 1.5f + RunState.GetEffectTotal("CriticalDamage.Multiplier") + RunState.ContextCriticalDamage;
		var nonCriticalMultiplier = 1.0f + RunState.GetEffectTotal("NonCriticalDamage.Multiplier");
		var targetsHit = 0;

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemyNode)
				continue;

			var toEnemy = enemyNode.GlobalPosition - player.GlobalPosition;
			toEnemy.Y = 0.0f;

			var distance = toEnemy.Length();
			var targetRadius = enemyNode is EnemyBasic target ? target.HitTargetRadius : 0.35f;
			if (distance <= 0.001f || distance > attack.Range + HitboxEdgeForgiveness + targetRadius)
				continue;

			var angle = Mathf.Acos(Mathf.Clamp(toEnemy.Normalized().Dot(_attackDirection), -1.0f, 1.0f));
			var targetAngle = Mathf.Asin(Mathf.Clamp(targetRadius / Mathf.Max(distance, targetRadius), 0.0f, 1.0f));
			if (angle > halfArcRadians + targetAngle)
				continue;

			var targetHealth = enemyNode.GetNodeOrNull<Health>("Health");
			if (targetHealth == null) continue;
			var finalDamage = damage * (isCritical ? criticalMultiplier : nonCriticalMultiplier);
			if (enemyNode is EnemyBasic special)
			{
				finalDamage *= special.DamageTakenMultiplier;
				if (special.Rarity != EnemyRarity.Normal) finalDamage *= 1.0f + RunState.GetEffectTotal("DamageVsElite.Multiplier");
				if (special.Name.ToString().Contains("Boss")) finalDamage *= 1.0f + RunState.GetEffectTotal("DamageVsBoss.Multiplier");
			}
			if (enemyNode is EnemyBasic blessingTarget)
				finalDamage = _blessings?.ModifyOutgoingDamage(finalDamage, blessingTarget) ?? finalDamage;
			var executeThreshold = RunState.GetEffectTotal("ExecuteThreshold.Flat") + (isCritical ? RunState.GetEffectTotal("ExecuteThresholdOnCritical.Flat") : 0);
			if (executeThreshold > 0 && targetHealth.CurrentHealth / targetHealth.MaxHealth <= executeThreshold) finalDamage = targetHealth.CurrentHealth;
			targetHealth.ApplyDamage(finalDamage, isCritical);
			if (enemyNode is EnemyBasic hitEnemy)
				_blessings?.OnPlayerHit(hitEnemy, finalDamage, isCritical);
			targetsHit++;
			if (isCritical)
			{
				_health?.Heal(finalDamage * (RunState.GetEffectTotal("CriticalLifeSteal.Multiplier") + RunState.ContextLifeSteal));
				GetTree().CallGroup("player_artifacts", "NotifyCriticalHit");
			}
			if (enemyNode is EnemyBasic enemy)
				enemy.ApplyKnockback(enemyNode.GlobalPosition - player.GlobalPosition, knockback);
		}

		return new HitResult(targetsHit, isCritical);
	}

	private int DealCircularDamage(float damage, float knockback, float radius)
	{
		var player = GetParent<Node3D>();
		var targetsHit = 0;

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemyNode)
				continue;

			var toEnemy = enemyNode.GlobalPosition - player.GlobalPosition;
			toEnemy.Y = 0.0f;

			var targetRadius = enemyNode is EnemyBasic target ? target.HitTargetRadius : 0.35f;
			if (toEnemy.Length() > radius + HitboxEdgeForgiveness + targetRadius)
				continue;

			var finalDamage = enemyNode is EnemyBasic blessingTarget ? _blessings?.ModifyOutgoingDamage(damage, blessingTarget) ?? damage : damage;
			enemyNode.GetNodeOrNull<Health>("Health")?.ApplyDamage(finalDamage);
			if (enemyNode is EnemyBasic hitEnemy) _blessings?.OnPlayerHit(hitEnemy, finalDamage, false);
			targetsHit++;
			if (enemyNode is EnemyBasic enemy)
				enemy.ApplyKnockback(toEnemy.LengthSquared() > 0.001f ? toEnemy : _attackDirection, knockback);
		}

		return targetsHit;
	}

	private float GetCurrentAttackSpeedMultiplier()
	{
		var missingHealth = _health != null && _health.MaxHealth > 0 ? 1.0f - _health.CurrentHealth / _health.MaxHealth : 0.0f;
		var artifactSpeed = 1.0f + RunState.GetEffectTotal("AttackSpeed.Multiplier")
			+ RunState.GetEffectTotal("AttackSpeedMissingHealth.Multiplier") * missingHealth
			+ RunState.ContextAttackSpeed + (_blessings?.BonusAttackSpeed() ?? 0.0f);
		return Mathf.Max(GameTuning.GetAttackSpeedMultiplier() * artifactSpeed, 0.01f);
	}

	private readonly record struct HitResult(int TargetsHit, bool IsCritical);

	private void ConfigureAttackArea(WeaponAttackDefinition attack)
	{
		_attackArea.Position = new Vector3(0.0f, 0.75f, -0.45f - attack.Range * 0.5f);

		if (_attackBox != null)
			_attackBox.Size = new Vector3(attack.Width, 1.1f, attack.Range);
	}

	private async void SpawnAttackSlash(WeaponAttackDefinition attack, bool isHeavy)
	{
		var player = GetParent<Node3D>();
		var fillMaterial = new StandardMaterial3D
		{
			AlbedoColor = isHeavy ? new Color(1.0f, 0.02f, 0.01f, 0.24f) : new Color(0.95f, 0.08f, 0.04f, 0.20f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = isHeavy ? 1.4f : 1.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var edgeMaterial = new StandardMaterial3D
		{
			AlbedoColor = isHeavy ? new Color(1.0f, 0.05f, 0.03f, 0.88f) : new Color(1.0f, 0.16f, 0.08f, 0.74f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = isHeavy ? 2.8f : 2.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		var indicator = new Node3D
		{
			Name = "AttackHitboxIndicator"
		};
		player.AddChild(indicator);

		var visualRange = attack.Range + HitboxEdgeForgiveness;
		AddArcMesh(indicator, "ArcFill", visualRange, AttackArcDegrees, fillMaterial, 0.075f);
		AddArcMesh(indicator, "ArcEdge", visualRange, AttackArcDegrees, edgeMaterial, 0.12f, true);

		var tween = indicator.CreateTween();
		indicator.Scale = new Vector3(0.9f, 1.0f, 0.85f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.05f, 1.0f, 1.0f), 0.08f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.12f, 1.0f, 1.06f), 0.06f);
		await ToSignal(tween, Tween.SignalName.Finished);
		indicator.QueueFree();
	}

	private async void SpawnDashAttackCircle(WeaponAttackDefinition attack)
	{
		var player = GetParent<Node3D>();
		var fillMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.02f, 0.01f, 0.22f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = 1.8f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var ringMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(1.0f, 0.08f, 0.04f, 0.86f),
			EmissionEnabled = true,
			Emission = new Color(1.0f, 0.0f, 0.0f),
			EmissionEnergyMultiplier = 3.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		var indicator = new Node3D
		{
			Name = "DashAttackCircleIndicator"
		};
		player.AddChild(indicator);

		var visualRadius = attack.Range + HitboxEdgeForgiveness;
		var fill = new MeshInstance3D
		{
			Name = "CircleFill",
			Mesh = new CylinderMesh
			{
				TopRadius = visualRadius,
				BottomRadius = visualRadius,
				Height = 0.035f,
				RadialSegments = 64
			},
			MaterialOverride = fillMaterial,
			Position = new Vector3(0.0f, 0.08f, 0.0f)
		};
		indicator.AddChild(fill);

		var ring = new MeshInstance3D
		{
			Name = "CircleRing",
			Mesh = new TorusMesh
			{
				InnerRadius = Mathf.Max(visualRadius - 0.08f, 0.01f),
				OuterRadius = visualRadius,
				RingSegments = 96
			},
			MaterialOverride = ringMaterial,
			Position = new Vector3(0.0f, 0.12f, 0.0f)
		};
		indicator.AddChild(ring);

		var pulse = new MeshInstance3D
		{
			Name = "PulseCore",
			Mesh = new CylinderMesh
			{
				TopRadius = visualRadius * 0.32f,
				BottomRadius = visualRadius * 0.32f,
				Height = 0.07f,
				RadialSegments = 48
			},
			MaterialOverride = ringMaterial,
			Position = new Vector3(0.0f, 0.16f, 0.0f)
		};
		indicator.AddChild(pulse);

		var tween = indicator.CreateTween();
		indicator.Scale = new Vector3(0.72f, 1.0f, 0.72f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.08f, 1.0f, 1.08f), 0.08f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.16f, 1.0f, 1.16f), 0.06f);
		await ToSignal(tween, Tween.SignalName.Finished);
		indicator.QueueFree();
	}

	private void AddHitboxBox(Node3D parent, string name, Vector3 size, Vector3 position, Material material, Vector3? rotation = null)
	{
		var box = new MeshInstance3D
		{
			Name = name,
			Mesh = new BoxMesh { Size = size },
			MaterialOverride = material,
			Position = position,
			Rotation = rotation ?? Vector3.Zero
		};
		parent.AddChild(box);
	}

	private void AddArcMesh(Node3D parent, string name, float radius, float arcDegrees, Material material, float height, bool edgeOnly = false)
	{
		var segmentCount = 28;
		var halfArc = Mathf.DegToRad(arcDegrees * 0.5f);
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);
		surfaceTool.SetMaterial(material);

		var innerRadius = edgeOnly ? Mathf.Max(radius - 0.14f, 0.01f) : 0.0f;
		for (var i = 0; i < segmentCount; i++)
		{
			var a0 = Mathf.Lerp(-halfArc, halfArc, i / (float)segmentCount);
			var a1 = Mathf.Lerp(-halfArc, halfArc, (i + 1) / (float)segmentCount);
			var outer0 = ArcPoint(radius, a0, height);
			var outer1 = ArcPoint(radius, a1, height);
			var inner0 = ArcPoint(innerRadius, a0, height);
			var inner1 = ArcPoint(innerRadius, a1, height);

			AddTriangle(surfaceTool, inner0, outer0, outer1);
			AddTriangle(surfaceTool, inner0, outer1, inner1);
		}

		parent.AddChild(new MeshInstance3D
		{
			Name = name,
			Mesh = surfaceTool.Commit()
		});
	}

	private Vector3 ArcPoint(float radius, float angle, float height)
	{
		return new Vector3(Mathf.Sin(angle) * radius, height, -Mathf.Cos(angle) * radius);
	}

	private void AddTriangle(SurfaceTool surfaceTool, Vector3 a, Vector3 b, Vector3 c)
	{
		surfaceTool.AddVertex(a);
		surfaceTool.AddVertex(b);
		surfaceTool.AddVertex(c);
	}

	public bool TryActivateFury()
	{
		MaxFury = GetTunedMaxFury();
		if (IsFuryActive || CurrentFury < MaxFury)
			return false;

		ActivateFury();
		return true;
	}

	private async void ActivateFury()
	{
		IsFuryActive = true;
		CurrentFury = 0.0f;
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);

		var duration = CompendiumTuning.GetFloat("attack.fury.duration", FuryDuration);
		await Wait(duration);

		IsFuryActive = false;
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
	}

	private async Task Wait(float seconds)
	{
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
	}

	private void TickComboReset(float delta)
	{
		if (_isAttacking || _lightComboIndex == 0)
			return;

		_comboResetTimer -= delta;
		if (_comboResetTimer <= 0.0f)
		{
			_lightComboIndex = 0;
			_queuedLightAttack = false;
		}
	}

	private Vector3 GetMouseAttackDirection(Vector3? fallbackDirection = null)
	{
		var player = GetParent<Node3D>();
		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return NormalizeFallback(fallbackDirection);

		var mousePosition = GetViewport().GetMousePosition();
		var rayOrigin = camera.ProjectRayOrigin(mousePosition);
		var rayDirection = camera.ProjectRayNormal(mousePosition);

		if (Mathf.Abs(rayDirection.Y) <= 0.001f)
			return NormalizeFallback(fallbackDirection);

		var t = (player.GlobalPosition.Y - rayOrigin.Y) / rayDirection.Y;
		if (t <= 0.0f)
			return NormalizeFallback(fallbackDirection);

		var target = rayOrigin + rayDirection * t;
		var direction = target - player.GlobalPosition;
		direction.Y = 0.0f;

		return direction.LengthSquared() > 0.001f
			? direction.Normalized()
			: NormalizeFallback(fallbackDirection);
	}

	private Vector3 NormalizeFallback(Vector3? fallbackDirection)
	{
		if (fallbackDirection.HasValue && fallbackDirection.Value.LengthSquared() > 0.001f)
			return fallbackDirection.Value.Normalized();

		return GetFacingDirection();
	}

	private Vector3 GetFacingDirection()
	{
		var player = GetParent<Node3D>();
		var forward = -player.GlobalTransform.Basis.Z;
		forward.Y = 0.0f;

		if (forward.LengthSquared() > 0.001f)
			return forward.Normalized();

		return _controller?.LastMoveDirection ?? Vector3.Forward;
	}

	private void FaceAttackDirection()
	{
		var player = GetParent<Node3D>();
		if (_attackDirection.LengthSquared() > 0.001f)
			player.LookAt(player.GlobalPosition + _attackDirection, Vector3.Up);
	}

	private WeaponAttackDefinition GetTunedAttack(WeaponAttackDefinition attack, string keyPrefix)
	{
		return new WeaponAttackDefinition(
			attack.Name,
			CompendiumTuning.GetFloat($"{keyPrefix}.damage", attack.Damage),
			CompendiumTuning.GetFloat($"{keyPrefix}.hit_delay", attack.HitDelay),
			CompendiumTuning.GetFloat($"{keyPrefix}.recovery", attack.Recovery),
			CompendiumTuning.GetFloat($"{keyPrefix}.knockback", attack.Knockback),
			CompendiumTuning.GetFloat($"{keyPrefix}.range", attack.Range) * GameTuning.GetAttackRangeMultiplier(),
			CompendiumTuning.GetFloat($"{keyPrefix}.width", attack.Width),
			attack.SlashTilt
		);
	}

	private float GetTunedMaxFury()
	{
		return CompendiumTuning.GetFloat("attack.fury.max_fury", _baseMaxFury) * CharacterSheet.GetFuryMultiplier();
	}

}
