using Godot;
using System.Threading.Tasks;

public partial class PlayerCombat : Node
{
	[Signal]
	public delegate void FuryChangedEventHandler(float currentFury, float maxFury, bool isActive);
	[Signal]
	public delegate void AmmoChangedEventHandler(int currentAmmo, int maxAmmo, bool isReloading);

	[Export] public NodePath AttackAreaPath { get; set; } = new("../AttackArea");
	[Export] public NodePath AttackShapePath { get; set; } = new("../AttackArea/CollisionShape3D");
	public const float DefaultFuryDrainPerSecond = 10.0f;
	// "Alcance" passado a OnSpecialAttack pelo tiro especial: poca de sangue com raio ~1,65 no ponto do impacto.
	private const float RangedSpecialPoolRange = 3.0f;
	[Export] public float ComboResetTime { get; set; } = 1.0f;
	[Export] public float QuickAttackInputBufferSeconds { get; set; } = 0.32f;
	[Export] public float MaxFury { get; set; } = 100.0f;
	[Export] public float FuryDrainPerSecond { get; set; } = DefaultFuryDrainPerSecond;
	[Export] public float FuryEndBurstDamageRatio { get; set; } = 0.15f;
	[Export] public float FuryEndBurstRadius { get; set; } = 3.5f;
	[Export] public float FuryDamageMultiplier { get; set; } = 1.75f;
	[Export] public float AutoAimRadius { get; set; } = 4.25f;
	public const float DefaultAimPlaneHeight = 0.8f;
	public const float DefaultEnemyPickHeight = 1.6f;
	public const float DefaultPlayerHurtRadius = 0.35f;

	/// <summary>Raio do corpo do jogador para os golpes inimigos (igual ao da cápsula): golpes de área, linhas e projéteis
	/// acertam quando encostam no corpo, não só quando cobrem o centro dos pés.</summary>
	public static float PlayerHurtRadius => Mathf.Max(CompendiumTuning.GetFloat("combat.player.hurt_radius", DefaultPlayerHurtRadius), 0.0f);

	public float CurrentFury { get; private set; }
	public bool IsFuryActive { get; private set; }
	public bool IsAttacking => _isAttacking;
	public bool IsFuryUnlocked => RunState.GetEffectTotal("UnlockBestialFury.Flat") > 0.0f;
	public bool IsAttackFacingLocked => _isAttacking;
	public bool IsQuickAttacking { get; private set; }
	public Vector3 AttackFacingDirection => _attackDirection;
	public PlayerAttackType CurrentAttackType { get; private set; } = PlayerAttackType.Basic;
	public PlayerAttackPhase CurrentAttackPhase { get; private set; } = PlayerAttackPhase.None;
	public float CurrentMovementMultiplier => CurrentAttackPhase switch
	{
		PlayerAttackPhase.Startup => _currentControl.StartupMovementMultiplier,
		PlayerAttackPhase.Recovery => _currentControl.RecoveryMovementMultiplier,
		_ => 1.0f
	};
	public string EquippedSpecialName => EquippedWeapon.SpecialAttack.Name;
	// Municao da arma equipada (so armas com MagazineSize > 0, ex.: Revolver Duplo).
	public bool UsesAmmo => MaxAmmo > 0;
	public int MaxAmmo => EquippedWeapon.TunedMagazineSize > 0
		? Mathf.Max(EquippedWeapon.TunedMagazineSize + Mathf.RoundToInt(RunState.GetWeaponModTotal("Ammo.Flat")), 1)
		: 0;
	public int CurrentAmmo => _ammo;
	public bool IsReloading => _reloadTimer > 0.0f;

	private Area3D _attackArea;
	private CollisionShape3D _attackShape;
	private BoxShape3D _attackBox;
	private PlayerController _controller;
	private Health _health;
	private PlayerAnimator _animator;
	private BloodBlessingRuntime _blessings;
	private PlayerSkillController _skills;
	private bool _isAttacking;
	private bool _queuedQuickAttack;
	private int _lightComboIndex;
	private float _comboResetTimer;
	private float _lightInputBufferTimer;
	private float _quickAttackInputBufferTimer;
	private float _specialInputBufferTimer;
	private float _specialAttackCooldownTimer;
	private float _attackPhaseElapsed;
	private float _attackPhaseDuration;
	private float _baseMaxFury = 100.0f;
	private Vector3 _attackDirection = Vector3.Forward;
	private int _attackGeneration;
	private AttackControlProfile _currentControl = AttackControlProfile.Default;
	private Node3D _attackPreview;
	private CanvasLayer _furyVisualLayer;
	private ColorRect _furyVisualOverlay;
	private int _ammo;
	private float _reloadTimer;
	private string _ammoWeaponId = "";
	private int _lastEmittedMaxAmmo = -1;

	private WeaponDefinition EquippedWeapon => WeaponLoadout.EquippedWeapon;
	private WeaponAttackDefinition[] BasicCombo => EquippedWeapon.BasicCombo;
	private bool IsComboFinisher => EquippedWeapon.HasComboFinisher && _lightComboIndex == BasicCombo.Length - 1;

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
		_skills = player.GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
		CreateFuryVisualOverlay();
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
		RefreshAmmoForEquippedWeapon();
	}

	public override void _Process(double delta)
	{
		var elapsed = (float)delta;
		TickComboReset(elapsed);
		_lightInputBufferTimer = Mathf.Max(_lightInputBufferTimer - elapsed, 0.0f);
		_quickAttackInputBufferTimer = Mathf.Max(_quickAttackInputBufferTimer - elapsed, 0.0f);
		_specialInputBufferTimer = Mathf.Max(_specialInputBufferTimer - elapsed, 0.0f);
		_specialAttackCooldownTimer = Mathf.Max(_specialAttackCooldownTimer - elapsed, 0.0f);
		if (CurrentAttackPhase != PlayerAttackPhase.None)
			_attackPhaseElapsed += elapsed;
		UpdateDevHitboxOverlay();
		RefreshFuryLockState();
		TickBestialFury(elapsed);
		TickAmmo(elapsed);
		if (_controller?.IsStaggered == true)
			return;
		if (GetTree().GetFirstNodeInGroup("hud") is HUD hud && hud.IsGameplayInputBlocked())
			return;
		if (Input.IsActionJustPressed("attack_light") && !IsPointerAttackBlocked())
		{
			_quickAttackInputBufferTimer = CompendiumTuning.GetFloat("combat.quick.buffer", QuickAttackInputBufferSeconds);
			BeginBasicAttack();
		}
		else if (EquippedWeapon.AutoFire && !_isAttacking && Input.IsActionPressed("attack_light") && !IsPointerAttackBlocked()
			&& (_controller == null || (!_controller.IsDodging && !_controller.CanDashAttack)))
			BeginBasicAttack();
		if (Input.IsActionJustPressed("attack_special"))
			RequestSpecialAttack();
		else if (_specialInputBufferTimer > 0.0f && CanBeginSpecialAttack())
			BeginSpecialAttack();
		if (Input.IsActionJustPressed("attack_fury"))
			BeginBestialFury();
	}

	public void AddFury(float amount)
	{
		if (!IsFuryUnlocked || IsFuryActive)
			return;

		MaxFury = GetTunedMaxFury();
		CurrentFury = Mathf.Min(CurrentFury + amount * CharacterSheet.GetBloodcraftMultiplier(), MaxFury);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
	}

	private void BeginBasicAttack()
	{
		if (_controller != null && (_controller.IsDodging || Input.IsActionJustPressed("dodge")))
		{
			_queuedQuickAttack = true;
			_quickAttackInputBufferTimer = 0.0f;
			return;
		}

		if (_controller != null && _controller.CanDashAttack)
		{
			_quickAttackInputBufferTimer = 0.0f;
			BeginQuickAttack(_controller.DodgeDirection);
			return;
		}

		if (_isAttacking)
		{
			_lightInputBufferTimer = Mathf.Max(_currentControl.InputBufferSeconds, 0.01f);
			return;
		}

		if (UsesAmmo && !TryConsumeAmmo())
			return;

		BeginAttack(GetTunedAttack(BasicCombo[_lightComboIndex], $"weapon.{EquippedWeapon.Id}.basic_{_lightComboIndex + 1}", PlayerAttackType.Basic), PlayerAttackType.Basic);
	}

	private void RequestSpecialAttack()
	{
		if (_specialAttackCooldownTimer > 0.0f)
			return;

		if (CanBeginSpecialAttack())
		{
			BeginSpecialAttack();
			return;
		}

		_specialInputBufferTimer = Mathf.Max(_currentControl.InputBufferSeconds, 0.01f);
	}

	private bool CanBeginSpecialAttack()
	{
		return _specialAttackCooldownTimer <= 0.0f
			&& (!_isAttacking || CanCancelCurrentAttack(AttackCancelAction.Special));
	}

	private void BeginSpecialAttack()
	{
		if (!CanBeginSpecialAttack())
			return;

		var attack = GetTunedAttack(EquippedWeapon.SpecialAttack, $"weapon.{EquippedWeapon.Id}.special", PlayerAttackType.Special);
		_specialAttackCooldownTimer = Mathf.Max(EquippedWeapon.TunedSpecialAttackCooldown * (1.0f - Mathf.Clamp(RunState.GetWeaponModTotal("SpecialCooldown.Reduction"), 0.0f, 0.9f)), 0.05f);
		if (_isAttacking)
			CancelCurrentAttackInternal(false);

		_specialInputBufferTimer = 0.0f;
		_lightInputBufferTimer = 0.0f;
		_lightComboIndex = 0;
		BeginAttack(attack, PlayerAttackType.Special);
	}

	private void BeginBestialFury()
	{
		if (RunState.GetEffectTotal("UnlockBestialFury.Flat") <= 0.0f)
		{
			GetTree().CallGroup("hud", "NotifySkillMessage", Localization.Text("combat.fury.locked"));
			return;
		}
		if (_isAttacking && !TryCancelCurrentAttack(AttackCancelAction.Special))
			return;
		if (!TryActivateFury())
			GetTree().CallGroup("hud", "NotifySkillMessage", Localization.TextFormat("combat.fury.not_ready", Mathf.RoundToInt(CurrentFury), Mathf.RoundToInt(MaxFury)));
	}

	public bool TryReleaseQueuedQuickAttack(Vector3 dashDirection)
	{
		if (!_queuedQuickAttack)
			return false;

		_queuedQuickAttack = false;
		_lightInputBufferTimer = 0.0f;
		_lightComboIndex = 0;
		BeginQuickAttack(dashDirection);
		return true;
	}

	public void QueueQuickAttack()
	{
		_queuedQuickAttack = true;
		_quickAttackInputBufferTimer = 0.0f;
	}

	public bool HasBufferedQuickAttack => _quickAttackInputBufferTimer > 0.0f;

	private async void BeginAttack(WeaponAttackDefinition attack, PlayerAttackType attackType)
	{
		if (_isAttacking)
			return;

		_isAttacking = true;
		var attackGeneration = ++_attackGeneration;
		CurrentAttackType = attackType;
		CodexProgress.MarkAttack(attackType == PlayerAttackType.Special ? CodexAttackId.Special : CodexAttackId.Light(_lightComboIndex));
		_currentControl = attack.Control ?? AttackControlProfile.Default;
		_attackDirection = GetAssistedAttackDirection(GetMouseAttackDirection(), attack);
		FaceAttackDirection();
		ConfigureAttackArea(attack);
		var hitDelay = GetAttackDelay(attack.HitDelay, attackType);
		var recoveryDelay = GetAttackDelay(attack.Recovery, attackType);
		var isRangedShot = EquippedWeapon.IsRanged && attack.Shape == WeaponAttackShape.Line;
		if (attackType == PlayerAttackType.Special && !isRangedShot)
			_animator?.PlayHeavyAttack(hitDelay, recoveryDelay);
		else
			_animator?.PlayLightAttack(_lightComboIndex, hitDelay, recoveryDelay, attack.Shape, IsComboFinisher);
		// O disparo basico e rapido demais para preview; a Bala de Prata mostra a linha do tiro na preparacao.
		if (!(isRangedShot && attackType == PlayerAttackType.Basic))
			SpawnAttackPreview(attack, hitDelay, attackType == PlayerAttackType.Special || IsComboFinisher, attackType);
		var swingEffect = isRangedShot
			? attackType == PlayerAttackType.Special ? "gun_heavy" : "gun_fire"
			: attackType == PlayerAttackType.Special
			? "attack_special"
			: _lightComboIndex switch
			{
				0 => "attack_basic_1",
				1 => "attack_basic_2",
				_ => "attack_thrust"
			};
		GetTree().CallGroup("audio_manager", "PlayEffect", swingEffect);

		SetAttackPhase(PlayerAttackPhase.Startup, hitDelay);
		await Wait(hitDelay);
		if (!IsCurrentAttack(attackGeneration))
			return;
		FreeAttackPreview();

		if (_currentControl.RetargetAtImpactStrength > 0.0f && attack.Shape != WeaponAttackShape.Radial)
		{
			var retargetedDirection = GetAssistedAttackDirection(GetMouseAttackDirection(_attackDirection), attack);
			_attackDirection = _attackDirection.Slerp(retargetedDirection, Mathf.Clamp(_currentControl.RetargetAtImpactStrength, 0.0f, 1.0f)).Normalized();
			FaceAttackDirection();
		}

		// Tiro: o rastro de bala e desenhado depois do dano, com o comprimento ate o alvo ou a parede.
		if (attack.Shape == WeaponAttackShape.Line && !isRangedShot)
		{
			_controller?.PerformAttackLunge(_attackDirection, EquippedWeapon.TunedLineAttackLungeDistance);
			SpawnThrustIndicator(attack, attackType);
		}
		else if (attack.Shape == WeaponAttackShape.Radial)
		{
			SpawnRadialAttackCircle(attack, attackType);
			if (attackType == PlayerAttackType.Special)
				GetTree().CallGroup("audio_manager", "PlayEffect", "special_ground");
		}
		else if (!isRangedShot) SpawnAttackSlash(attack, attackType, attackType == PlayerAttackType.Special);
		// Teto do Livro-Caixa: +100% de dano (economy.ledger.max_bonus).
		var currencyDamage = Mathf.Min(RunState.GetEffectTotal("DamagePerCurrencyHeld.Multiplier") * RunState.RunBloodCoins, CompendiumTuning.GetFloat("economy.ledger.max_bonus", 1.0f));
		var roomDamage = RunState.GetEffectTotal("DamageUntilRoomClear.Multiplier");
		var damageMultiplier = CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier * (1.0f + currencyDamage + roomDamage + RunState.ContextDamageBonus) * EquippedWeapon.TunedDamageMultiplier;
		var furyDamageMultiplier = CompendiumTuning.GetFloat("attack.fury.damage_multiplier", FuryDamageMultiplier);
		furyDamageMultiplier *= 1.0f + RunState.GetEffectTotal("FuryDamage.Multiplier") + (_skills?.FuryDamageBonus() ?? 0.0f);
		if (IsFuryActive) furyDamageMultiplier *= CharacterSheet.GetBloodcraftMultiplier();
		var attackDamage = attack.Damage * damageMultiplier * (attackType == PlayerAttackType.Basic ? RunState.GetWeaponModMultiplier("BasicQuickDamage.Multiplier") : 1.0f) * (IsFuryActive ? furyDamageMultiplier : 1.0f);
		var hit = attack.Shape == WeaponAttackShape.Radial
			? DealCircularDamage(attackDamage, attack, attackType)
			: DealDamage(attackDamage, attack.Knockback, attack, attackType);
		if (isRangedShot)
			SpawnBulletTracer(hit.Reach, attackType);
		if (hit.TargetsHit > 0)
		{
			var hitEffect = isRangedShot
				? "hit_pierce"
				: attackType == PlayerAttackType.Special
				? "hit_slam"
				: IsComboFinisher ? "hit_pierce" : "hit_slash";
			GetTree().CallGroup("audio_manager", "PlayEffect", hitEffect);
		}
		CombatFeedback.Instance?.PlayPlayerHitWeighted(hit.TargetsHit, hit.IsCritical,
			attackType == PlayerAttackType.Special ? 1.5f : IsComboFinisher ? 1.0f : 0.0f);
		if (attackType == PlayerAttackType.Special)
		{
			// Tiro: a poca de sangue nasce no primeiro alvo (ou no fim do tiro), com raio pequeno; corpo a corpo: em Virgil.
			if (isRangedShot)
				_blessings?.OnSpecialAttack(hit.ImpactPosition, RangedSpecialPoolRange, attack.Shape);
			else
				_blessings?.OnSpecialAttack(GetParent<Node3D>().GlobalPosition, attack.Range, attack.Shape);
			if (EquippedWeapon.SpecialReloadsMagazine && UsesAmmo)
				RefillAmmo();
		}

		SetAttackPhase(PlayerAttackPhase.Recovery, recoveryDelay);
		await Wait(recoveryDelay);
		if (!IsCurrentAttack(attackGeneration))
			return;

		_isAttacking = false;
		SetAttackPhase(PlayerAttackPhase.None, 0.0f);
		_comboResetTimer = CompendiumTuning.GetFloat("combat.combo.reset_time", ComboResetTime);

		if (attackType == PlayerAttackType.Special)
		{
			_lightComboIndex = 0;
			return;
		}

		_lightComboIndex = (_lightComboIndex + 1) % BasicCombo.Length;

		if (_lightInputBufferTimer > 0.0f)
		{
			_lightInputBufferTimer = 0.0f;
			BeginBasicAttack();
		}
	}

	private async void BeginQuickAttack(Vector3 dashDirection)
	{
		if (_isAttacking)
			return;

		_isAttacking = true;
		_quickAttackInputBufferTimer = 0.0f;
		var attackGeneration = ++_attackGeneration;
		IsQuickAttacking = true;
		CurrentAttackType = PlayerAttackType.Quick;
		CodexProgress.MarkAttack(CodexAttackId.Dash);
		_controller?.ConsumeDashAttackGrace();
		_health?.SetInvincible(true);

		var tunedQuickAttack = GetTunedAttack(EquippedWeapon.QuickAttack, $"weapon.{EquippedWeapon.Id}.quick", PlayerAttackType.Quick);
		var attack = tunedQuickAttack with
		{
			Range = tunedQuickAttack.Range * EquippedWeapon.TunedQuickAttackHitboxScale,
			Width = tunedQuickAttack.Width * EquippedWeapon.TunedQuickAttackHitboxScale
		};
		_currentControl = attack.Control ?? AttackControlProfile.Default;
		_attackDirection = GetAssistedAttackDirection(NormalizeFallback(dashDirection), attack);
		FaceAttackDirection();
		ConfigureAttackArea(attack);
		var quickHitDelay = Mathf.Max(attack.HitDelay, 0.04f);
		var recoveryDelay = Mathf.Max(attack.Recovery, 0.01f);
		_animator?.PlayDashAttack(quickHitDelay, recoveryDelay);
		GetTree().CallGroup("audio_manager", "PlayEffect", EquippedWeapon.IsRanged ? "gun_fire" : "attack_quick");
		SetAttackPhase(PlayerAttackPhase.Startup, quickHitDelay);
		await Wait(quickHitDelay);
		if (!IsCurrentAttack(attackGeneration)) return;

		var damageMultiplier = CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier * EquippedWeapon.TunedDamageMultiplier;
		var furyDamageMultiplier = CompendiumTuning.GetFloat("attack.fury.damage_multiplier", FuryDamageMultiplier);
		furyDamageMultiplier *= 1.0f + RunState.GetEffectTotal("FuryDamage.Multiplier") + (_skills?.FuryDamageBonus() ?? 0.0f);
		if (IsFuryActive) furyDamageMultiplier *= CharacterSheet.GetBloodcraftMultiplier();
		var quickDamageMultiplier = EquippedWeapon.TunedQuickAttackDamageMultiplier;
		if (attack.Shape == WeaponAttackShape.Radial) SpawnRadialAttackCircle(attack, PlayerAttackType.Quick);
		else if (attack.Shape == WeaponAttackShape.Line) SpawnThrustIndicator(attack, PlayerAttackType.Quick);
		else SpawnAttackSlash(attack, PlayerAttackType.Quick, true);
		var quickDamage = attack.Damage * quickDamageMultiplier * damageMultiplier * RunState.GetWeaponModMultiplier("BasicQuickDamage.Multiplier") * (IsFuryActive ? furyDamageMultiplier : 1.0f);
		var dashHit = attack.Shape == WeaponAttackShape.Radial
			? DealCircularDamage(quickDamage, attack, PlayerAttackType.Quick)
			: DealDamage(quickDamage, attack.Knockback, attack, PlayerAttackType.Quick);
		if (dashHit.TargetsHit > 0)
			GetTree().CallGroup("audio_manager", "PlayEffect", "hit_spin");
		CombatFeedback.Instance?.PlayPlayerHitWeighted(dashHit.TargetsHit, dashHit.IsCritical, 0.8f);

		SetAttackPhase(PlayerAttackPhase.Recovery, recoveryDelay);
		await Wait(recoveryDelay);
		if (!IsCurrentAttack(attackGeneration))
			return;

		_health?.SetInvincible(false);
		IsQuickAttacking = false;
		_isAttacking = false;
		SetAttackPhase(PlayerAttackPhase.None, 0.0f);
		_comboResetTimer = CompendiumTuning.GetFloat("combat.combo.reset_time", ComboResetTime);
		_lightComboIndex = 0;
	}

	public bool CanCancelCurrentAttack(AttackCancelAction action)
	{
		if (!_isAttacking || CurrentAttackPhase == PlayerAttackPhase.None)
			return true;

		if (CurrentAttackPhase == PlayerAttackPhase.Startup)
			return action == AttackCancelAction.Dodge
				? _currentControl.DodgeCancelsStartup
				: _currentControl.SpecialCancelsStartup;

		var recoveryProgress = GetAttackPhaseProgress();
		return action == AttackCancelAction.Dodge
			? recoveryProgress >= _currentControl.DodgeCancelRecoveryStart
			: recoveryProgress >= _currentControl.SpecialCancelRecoveryStart;
	}

	public bool TryCancelCurrentAttack(AttackCancelAction action)
	{
		if (!CanCancelCurrentAttack(action))
			return false;

		if (_isAttacking)
			CancelCurrentAttackInternal(true);
		return true;
	}

	public void CancelCurrentAttack()
	{
		CancelCurrentAttackInternal(true);
	}

	private void CancelCurrentAttackInternal(bool resetCombo)
	{
		if (!_isAttacking)
			return;

		_attackGeneration++;
		FreeAttackPreview();
		_isAttacking = false;
		IsQuickAttacking = false;
		SetAttackPhase(PlayerAttackPhase.None, 0.0f);
		_lightInputBufferTimer = 0.0f;
		_specialInputBufferTimer = 0.0f;
		_queuedQuickAttack = false;
		if (resetCombo)
			_lightComboIndex = 0;
		_health?.SetInvincible(false);
		_animator?.CancelOneShot();
	}

	public bool IsPointerAttackBlocked()
	{
		if (GetTree().Paused)
			return true;

		foreach (var node in GetTree().GetNodesInGroup("hud"))
			if (node is HUD hud && hud.IsGameplayInputBlocked())
				return true;

		return false;
	}

	private bool IsCurrentAttack(int attackGeneration)
	{
		return IsInsideTree() && _isAttacking && attackGeneration == _attackGeneration;
	}

	private HitResult DealDamage(float damage, float knockback, WeaponAttackDefinition attack, PlayerAttackType attackType)
	{
		var player = GetParent<Node3D>();
		var halfArcRadians = Mathf.DegToRad(EquippedWeapon.TunedAttackArcDegrees * 0.5f);
		var edgeForgiveness = GetEdgeForgiveness(attackType);
		var criticalChance = Mathf.Clamp(RunState.GetEffectTotal("CriticalChance.Flat") + RunState.ContextCriticalChance + (_blessings?.BonusCriticalChance() ?? 0.0f), 0, 1);
		var isCritical = RunState.ConsumeGuaranteedCritical() || GD.Randf() < criticalChance;
		var criticalMultiplier = CompendiumTuning.GetFloat("combat.crit.base_multiplier", 1.5f) + RunState.GetEffectTotal("CriticalDamage.Multiplier") + RunState.ContextCriticalDamage;
		var nonCriticalMultiplier = 1.0f + RunState.GetEffectTotal("NonCriticalDamage.Multiplier");
		var targetsHit = 0;
		var isRangedShot = EquippedWeapon.IsRanged && attack.Shape == WeaponAttackShape.Line;
		var wallDistance = isRangedShot ? GetShotWallDistance(attack.Range + edgeForgiveness) : float.PositiveInfinity;
		var candidates = new System.Collections.Generic.List<(Node3D Node, float Forward)>();

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemyNode)
				continue;

			var toEnemy = enemyNode.GlobalPosition - player.GlobalPosition;
			toEnemy.Y = 0.0f;

			var distance = toEnemy.Length();
			var targetRadius = enemyNode is EnemyBasic target ? target.HitTargetRadius : 0.35f;
			if (distance <= 0.001f || distance > attack.Range + edgeForgiveness + targetRadius)
				continue;

			if (attack.Shape == WeaponAttackShape.Line)
			{
				var forwardDistance = toEnemy.Dot(_attackDirection);
				var lateralDirection = new Vector3(-_attackDirection.Z, 0.0f, _attackDirection.X);
				var lateralDistance = Mathf.Abs(toEnemy.Dot(lateralDirection));
				if (forwardDistance < -targetRadius || forwardDistance > attack.Range + edgeForgiveness + targetRadius || lateralDistance > attack.Width * 0.5f + targetRadius)
					continue;
			}
			else
			{
				var angle = Mathf.Acos(Mathf.Clamp(toEnemy.Normalized().Dot(_attackDirection), -1.0f, 1.0f));
				var targetAngle = Mathf.Asin(Mathf.Clamp(targetRadius / Mathf.Max(distance, targetRadius), 0.0f, 1.0f));
				if (angle > halfArcRadians + targetAngle)
					continue;
			}

			var forwardAlong = toEnemy.Dot(_attackDirection);
			// Tiro nao atravessa parede: descarta alvos depois do primeiro obstaculo na linha.
			if (isRangedShot && forwardAlong - targetRadius > wallDistance)
				continue;
			// Com limite de alvos, um inimigo morrendo nao pode "segurar" a bala.
			if (attack.MaxTargets > 0 && enemyNode.GetNodeOrNull<Health>("Health") is { IsDead: true })
				continue;
			candidates.Add((enemyNode, forwardAlong));
		}

		// MaxTargets > 0: so os N mais proximos ao longo do golpe (o disparo para no primeiro inimigo).
		if (attack.MaxTargets > 0)
		{
			candidates.Sort((a, b) => a.Forward.CompareTo(b.Forward));
			if (candidates.Count > attack.MaxTargets)
				candidates.RemoveRange(attack.MaxTargets, candidates.Count - attack.MaxTargets);
		}

		var reach = Mathf.Min(attack.Range + edgeForgiveness, wallDistance);
		if (attack.MaxTargets > 0 && candidates.Count >= attack.MaxTargets)
			reach = Mathf.Clamp(candidates[^1].Forward, 0.3f, reach);
		var impactPosition = candidates.Count > 0
			? candidates[0].Node.GlobalPosition
			: player.GlobalPosition + _attackDirection * reach;

		foreach (var (enemyNode, _) in candidates)
		{
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
				finalDamage = _blessings?.ModifyOutgoingDamage(finalDamage, blessingTarget, attackType) ?? finalDamage;
			var executeThreshold = RunState.GetEffectTotal("ExecuteThreshold.Flat") + (isCritical ? RunState.GetEffectTotal("ExecuteThresholdOnCritical.Flat") : 0);
			if (executeThreshold > 0 && targetHealth.CurrentHealth / targetHealth.MaxHealth <= executeThreshold) finalDamage = targetHealth.CurrentHealth;
			var isBackstab = enemyNode is EnemyBasic backstabTarget && backstabTarget.IsAttackedFromBehind(player.GlobalPosition);
			if (isBackstab) finalDamage *= EquippedWeapon.TunedBackstabDamageMultiplier;
			if (enemyNode is EnemyBasic skillTarget) finalDamage = _skills?.ModifyOutgoingDamage(finalDamage, skillTarget, isBackstab, attackType) ?? finalDamage;
			targetHealth.ApplyDamage(finalDamage, isCritical, isBackstab ? EnemyBasic.BackstabDamageSource : attack.Name);
			if (enemyNode is EnemyBasic hitEnemy)
			{
				hitEnemy.ApplyStagger(attack.StaggerDuration);
				_blessings?.OnPlayerHit(hitEnemy, finalDamage, isCritical, attackType);
				if (IsFuryActive) SpawnFuryBloodBurst(hitEnemy.GlobalPosition);
			}
			targetsHit++;
			if (isCritical)
			{
				_health?.Heal(finalDamage * (RunState.GetEffectTotal("CriticalLifeSteal.Multiplier") + RunState.ContextLifeSteal));
			}
			if (enemyNode is EnemyBasic enemy)
				enemy.ApplyKnockback(enemyNode.GlobalPosition - player.GlobalPosition, GetAttackKnockback(knockback, attackType, enemy), finalDamage * EquippedWeapon.TunedWallImpactDamageMultiplier);
		}

		return new HitResult(targetsHit, isCritical, reach, impactPosition);
	}

	private HitResult DealCircularDamage(float damage, WeaponAttackDefinition attack, PlayerAttackType attackType)
	{
		var player = GetParent<Node3D>();
		var radius = attack.Range;
		var targetsHit = 0;
		var edgeForgiveness = GetEdgeForgiveness(attackType);
		var criticalChance = Mathf.Clamp(RunState.GetEffectTotal("CriticalChance.Flat") + RunState.ContextCriticalChance + (_blessings?.BonusCriticalChance() ?? 0.0f), 0, 1);
		var isCritical = RunState.ConsumeGuaranteedCritical() || GD.Randf() < criticalChance;
		var criticalMultiplier = CompendiumTuning.GetFloat("combat.crit.base_multiplier", 1.5f) + RunState.GetEffectTotal("CriticalDamage.Multiplier") + RunState.ContextCriticalDamage;
		var nonCriticalMultiplier = 1.0f + RunState.GetEffectTotal("NonCriticalDamage.Multiplier");

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not Node3D enemyNode)
				continue;

			var toEnemy = enemyNode.GlobalPosition - player.GlobalPosition;
			toEnemy.Y = 0.0f;

			var targetRadius = enemyNode is EnemyBasic target ? target.HitTargetRadius : 0.35f;
			if (toEnemy.Length() > radius + edgeForgiveness + targetRadius)
				continue;

			var targetHealth = enemyNode.GetNodeOrNull<Health>("Health");
			if (targetHealth == null)
				continue;

			var finalDamage = damage * (isCritical ? criticalMultiplier : nonCriticalMultiplier);
			if (enemyNode is EnemyBasic special)
			{
				finalDamage *= special.DamageTakenMultiplier;
				if (special.Rarity != EnemyRarity.Normal) finalDamage *= 1.0f + RunState.GetEffectTotal("DamageVsElite.Multiplier");
				if (special.Name.ToString().Contains("Boss")) finalDamage *= 1.0f + RunState.GetEffectTotal("DamageVsBoss.Multiplier");
				finalDamage = _blessings?.ModifyOutgoingDamage(finalDamage, special, attackType) ?? finalDamage;
			}
			var isBackstab = enemyNode is EnemyBasic backstabTarget && backstabTarget.IsAttackedFromBehind(player.GlobalPosition);
			if (isBackstab) finalDamage *= EquippedWeapon.TunedBackstabDamageMultiplier;
			if (enemyNode is EnemyBasic skillTarget) finalDamage = _skills?.ModifyOutgoingDamage(finalDamage, skillTarget, isBackstab, attackType) ?? finalDamage;
			var executeThreshold = RunState.GetEffectTotal("ExecuteThreshold.Flat") + (isCritical ? RunState.GetEffectTotal("ExecuteThresholdOnCritical.Flat") : 0);
			if (executeThreshold > 0 && targetHealth.CurrentHealth / targetHealth.MaxHealth <= executeThreshold) finalDamage = targetHealth.CurrentHealth;
			targetHealth.ApplyDamage(finalDamage, isCritical, isBackstab ? EnemyBasic.BackstabDamageSource : attackType == PlayerAttackType.Special ? EquippedSpecialName : EquippedWeapon.QuickAttack.Name);
			if (enemyNode is EnemyBasic hitEnemy)
			{
				hitEnemy.ApplyStagger(attack.StaggerDuration);
				_blessings?.OnPlayerHit(hitEnemy, finalDamage, isCritical, attackType);
				if (IsFuryActive) SpawnFuryBloodBurst(hitEnemy.GlobalPosition);
			}
			targetsHit++;
			if (isCritical)
			{
				_health?.Heal(finalDamage * (RunState.GetEffectTotal("CriticalLifeSteal.Multiplier") + RunState.ContextLifeSteal));
			}
			if (enemyNode is EnemyBasic enemy)
				enemy.ApplyKnockback(toEnemy.LengthSquared() > 0.001f ? toEnemy : _attackDirection, GetAttackKnockback(attack.Knockback, attackType, enemy), finalDamage * EquippedWeapon.TunedWallImpactDamageMultiplier);
		}

		return new HitResult(targetsHit, isCritical);
	}

	public float GetCurrentAttackSpeedMultiplier()
	{
		var missingHealth = _health != null && _health.MaxHealth > 0 ? 1.0f - _health.CurrentHealth / _health.MaxHealth : 0.0f;
		var artifactSpeed = 1.0f + RunState.GetEffectTotal("AttackSpeed.Multiplier")
			+ RunState.GetEffectTotal("AttackSpeedMissingHealth.Multiplier") * missingHealth
			+ RunState.ContextAttackSpeed + (_blessings?.BonusAttackSpeed() ?? 0.0f);
		return Mathf.Max(EquippedWeapon.TunedBasicAttackSpeedMultiplier * artifactSpeed, 0.01f);
	}

	public float GetCurrentBasicAttackDamage()
	{
		var attack = GetTunedAttack(BasicCombo[0], $"weapon.{EquippedWeapon.Id}.basic_1", PlayerAttackType.Basic);
		// Teto do Livro-Caixa: +100% de dano (economy.ledger.max_bonus).
		var currencyDamage = Mathf.Min(RunState.GetEffectTotal("DamagePerCurrencyHeld.Multiplier") * RunState.RunBloodCoins, CompendiumTuning.GetFloat("economy.ledger.max_bonus", 1.0f));
		var roomDamage = RunState.GetEffectTotal("DamageUntilRoomClear.Multiplier");
		var multiplier = CharacterSheet.GetDamageMultiplier() * RunState.DamageMultiplier
			* (1.0f + currencyDamage + roomDamage + RunState.ContextDamageBonus)
			* EquippedWeapon.TunedDamageMultiplier;
		multiplier *= Mathf.Max(1.0f + RunState.GetEffectTotal("BasicAttackDamage.Multiplier"), 0.0f);
		if (IsFuryActive)
		{
			var furyMultiplier = CompendiumTuning.GetFloat("attack.fury.damage_multiplier", FuryDamageMultiplier);
			furyMultiplier *= 1.0f + RunState.GetEffectTotal("FuryDamage.Multiplier") + (_skills?.FuryDamageBonus() ?? 0.0f);
			multiplier *= furyMultiplier * CharacterSheet.GetBloodcraftMultiplier();
		}
		multiplier *= RunState.GetWeaponModMultiplier("BasicQuickDamage.Multiplier");
		return attack.Damage * multiplier;
	}

	public float GetCurrentBasicAttackInterval()
	{
		var attack = GetTunedAttack(BasicCombo[0], $"weapon.{EquippedWeapon.Id}.basic_1", PlayerAttackType.Basic);
		return Mathf.Max((attack.HitDelay + attack.Recovery) * EquippedWeapon.TunedAttackDelayMultiplier
			/ GetCurrentAttackSpeedMultiplier(), 0.08f);
	}

	private float GetAttackDelay(float baseDelay, PlayerAttackType attackType)
	{
		if (attackType != PlayerAttackType.Basic)
			return Mathf.Max(baseDelay, 0.01f);

		return Mathf.Max((baseDelay * EquippedWeapon.TunedAttackDelayMultiplier) / GetCurrentAttackSpeedMultiplier(), 0.01f);
	}

	// Reach/ImpactPosition: so os golpes em Linha preenchem (comprimento do tiro e ponto do primeiro acerto ou do fim).
	private readonly record struct HitResult(int TargetsHit, bool IsCritical, float Reach = 0.0f, Vector3 ImpactPosition = default);

	private float GetAttackKnockback(float knockback, PlayerAttackType attackType, EnemyBasic enemy)
	{
		if (attackType == PlayerAttackType.Special)
			return knockback * EquippedWeapon.TunedSpecialAttackKnockbackMultiplier;
		if (attackType == PlayerAttackType.Quick)
			return knockback * EquippedWeapon.TunedQuickAttackKnockbackMultiplier;
		if (attackType != PlayerAttackType.Basic || enemy == null || enemy.Name.ToString().Contains("Boss"))
			return knockback;
		var archetypeMultiplier = enemy.Archetype == EnemyArchetype.Tank ? CompendiumTuning.GetFloat("combat.knockback.tank_mult", 0.5f) : 1.0f;
		var comboMultiplier = IsComboFinisher ? CompendiumTuning.GetFloat("combat.knockback.finisher_mult", 1.2f) : 1.0f;
		return knockback * EquippedWeapon.TunedBasicAttackKnockbackMultiplier * archetypeMultiplier * comboMultiplier;
	}

	private void ConfigureAttackArea(WeaponAttackDefinition attack)
	{
		_lastDevAttack = attack;
		_attackArea.Position = new Vector3(0.0f, 0.75f, -0.45f - attack.Range * 0.5f);

		if (_attackBox != null)
			_attackBox.Size = new Vector3(attack.Width, 1.1f, attack.Range);
	}

	private float GetEdgeForgiveness(PlayerAttackType attackType)
	{
		return attackType == PlayerAttackType.Quick
			? EquippedWeapon.TunedHitboxEdgeForgiveness * EquippedWeapon.TunedQuickAttackHitboxScale
			: EquippedWeapon.TunedHitboxEdgeForgiveness;
	}

	// Golpe com heranca de sangue: o indicador troca o ciano/dourado neutro pela cor da familia do slot (BloodlineVisuals),
	// com a borda mais clara. Sem heranca no slot, nada muda.
	private static void ApplyBloodlineTint(PlayerAttackType attackType, StandardMaterial3D fill, StandardMaterial3D edge)
	{
		if (!BloodlineVisuals.TryGetSlotColor(attackType, out var color))
			return;
		if (fill != null)
		{
			fill.AlbedoColor = new Color(color, fill.AlbedoColor.A + 0.06f);
			fill.Emission = color;
		}
		if (edge != null)
		{
			edge.AlbedoColor = new Color(BloodlineVisuals.Lighten(color, 0.35f), edge.AlbedoColor.A);
			edge.Emission = color;
		}
	}

	private async void SpawnAttackSlash(WeaponAttackDefinition attack, PlayerAttackType attackType, bool isHeavy)
	{
		var player = GetParent<Node3D>();
		// Indicador do JOGADOR: ciano (leve) / branco-dourado (pesado). Vermelho e reservado ao perigo dos inimigos.
		var fillMaterial = new StandardMaterial3D
		{
			AlbedoColor = isHeavy ? new Color(1.0f, 0.90f, 0.60f, 0.24f) : new Color(0.45f, 0.85f, 1.0f, 0.20f),
			EmissionEnabled = true,
			Emission = isHeavy ? new Color(1.0f, 0.82f, 0.35f) : new Color(0.25f, 0.70f, 1.0f),
			EmissionEnergyMultiplier = isHeavy ? 1.2f : 0.9f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var edgeMaterial = new StandardMaterial3D
		{
			AlbedoColor = isHeavy ? new Color(1.0f, 0.97f, 0.80f, 0.90f) : new Color(0.80f, 0.96f, 1.0f, 0.78f),
			EmissionEnabled = true,
			Emission = isHeavy ? new Color(1.0f, 0.85f, 0.40f) : new Color(0.40f, 0.82f, 1.0f),
			EmissionEnergyMultiplier = isHeavy ? 2.6f : 1.9f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		ApplyBloodlineTint(attackType, fillMaterial, edgeMaterial);

		var indicator = new Node3D
		{
			Name = "AttackHitboxIndicator"
		};
		player.AddChild(indicator);

		var visualRange = attack.Range + GetEdgeForgiveness(attackType);
		AddArcMesh(indicator, "ArcFill", visualRange, EquippedWeapon.TunedAttackArcDegrees, fillMaterial, 0.075f);
		AddArcMesh(indicator, "ArcEdge", visualRange, EquippedWeapon.TunedAttackArcDegrees, edgeMaterial, 0.12f, true);

		var tween = indicator.CreateTween();
		indicator.Scale = new Vector3(0.9f, 1.0f, 0.85f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.05f, 1.0f, 1.0f), 0.09f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.12f, 1.0f, 1.06f), 0.07f);
		tween.Parallel().TweenProperty(fillMaterial, "albedo_color:a", 0.0f, 0.07f);
		tween.Parallel().TweenProperty(edgeMaterial, "albedo_color:a", 0.0f, 0.07f);
		await ToSignal(tween, Tween.SignalName.Finished);
		indicator.QueueFree();
	}

	private async void SpawnRadialAttackCircle(WeaponAttackDefinition attack, PlayerAttackType attackType)
	{
		var player = GetParent<Node3D>();
		var fillMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.45f, 0.85f, 1.0f, 0.22f),
			EmissionEnabled = true,
			Emission = new Color(0.25f, 0.70f, 1.0f),
			EmissionEnergyMultiplier = 1.4f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var ringMaterial = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.80f, 0.96f, 1.0f, 0.86f),
			EmissionEnabled = true,
			Emission = new Color(0.40f, 0.82f, 1.0f),
			EmissionEnergyMultiplier = 2.4f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		ApplyBloodlineTint(attackType, fillMaterial, ringMaterial);

		var indicator = new Node3D
		{
			Name = "RadialAttackCircleIndicator"
		};
		player.AddChild(indicator);

		var visualRadius = attack.Range + GetEdgeForgiveness(attackType);
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
		tween.TweenProperty(indicator, "scale", new Vector3(1.08f, 1.0f, 1.08f), 0.09f);
		tween.TweenProperty(indicator, "scale", new Vector3(1.16f, 1.0f, 1.16f), 0.07f);
		tween.Parallel().TweenProperty(fillMaterial, "albedo_color:a", 0.0f, 0.07f);
		tween.Parallel().TweenProperty(ringMaterial, "albedo_color:a", 0.0f, 0.07f);
		await ToSignal(tween, Tween.SignalName.Finished);
		indicator.QueueFree();
	}

	private async void SpawnThrustIndicator(WeaponAttackDefinition attack, PlayerAttackType attackType)
	{
		var player = GetParent<Node3D>();
		var material = new StandardMaterial3D
		{
			AlbedoColor = new Color(0.75f, 0.95f, 1.0f, 0.78f),
			EmissionEnabled = true,
			Emission = new Color(0.40f, 0.82f, 1.0f),
			EmissionEnergyMultiplier = 2.0f,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		ApplyBloodlineTint(attackType, null, material);
		var indicator = new Node3D { Name = "ThrustAttackIndicator" };
		player.AddChild(indicator);
		var visualRange = attack.Range + GetEdgeForgiveness(attackType);
		AddHitboxBox(indicator, "ThrustLine", new Vector3(attack.Width, 0.06f, visualRange), new Vector3(0.0f, 0.1f, -visualRange * 0.5f), material);
		indicator.Scale = new Vector3(0.55f, 1.0f, 0.35f);
		var tween = indicator.CreateTween();
		tween.TweenProperty(indicator, "scale", Vector3.One, 0.08f);
		tween.TweenProperty(material, "albedo_color:a", 0.0f, 0.10f);
		await ToSignal(tween, Tween.SignalName.Finished);
		indicator.QueueFree();
	}

	// Preview barato na fase de startup: preenchimento tenue (arco ou linha) que cresce ate o impacto.
	private void SpawnAttackPreview(WeaponAttackDefinition attack, float hitDelay, bool isHeavy, PlayerAttackType attackType)
	{
		FreeAttackPreview();
		if (attack.Shape == WeaponAttackShape.Radial)
			return;

		var player = GetParent<Node3D>();
		var color = isHeavy ? new Color(1.0f, 0.90f, 0.60f, 0.05f) : new Color(0.45f, 0.85f, 1.0f, 0.05f);
		if (BloodlineVisuals.TryGetSlotColor(attackType, out var bloodline))
			color = new Color(bloodline, color.A);
		var material = new StandardMaterial3D
		{
			AlbedoColor = color,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		var preview = new Node3D { Name = "AttackPreviewIndicator" };
		player.AddChild(preview);
		var visualRange = attack.Range + EquippedWeapon.TunedHitboxEdgeForgiveness;
		if (attack.Shape == WeaponAttackShape.Line)
			AddHitboxBox(preview, "PreviewLine", new Vector3(attack.Width, 0.04f, visualRange), new Vector3(0.0f, 0.07f, -visualRange * 0.5f), material);
		else
			AddArcMesh(preview, "PreviewArc", visualRange, EquippedWeapon.TunedAttackArcDegrees, material, 0.07f);
		preview.Scale = new Vector3(0.85f, 1.0f, 0.85f);
		var tween = preview.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(preview, "scale", Vector3.One, Mathf.Max(hitDelay, 0.05f));
		tween.TweenProperty(material, "albedo_color:a", 0.17f, Mathf.Max(hitDelay, 0.05f));
		_attackPreview = preview;
	}

	private void FreeAttackPreview()
	{
		if (_attackPreview != null && IsInstanceValid(_attackPreview))
			_attackPreview.QueueFree();
		_attackPreview = null;
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
		if (!IsFuryUnlocked)
			return false;
		MaxFury = GetTunedMaxFury();
		if (IsFuryActive || CurrentFury < MaxFury)
			return false;

		ActivateFury();
		return true;
	}

	private void ActivateFury()
	{
		IsFuryActive = true;
		CodexProgress.MarkAttack(CodexAttackId.Fury);
		CurrentFury = Mathf.Min(CurrentFury, MaxFury);
		SetFuryVisualActive(true);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
	}

	private void TickBestialFury(float delta)
	{
		if (!IsFuryActive)
			return;

		var durationMultiplier = 1.0f + RunState.GetEffectTotal("FuryDuration.Multiplier");
		durationMultiplier *= _skills?.TemporaryEffectDurationMultiplier() ?? 1.0f;
		var baseDrainRate = CompendiumTuning.GetFloat("attack.fury.drain_per_second", FuryDrainPerSecond);
		var drainRate = Mathf.Max(baseDrainRate / Mathf.Max(durationMultiplier, 0.05f), 0.01f);
		CurrentFury = Mathf.Max(CurrentFury - drainRate * delta, 0.0f);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
		if (CurrentFury > 0.0f)
			return;

		EndBestialFury();
	}

	private void RefreshFuryLockState()
	{
		if (IsFuryUnlocked || (!IsFuryActive && CurrentFury <= 0.0f))
			return;

		IsFuryActive = false;
		CurrentFury = 0.0f;
		SetFuryVisualActive(false);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, false);
	}

	private void EndBestialFury()
	{
		IsFuryActive = false;
		SetFuryVisualActive(false);
		EmitSignal(SignalName.FuryChanged, CurrentFury, MaxFury, IsFuryActive);
		DealFuryEndBurst();
	}

	private void DealFuryEndBurst()
	{
		var player = GetParent<Node3D>();
		var damage = GetCurrentBasicAttackDamage() * CompendiumTuning.GetFloat("attack.fury.end_burst_ratio", FuryEndBurstDamageRatio);
		var burstRadius = CompendiumTuning.GetFloat("attack.fury.end_burst_radius", FuryEndBurstRadius);
		var burstKnockback = CompendiumTuning.GetFloat("attack.fury.end_burst_knockback", 2.5f);
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not EnemyBasic enemy || enemy.GlobalPosition.DistanceTo(player.GlobalPosition) > burstRadius)
				continue;
			var finalDamage = damage * enemy.DamageTakenMultiplier;
			enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(finalDamage, false, "Explosão final da Fúria Bestial");
			enemy.ApplyKnockback(enemy.GlobalPosition - player.GlobalPosition, burstKnockback, 0.0f);
		}
		SpawnFuryBloodBurst(player.GlobalPosition);
		GetTree().CallGroup("hud", "AppendGameLog", $"A Fúria terminou com uma explosão de {Mathf.RoundToInt(damage)} de dano base.");
	}

	private void CreateFuryVisualOverlay()
	{
		_furyVisualLayer = new CanvasLayer
		{
			Name = "BestialFuryVisualLayer",
			Layer = 1,
			ProcessMode = ProcessModeEnum.Pausable
		};
		AddChild(_furyVisualLayer);

		var shader = new Shader
		{
			Code = @"shader_type canvas_item;
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear;

void fragment() {
	vec4 source = texture(screen_texture, SCREEN_UV);
	float gray = dot(source.rgb, vec3(0.299, 0.587, 0.114));
	float blood_signal = source.r - max(source.g, source.b) * 1.12;
	float preserve_red = smoothstep(0.075, 0.32, blood_signal);
	vec3 monochrome = vec3(gray);
	vec3 blood = vec3(max(source.r, gray * 0.72), source.g * 0.13, source.b * 0.13);
	float vignette = smoothstep(0.82, 0.24, distance(SCREEN_UV, vec2(0.5)));
	vec3 result = mix(monochrome, blood, preserve_red) * mix(0.72, 1.0, vignette);
	COLOR = vec4(result, source.a);
}"
		};

		_furyVisualOverlay = new ColorRect
		{
			Name = "BestialFuryGrayscale",
			Color = Colors.White,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = shader },
			Visible = false
		};
		_furyVisualOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		_furyVisualLayer.AddChild(_furyVisualOverlay);
	}

	private void SetFuryVisualActive(bool active)
	{
		if (_furyVisualOverlay != null)
			_furyVisualOverlay.Visible = active;
	}

	private async void SpawnFuryBloodBurst(Vector3 position)
	{
		var scene = GetTree().CurrentScene;
		if (scene == null) return;

		var burst = new Node3D { Name = "BestialFuryBloodBurst" };
		scene.AddChild(burst);
		burst.GlobalPosition = position + Vector3.Up * 0.7f;
		for (var i = 0; i < 7; i++)
		{
			var drop = new MeshInstance3D
			{
				Mesh = new SphereMesh { Radius = 0.055f + GD.Randf() * 0.045f, Height = 0.13f + GD.Randf() * 0.09f },
				MaterialOverride = new StandardMaterial3D
				{
					AlbedoColor = new Color(0.82f, 0.0f, 0.025f),
					EmissionEnabled = true,
					Emission = new Color(1.0f, 0.0f, 0.025f),
					EmissionEnergyMultiplier = 3.2f,
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
				}
			};
			burst.AddChild(drop);
			var angle = Mathf.Tau * i / 7.0f + GD.Randf() * 0.4f;
			var direction = new Vector3(Mathf.Cos(angle), (float)GD.RandRange(0.25, 0.85), Mathf.Sin(angle));
			var tween = drop.CreateTween();
			tween.TweenProperty(drop, "position", direction * (float)GD.RandRange(0.45, 0.9), 0.24f)
				.SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			tween.Parallel().TweenProperty(drop, "scale", Vector3.One * 0.12f, 0.30f);
			tween.Parallel().TweenProperty(drop, "transparency", 1.0f, 0.30f);
		}

		await ToSignal(GetTree().CreateTimer(0.34f), SceneTreeTimer.SignalName.Timeout);
		if (IsInstanceValid(burst)) burst.QueueFree();
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
			_lightInputBufferTimer = 0.0f;
		}
	}

	private void SetAttackPhase(PlayerAttackPhase phase, float duration)
	{
		CurrentAttackPhase = phase;
		_attackPhaseElapsed = 0.0f;
		_attackPhaseDuration = Mathf.Max(duration, 0.0f);
	}

	private float GetAttackPhaseProgress()
	{
		if (_attackPhaseDuration <= 0.001f)
			return 1.0f;
		return Mathf.Clamp(_attackPhaseElapsed / _attackPhaseDuration, 0.0f, 1.0f);
	}

	private Vector3 GetAssistedAttackDirection(Vector3 rawDirection, WeaponAttackDefinition attack)
	{
		rawDirection = NormalizeFallback(rawDirection);
		var control = attack.Control ?? AttackControlProfile.Default;
		var assistStrength = Mathf.Clamp(control.AimAssistStrength, 0.0f, 1.0f);
		var assistRadius = control.AimAssistRadius > 0.0f ? control.AimAssistRadius : AutoAimRadius;
		if (CompendiumTuning.HasOverride("combat.aim.radius"))
			assistRadius = CompendiumTuning.GetFloat("combat.aim.radius", assistRadius);
		if (assistStrength <= 0.0f || assistRadius <= 0.0f)
			return rawDirection;

		var player = GetParent<PlayerController>();
		EnemyBasic bestTarget = null;
		var bestDirection = rawDirection;
		var bestScore = float.PositiveInfinity;
		var maxAngle = Mathf.DegToRad(Mathf.Max(control.AimAssistConeDegrees, 1.0f));
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not EnemyBasic enemy || enemy.GetNodeOrNull<Health>("Health") is not { IsDead: false })
				continue;

			var toTarget = enemy.GlobalPosition - player.GlobalPosition;
			toTarget.Y = 0.0f;
			var distance = toTarget.Length();
			if (distance <= 0.001f || distance > assistRadius + enemy.HitTargetRadius)
				continue;

			var targetDirection = toTarget / distance;
			var angle = Mathf.Acos(Mathf.Clamp(rawDirection.Dot(targetDirection), -1.0f, 1.0f));
			if (angle > maxAngle)
				continue;

			if (!HasAimLineOfSight(player, enemy))
				continue;

			var score = angle / maxAngle + distance / assistRadius * 0.35f;
			if (score >= bestScore)
				continue;

			bestScore = score;
			bestTarget = enemy;
			bestDirection = targetDirection;
		}

		if (bestTarget == null)
			return rawDirection;

		return rawDirection.Slerp(bestDirection, assistStrength).Normalized();
	}

	private bool HasAimLineOfSight(PlayerController player, EnemyBasic enemy)
	{
		var from = player.GlobalPosition + Vector3.Up * 0.65f;
		var to = enemy.GlobalPosition + Vector3.Up * 0.65f;
		var query = PhysicsRayQueryParameters3D.Create(from, to);
		query.CollideWithAreas = false;
		query.CollideWithBodies = true;
		query.Exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
		var collision = player.GetWorld3D().DirectSpaceState.IntersectRay(query);
		if (collision.Count == 0)
			return true;
		return collision["collider"].AsGodotObject() == enemy;
	}

	private Vector3 GetMouseAttackDirection(Vector3? fallbackDirection = null)
	{
		var stickAim = GetControllerMoveAim();
		if (stickAim.LengthSquared() > 0.12f)
			return stickAim;

		var player = GetParent<Node3D>();
		var camera = GetViewport().GetCamera3D();
		if (camera == null)
			return NormalizeFallback(fallbackDirection);

		var mousePosition = GetViewport().GetMousePosition();
		// Cursor em cima do corpo de um inimigo: mira nos pés dele. Com a câmera baixa do 2D (30°), projetar o cursor no
		// chão jogava a mira ~1,5 m para trás do alvo quando se clicava no tronco do sprite.
		if (FindEnemyUnderCursor(camera, mousePosition) is { } hovered)
		{
			var toHovered = hovered.GlobalPosition - player.GlobalPosition;
			toHovered.Y = 0.0f;
			if (toHovered.LengthSquared() > 0.001f)
				return toHovered.Normalized();
		}

		var rayOrigin = camera.ProjectRayOrigin(mousePosition);
		var rayDirection = camera.ProjectRayNormal(mousePosition);

		if (Mathf.Abs(rayDirection.Y) <= 0.001f)
			return NormalizeFallback(fallbackDirection);

		// Plano da mira na altura do tronco (combat.aim.plane_height), onde o olho lê a posição dos personagens.
		var planeY = player.GlobalPosition.Y + CompendiumTuning.GetFloat("combat.aim.plane_height", DefaultAimPlaneHeight);
		var t = (planeY - rayOrigin.Y) / rayDirection.Y;
		if (t <= 0.0f)
			return NormalizeFallback(fallbackDirection);

		var target = rayOrigin + rayDirection * t;
		var direction = target - player.GlobalPosition;
		direction.Y = 0.0f;

		return direction.LengthSquared() > 0.001f
			? direction.Normalized()
			: NormalizeFallback(fallbackDirection);
	}

	// Inimigo vivo cujo corpo (pés até ~altura do sprite, largura do raio de acerto) contém o cursor na tela; o mais à
	// frente da câmera vence.
	private EnemyBasic FindEnemyUnderCursor(Camera3D camera, Vector2 mousePosition)
	{
		var height = CompendiumTuning.GetFloat("combat.aim.enemy_pick_height", DefaultEnemyPickHeight);
		if (height <= 0.0f)
			return null;
		var right = camera.GlobalBasis.X;
		EnemyBasic best = null;
		var bestDepth = float.PositiveInfinity;
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not EnemyBasic enemy || !enemy.Visible || enemy.GetNodeOrNull<Health>("Health") is not { IsDead: false })
				continue;
			var feet = enemy.GlobalPosition;
			if (camera.IsPositionBehind(feet))
				continue;
			var feetScreen = camera.UnprojectPosition(feet);
			var headScreen = camera.UnprojectPosition(feet + Vector3.Up * height);
			var halfWidth = (camera.UnprojectPosition(feet + right * enemy.HitTargetRadius) - feetScreen).Length();
			if (DistanceToSegment(mousePosition, feetScreen, headScreen) > halfWidth)
				continue;
			var depth = camera.GlobalPosition.DistanceSquaredTo(feet);
			if (depth >= bestDepth)
				continue;
			bestDepth = depth;
			best = enemy;
		}
		return best;
	}

	// ---------- Hitboxes (só debug: comando "hitbox" do console) ----------

	/// <summary>Desenha no chão as áreas usadas no teste de acerto. Só em build de debug.</summary>
	public static bool DevShowHitboxes { get; set; }

	private WeaponAttackDefinition _lastDevAttack;
	private MeshInstance3D _devHitboxMesh;
	private ImmediateMesh _devHitboxLines;

	private void UpdateDevHitboxOverlay()
	{
		if (!DevShowHitboxes || !OS.IsDebugBuild())
		{
			if (_devHitboxMesh != null)
				_devHitboxMesh.Visible = false;
			return;
		}

		var player = GetParent<Node3D>();
		if (_devHitboxMesh == null)
		{
			_devHitboxLines = new ImmediateMesh();
			_devHitboxMesh = new MeshInstance3D
			{
				Name = "DevHitboxOverlay",
				TopLevel = true,
				Mesh = _devHitboxLines,
				CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
				MaterialOverride = new StandardMaterial3D
				{
					ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
					VertexColorUseAsAlbedo = true,
					NoDepthTest = true,
					RenderPriority = 10
				}
			};
			player.AddChild(_devHitboxMesh);
		}
		_devHitboxMesh.Visible = true;
		_devHitboxMesh.GlobalTransform = Transform3D.Identity;

		var lines = _devHitboxLines;
		lines.ClearSurfaces();
		lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
		var origin = player.GlobalPosition + Vector3.Up * 0.05f;
		DevCircle(lines, origin, PlayerHurtRadius, new Color(0.3f, 1.0f, 0.4f));
		var aim = GetMouseAttackDirection(_attackDirection);
		DevLine(lines, origin, origin + aim * 2.5f, new Color(1.0f, 1.0f, 1.0f));

		foreach (var node in GetTree().GetNodesInGroup("enemies"))
		{
			if (node is not EnemyBasic enemy || enemy.GetNodeOrNull<Health>("Health") is not { IsDead: false })
				continue;
			DevCircle(lines, enemy.GlobalPosition + Vector3.Up * 0.05f, enemy.HitTargetRadius, new Color(1.0f, 0.85f, 0.2f));
		}

		// Golpe atual: a área sem o raio dos alvos (o inimigo é acertado quando o círculo amarelo encosta nela).
		if (_isAttacking && _lastDevAttack is { } attack)
		{
			var color = new Color(0.3f, 0.9f, 1.0f);
			var reach = attack.Range + GetEdgeForgiveness(CurrentAttackType);
			var forward = _attackDirection;
			var side = new Vector3(-forward.Z, 0.0f, forward.X);
			if (attack.Shape == WeaponAttackShape.Radial)
				DevCircle(lines, origin, reach, color);
			else if (attack.Shape == WeaponAttackShape.Line)
			{
				var half = side * attack.Width * 0.5f;
				var tip = forward * reach;
				DevLine(lines, origin - half, origin + half, color);
				DevLine(lines, origin + half, origin + half + tip, color);
				DevLine(lines, origin + half + tip, origin - half + tip, color);
				DevLine(lines, origin - half + tip, origin - half, color);
			}
			else
			{
				var halfArc = Mathf.DegToRad(EquippedWeapon.TunedAttackArcDegrees * 0.5f);
				const int segments = 20;
				var previous = origin;
				for (var i = 0; i <= segments; i++)
				{
					var point = origin + forward.Rotated(Vector3.Up, Mathf.Lerp(-halfArc, halfArc, i / (float)segments)) * reach;
					DevLine(lines, previous, point, color);
					previous = point;
				}
				DevLine(lines, previous, origin, color);
			}
		}
		lines.SurfaceEnd();
	}

	private static void DevCircle(ImmediateMesh lines, Vector3 center, float radius, Color color)
	{
		const int segments = 28;
		for (var i = 0; i < segments; i++)
		{
			var a0 = Mathf.Tau * i / segments;
			var a1 = Mathf.Tau * (i + 1) / segments;
			DevLine(lines, center + new Vector3(Mathf.Cos(a0), 0.0f, Mathf.Sin(a0)) * radius, center + new Vector3(Mathf.Cos(a1), 0.0f, Mathf.Sin(a1)) * radius, color);
		}
	}

	private static void DevLine(ImmediateMesh lines, Vector3 from, Vector3 to, Color color)
	{
		lines.SurfaceSetColor(color);
		lines.SurfaceAddVertex(from);
		lines.SurfaceSetColor(color);
		lines.SurfaceAddVertex(to);
	}

	private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		var segment = b - a;
		var lengthSquared = segment.LengthSquared();
		var progress = lengthSquared > 0.0001f ? Mathf.Clamp((point - a).Dot(segment) / lengthSquared, 0.0f, 1.0f) : 0.0f;
		return point.DistanceTo(a + segment * progress);
	}

	private static Vector3 GetControllerMoveAim()
	{
		var strongest = Vector2.Zero;
		foreach (var device in Input.GetConnectedJoypads())
		{
			var candidate = new Vector2(
				Input.GetJoyAxis(device, (JoyAxis)0),
				Input.GetJoyAxis(device, (JoyAxis)1));
			if (candidate.LengthSquared() > strongest.LengthSquared()) strongest = candidate;
		}
		if (strongest.LengthSquared() <= 0.12f) return Vector3.Zero;
		return new Vector3(strongest.X, 0.0f, strongest.Y)
			.Rotated(Vector3.Up, Mathf.DegToRad(45.0f)).Normalized();
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

	private WeaponAttackDefinition GetTunedAttack(WeaponAttackDefinition attack, string keyPrefix, PlayerAttackType attackType)
	{
		// Modificacoes da Bigorna (docs/design/bigorna.md): ultimo multiplicador, sobre o valor ja tunado. Sem mods todos valem 1.
		var rangeMod = RunState.GetWeaponModMultiplier("AttackRange.Multiplier");
		if (attackType == PlayerAttackType.Special)
			rangeMod *= RunState.GetWeaponModMultiplier("SpecialRadius.Multiplier");
		var timingMod = 1.0f - Mathf.Clamp(RunState.GetWeaponModTotal("AttackTiming.Reduction"), 0.0f, 0.9f);
		return new WeaponAttackDefinition(
			attack.NameKey,
			CompendiumTuning.GetFloat($"{keyPrefix}.damage", attack.Damage),
			CompendiumTuning.GetFloat($"{keyPrefix}.hit_delay", attack.HitDelay) * timingMod,
			CompendiumTuning.GetFloat($"{keyPrefix}.recovery", attack.Recovery) * timingMod,
			CompendiumTuning.GetFloat($"{keyPrefix}.knockback", attack.Knockback) * RunState.GetWeaponModMultiplier("Knockback.Multiplier"),
			CompendiumTuning.GetFloat($"{keyPrefix}.range", attack.Range) * EquippedWeapon.TunedRangeMultiplier * rangeMod,
			CompendiumTuning.GetFloat($"{keyPrefix}.width", attack.Width),
			attack.SlashTilt,
			attack.Shape,
			CompendiumTuning.GetFloat($"{keyPrefix}.stagger", attack.StaggerDuration) * RunState.GetWeaponModMultiplier("Stagger.Multiplier"),
			attack.Control,
			GetTunedMaxTargets(attack, keyPrefix, attackType)
		);
	}

	// Limite de alvos do golpe (0 = todos). Bala Perfurante (Pierce.Flat) soma alvos ao disparo basico.
	private static int GetTunedMaxTargets(WeaponAttackDefinition attack, string keyPrefix, PlayerAttackType attackType)
	{
		var maxTargets = Mathf.Max(Mathf.RoundToInt(CompendiumTuning.GetFloat($"{keyPrefix}.max_targets", attack.MaxTargets)), 0);
		if (maxTargets > 0 && attackType == PlayerAttackType.Basic)
			maxTargets += Mathf.Max(Mathf.RoundToInt(RunState.GetWeaponModTotal("Pierce.Flat")), 0);
		return maxTargets;
	}

	// --- Municao (armas com MagazineSize > 0) ---

	private void RefreshAmmoForEquippedWeapon()
	{
		_ammoWeaponId = EquippedWeapon.Id;
		_reloadTimer = 0.0f;
		_ammo = MaxAmmo;
		EmitAmmoChanged();
	}

	private void TickAmmo(float delta)
	{
		if (_ammoWeaponId != EquippedWeapon.Id)
		{
			RefreshAmmoForEquippedWeapon();
			return;
		}
		if (!UsesAmmo)
			return;

		var maxAmmo = MaxAmmo;
		if (maxAmmo != _lastEmittedMaxAmmo)
		{
			// Tambor Largo pego no meio da run: as balas novas ja chegam carregadas.
			if (_lastEmittedMaxAmmo > 0 && maxAmmo > _lastEmittedMaxAmmo && !IsReloading)
				_ammo += maxAmmo - _lastEmittedMaxAmmo;
			_ammo = Mathf.Min(_ammo, maxAmmo);
			EmitAmmoChanged();
		}

		if (_reloadTimer <= 0.0f)
			return;
		_reloadTimer -= delta;
		if (_reloadTimer <= 0.0f)
		{
			GetTree().CallGroup("audio_manager", "PlayEffect", "gun_reload");
			RefillAmmo();
		}
	}

	private bool TryConsumeAmmo()
	{
		if (IsReloading)
			return false;
		if (_ammo <= 0)
		{
			StartReload();
			return false;
		}

		_ammo--;
		if (_ammo <= 0)
			StartReload();
		else
			EmitAmmoChanged();
		return true;
	}

	private void StartReload()
	{
		if (IsReloading)
			return;
		var reduction = Mathf.Clamp(RunState.GetWeaponModTotal("Reload.Reduction"), 0.0f, 0.9f);
		_reloadTimer = Mathf.Max(EquippedWeapon.TunedReloadSeconds * (1.0f - reduction), 0.05f);
		EmitAmmoChanged();
	}

	private void RefillAmmo()
	{
		_reloadTimer = 0.0f;
		_ammo = MaxAmmo;
		EmitAmmoChanged();
	}

	private void EmitAmmoChanged()
	{
		_lastEmittedMaxAmmo = MaxAmmo;
		EmitSignal(SignalName.AmmoChanged, _ammo, _lastEmittedMaxAmmo, IsReloading);
	}

	// Distancia ate a primeira parede/obstaculo na direcao do tiro (inimigos nao bloqueiam; o limite de alvos cuida deles).
	private float GetShotWallDistance(float maxDistance)
	{
		var player = GetParent<PlayerController>();
		var from = player.GlobalPosition + Vector3.Up * 0.65f;
		var to = from + _attackDirection * maxDistance;
		var exclude = new Godot.Collections.Array<Rid> { player.GetRid() };
		var space = player.GetWorld3D().DirectSpaceState;
		for (var attempt = 0; attempt < 8; attempt++)
		{
			var query = PhysicsRayQueryParameters3D.Create(from, to);
			query.CollideWithAreas = false;
			query.CollideWithBodies = true;
			query.Exclude = exclude;
			var collision = space.IntersectRay(query);
			if (collision.Count == 0)
				return maxDistance;
			if (collision["collider"].AsGodotObject() is EnemyBasic enemy)
			{
				exclude.Add(enemy.GetRid());
				continue;
			}
			var point = collision["position"].AsVector3();
			return Mathf.Clamp(((point - from) with { Y = 0.0f }).Length(), 0.0f, maxDistance);
		}
		return maxDistance;
	}

	// Rastro de bala em espaco de mundo, saindo da mao do disparo (alterna a cada tiro do combo).
	private async void SpawnBulletTracer(float reach, PlayerAttackType attackType)
	{
		var player = GetParent<Node3D>();
		var world = player.GetParent<Node>();
		if (world == null || reach <= 0.05f)
			return;

		var isSpecial = attackType == PlayerAttackType.Special;
		var side = new Vector3(-_attackDirection.Z, 0.0f, _attackDirection.X) * (_lightComboIndex % 2 == 0 ? 0.22f : -0.22f);
		var muzzle = player.GlobalPosition + Vector3.Up * 1.0f + _attackDirection * 0.55f + (isSpecial ? Vector3.Zero : side);
		var length = Mathf.Max(reach - 0.55f, 0.1f);
		var material = new StandardMaterial3D
		{
			AlbedoColor = isSpecial ? new Color(0.85f, 0.95f, 1.0f, 0.95f) : new Color(1.0f, 0.92f, 0.65f, 0.90f),
			EmissionEnabled = true,
			Emission = isSpecial ? new Color(0.65f, 0.85f, 1.0f) : new Color(1.0f, 0.80f, 0.40f),
			EmissionEnergyMultiplier = isSpecial ? 3.2f : 2.4f,
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};
		ApplyBloodlineTint(attackType, null, material);
		var thickness = isSpecial ? 0.16f : 0.05f;
		var tracer = new MeshInstance3D
		{
			Name = "BulletTracer",
			Mesh = new BoxMesh { Size = new Vector3(thickness, thickness, length) },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		world.AddChild(tracer);
		tracer.GlobalPosition = muzzle + _attackDirection * (length * 0.5f);
		tracer.LookAt(tracer.GlobalPosition + _attackDirection, Vector3.Up);

		var flash = new MeshInstance3D
		{
			Name = "MuzzleFlash",
			Mesh = new SphereMesh { Radius = isSpecial ? 0.22f : 0.12f, Height = isSpecial ? 0.44f : 0.24f },
			MaterialOverride = material,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		world.AddChild(flash);
		flash.GlobalPosition = muzzle;

		var tween = tracer.CreateTween();
		tween.SetParallel(true);
		tween.TweenProperty(material, "albedo_color:a", 0.0f, isSpecial ? 0.18f : 0.09f);
		tween.TweenProperty(flash, "scale", Vector3.One * 0.2f, 0.07f);
		await ToSignal(tween, Tween.SignalName.Finished);
		if (IsInstanceValid(tracer)) tracer.QueueFree();
		if (IsInstanceValid(flash)) flash.QueueFree();
	}

	private float GetTunedMaxFury()
	{
		return CompendiumTuning.GetFloat("attack.fury.max_fury", _baseMaxFury) * CharacterSheet.GetFuryMultiplier();
	}

}
