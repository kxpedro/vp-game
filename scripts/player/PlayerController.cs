using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Signal]
	public delegate void StaminaChangedEventHandler(float currentStamina, float maxStamina);

	public const float DefaultWalkSpeed = 3.6f;
	public const float DefaultSprintSpeed = 7.4f;
	public const float DefaultDodgeSpeed = 13.5f;
	public const float DefaultDodgeDuration = 0.22f;
	public const float DefaultDashAttackGraceSeconds = 0.5f;
	public const float DefaultDodgeCooldownSeconds = 0.18f;
	public const float DefaultDodgeInputBufferSeconds = 0.28f;
	public const float DefaultMaxStamina = 100.0f;
	public const float DefaultStaminaRegenPerSecond = 28.0f;
	public const float DefaultDamageStaggerDuration = 0.24f;
	// Vida base do jogador: Health.MaxHealth em Player.tscn (100).
	public const float DefaultMaxHealth = 100.0f;

	[Export] public float WalkSpeed { get; set; } = DefaultWalkSpeed;
	[Export] public float SprintSpeed { get; set; } = DefaultSprintSpeed;
	[Export] public float DodgeSpeed { get; set; } = DefaultDodgeSpeed;
	[Export] public float DodgeDuration { get; set; } = DefaultDodgeDuration;
	[Export] public float DashAttackGraceSeconds { get; set; } = DefaultDashAttackGraceSeconds;
	[Export] public float DodgeCooldownSeconds { get; set; } = DefaultDodgeCooldownSeconds;
	[Export] public float DodgeInputBufferSeconds { get; set; } = DefaultDodgeInputBufferSeconds;
	[Export] public float MaxStamina { get; set; } = DefaultMaxStamina;
	[Export] public float StaminaRegenPerSecond { get; set; } = DefaultStaminaRegenPerSecond;
	[Export] public float DodgeCost { get; set; } = 28.0f; // sem uso no codigo (nao parametrizado)
	[Export] public float DamageStaggerDuration { get; set; } = DefaultDamageStaggerDuration;
	[Export] public NodePath HealthPath { get; set; } = new("Health");

	public float CurrentStamina { get; private set; }
	public bool IsDodging => _dodgeTimeRemaining > 0.0f;
	public bool CanDashAttack => IsDodging || _dashAttackGraceTimeRemaining > 0.0f;
	public bool IsStaggered => _damageStaggerTimeRemaining > 0.0f;
	public Vector3 DodgeDirection => _dodgeDirection;
	// Indicador de recarga do HUD: 1 = acabou de esquivar, 0 = pronta.
	public float DodgeCooldownRatio => _dodgeCooldownTotal > 0.0f ? Mathf.Clamp(_dodgeCooldownTimeRemaining / _dodgeCooldownTotal, 0.0f, 1.0f) : 0.0f;
	public Vector3 LastMoveDirection => _lastMoveDirection;
	public float DodgeAnimationSpeedMultiplier => BaseDodgeSpeed > 0.0f ? DodgeSpeed / BaseDodgeSpeed : 1.0f;
	private float BaseDodgeSpeed => CompendiumTuning.GetFloat("player.dodge.speed", _defaultDodgeSpeed);

	private Health _health;
	private PlayerSkillController _skillController;
	private PlayerCombat _combat;
	private PlayerAnimator _animator;
	private BloodBlessingRuntime _blessings;
	// Padroes vindos da cena (Export); as chaves player.* do CompendiumTuning sobrescrevem em ApplyRuntimeTuning.
	private float _defaultWalkSpeed;
	private float _defaultSprintSpeed;
	private float _defaultDodgeSpeed;
	private float _defaultDodgeDuration;
	private float _defaultMaxHealth;
	private float _defaultMaxStamina;
	private Vector3 _lastMoveDirection = Vector3.Forward;
	private Vector3 _dodgeDirection = Vector3.Zero;
	private float _dodgeTimeRemaining;
	private float _dashAttackGraceTimeRemaining;
	private float _dodgeCooldownTimeRemaining;
	private float _dodgeCooldownTotal;
	private float _dodgeInputBufferTimer;
	private float _damageStaggerTimeRemaining;
	// Passos automáticos ao atravessar um portão (RoomTransition): sem controle do jogador.
	private Vector3 _scriptedWalkDirection;
	private float _scriptedWalkTimeRemaining;

	public override void _Ready()
	{
		AddToGroup("player");
		_defaultWalkSpeed = WalkSpeed;
		_defaultSprintSpeed = SprintSpeed;
		_defaultDodgeSpeed = DodgeSpeed;
		_defaultDodgeDuration = DodgeDuration;
		CurrentStamina = MaxStamina;
		_defaultMaxStamina = MaxStamina;
		_health = GetNode<Health>(HealthPath);
		_skillController = GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
		_combat = GetNodeOrNull<PlayerCombat>("PlayerCombat");
		_animator = GetNodeOrNull<PlayerAnimator>("PlayerAnimator");
		_blessings = GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		_defaultMaxHealth = _health.MaxHealth;

		GameTuning.Changed += ApplyRuntimeTuning;
		CompendiumTuning.Changed += OnCompendiumTuningChanged;
		ApplyRuntimeTuning();
		CurrentStamina = MaxStamina;
		if (RunState.SavedPlayerHealth >= 0.0f)
			RestoreSavedHealth();
		else
			_health.SetCurrentHealth(_health.MaxHealth);

		_health.HealthChanged += OnHealthChanged;
		_health.Damaged += OnDamaged;
		_health.Died += OnDied;
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}

	public override void _ExitTree()
	{
		GameTuning.Changed -= ApplyRuntimeTuning;
		CompendiumTuning.Changed -= OnCompendiumTuningChanged;
		if (_health != null)
		{
			_health.HealthChanged -= OnHealthChanged;
			_health.Damaged -= OnDamaged;
			_health.Died -= OnDied;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_health.IsDead)
		{
			_animator?.SetMovementState(false, false, 0.0f);
			return;
		}
		if (_scriptedWalkTimeRemaining > 0.0f)
		{
			_scriptedWalkTimeRemaining -= (float)delta;
			Velocity = _scriptedWalkTimeRemaining > 0.0f ? _scriptedWalkDirection * WalkSpeed : Vector3.Zero;
			UpdateFacing(_scriptedWalkDirection);
			_animator?.SetMovementState(_scriptedWalkTimeRemaining > 0.0f, false, WalkSpeed);
			MoveAndSlide();
			return;
		}
		if (GetTree().GetFirstNodeInGroup("hud") is HUD hud && hud.IsGameplayInputBlocked())
		{
			Velocity = Vector3.Zero;
			_animator?.SetMovementState(false, false, 0.0f);
			return;
		}

		var deltaF = (float)delta;
		RunState.Tick(deltaF);
		_dodgeCooldownTimeRemaining = Mathf.Max(_dodgeCooldownTimeRemaining - deltaF, 0.0f);
		_dodgeInputBufferTimer = Mathf.Max(_dodgeInputBufferTimer - deltaF, 0.0f);
		if (Input.IsActionJustPressed("dodge"))
			_dodgeInputBufferTimer = CompendiumTuning.GetFloat("player.dodge.buffer", DodgeInputBufferSeconds);
		if (_damageStaggerTimeRemaining > 0.0f)
		{
			_damageStaggerTimeRemaining = Mathf.Max(_damageStaggerTimeRemaining - deltaF, 0.0f);
			Velocity = Vector3.Zero;
			_animator?.SetMovementState(false, false, 0.0f);
			return;
		}
		if (_dashAttackGraceTimeRemaining > 0.0f)
		{
			_dashAttackGraceTimeRemaining = Mathf.Max(_dashAttackGraceTimeRemaining - deltaF, 0.0f);
			if (_dashAttackGraceTimeRemaining <= 0.0f && !IsDodging)
				_health?.SetInvincible(false);
		}

		if (!IsDodging && !CanDashAttack && (_combat == null || !_combat.IsQuickAttacking) && _health.IsInvincible)
			_health.SetInvincible(false);

		var moveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		var moveDirection = GetIsometricDirection(moveInput);

		if (moveDirection.LengthSquared() > 0.001f)
			_lastMoveDirection = moveDirection;

		UpdateFacing(moveDirection);

		if (_dodgeTimeRemaining > 0.0f)
		{
			if (Input.IsActionJustPressed("attack_light") && !(_combat?.IsPointerAttackBlocked() ?? false))
				_combat?.QueueQuickAttack();
			_dodgeTimeRemaining -= deltaF;
			Velocity = _dodgeDirection * DodgeSpeed;
			_animator?.SetMovementState(true, true, DodgeSpeed);
			MoveAndSlide();

			if (_dodgeTimeRemaining <= 0.0f)
				EndDodge();

			return;
		}

		if (_dodgeInputBufferTimer > 0.0f
			&& _dodgeCooldownTimeRemaining <= 0.0f
			&& (_combat?.CanCancelCurrentAttack(AttackCancelAction.Dodge) ?? true))
		{
			var perfectDodge = IsEnemyAttackThreatening();
			var hasBufferedQuickAttack = Input.IsActionJustPressed("attack_light") || (_combat?.HasBufferedQuickAttack ?? false);
			if (_combat?.TryCancelCurrentAttack(AttackCancelAction.Dodge) == false)
				return;
			_dodgeInputBufferTimer = 0.0f;
			if (hasBufferedQuickAttack && !(_combat?.IsPointerAttackBlocked() ?? false))
				_combat?.QueueQuickAttack();
			_dodgeDirection = moveDirection.LengthSquared() > 0.001f ? moveDirection : _lastMoveDirection;
			_dodgeTimeRemaining = DodgeDuration;
			_dodgeCooldownTimeRemaining = DodgeDuration + CompendiumTuning.GetFloat("player.dodge.cooldown", DodgeCooldownSeconds);
			_dodgeCooldownTotal = _dodgeCooldownTimeRemaining;
			_dashAttackGraceTimeRemaining = 0.0f;
			_health?.SetInvincible(true);
			_blessings?.OnDodged();
			GetTree().CallGroup("audio_manager", "PlayEffect", "dodge");
			if (perfectDodge)
				_skillController?.OnPerfectDodge();
			Velocity = _dodgeDirection * DodgeSpeed;
			_animator?.PlayDodge(DodgeDuration);
			_animator?.SetMovementState(true, true, DodgeSpeed);
			MoveAndSlide();
			return;
		}

		var isWalking = IsWalkPressed() && moveDirection.LengthSquared() > 0.001f;

		var speed = isWalking ? WalkSpeed : SprintSpeed;
		var missingHealth = _health.MaxHealth > 0 ? 1.0f - _health.CurrentHealth / _health.MaxHealth : 0;
		speed *= 1.0f + RunState.GetEffectTotal("MoveSpeedMissingHealth.Multiplier") * missingHealth + RunState.ContextMoveSpeed;
		speed *= _blessings?.MovementMultiplier() ?? 1.0f;
		speed *= _skillController?.MovementMultiplier() ?? 1.0f;
		speed *= _combat?.CurrentMovementMultiplier ?? 1.0f;

		Velocity = moveDirection * speed;
		_animator?.SetMovementState(moveDirection.LengthSquared() > 0.001f, !isWalking, speed);
		MoveAndSlide();
		RegenerateStamina(deltaF);
	}

	private Vector3 GetIsometricDirection(Vector2 input)
	{
		if (input.LengthSquared() <= 0.001f)
			return Vector3.Zero;

		var direction = new Vector3(input.X, 0.0f, input.Y).Normalized();
		return direction.Rotated(Vector3.Up, Mathf.DegToRad(45.0f)).Normalized();
	}

	private bool IsWalkPressed()
	{
		return Input.IsKeyPressed(Key.Capslock)
			|| Input.IsPhysicalKeyPressed(Key.Capslock);
	}

	private bool IsEnemyAttackThreatening()
	{
		foreach (var node in GetTree().GetNodesInGroup("enemies"))
			if (node is EnemyBasic enemy && enemy.IsThreateningDodge(GlobalPosition))
				return true;
		return false;
	}

	public void ApplyRuntimeTuning()
	{
		var moveMultiplier = CharacterSheet.GetMoveSpeedMultiplier() * (1.0f + RunState.GetEffectTotal("MoveSpeed.Multiplier"));
		WalkSpeed = CompendiumTuning.GetFloat("player.walk_speed", _defaultWalkSpeed) * moveMultiplier * GameTuning.GetWalkSpeedMultiplier();
		SprintSpeed = CompendiumTuning.GetFloat("player.sprint_speed", _defaultSprintSpeed) * moveMultiplier * GameTuning.GetRunSpeedMultiplier();
		DodgeSpeed = BaseDodgeSpeed * GameTuning.GetDashSpeedMultiplier() * (1.0f + RunState.GetEffectTotal("DashSpeed.Multiplier"));
		DodgeDuration = CompendiumTuning.GetFloat("player.dodge.duration", _defaultDodgeDuration) * GameTuning.GetDashDistanceMultiplier();
		MaxStamina = CompendiumTuning.GetFloat("player.stamina.max", _defaultMaxStamina) + CharacterSheet.GetMaxStaminaBonus();
		CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);

		var attributeHealthBonus = CharacterSheet.GetMaxHealthBonus() + (_skillController?.MaxHealthBonus() ?? 0.0f);
		var tunedMaxHealth = ((CompendiumTuning.GetFloat("player.max_health", _defaultMaxHealth) + attributeHealthBonus) * GameTuning.GetHealthMultiplier()
			+ RunState.MaxHealthBonus)
			* RunState.MaxHealthMultiplier
			* RunState.SacrificedMaxHealthMultiplier
			+ RunState.DevilsFruitMaxHealthTotal;
		_health?.SetMaxHealthPreservingMissingHealth(tunedMaxHealth);
	}

	private void OnCompendiumTuningChanged(string key)
	{
		if (key == null || key.StartsWith("player."))
			ApplyRuntimeTuning();
	}

	public void ConsumeDashAttackGrace()
	{
		_dashAttackGraceTimeRemaining = 0.0f;
	}

	public void PerformAttackLunge(Vector3 direction, float distance)
	{
		direction.Y = 0.0f;
		if (direction.LengthSquared() <= 0.001f || distance <= 0.0f)
			return;
		MoveAndCollide(direction.Normalized() * distance);
	}

	/// <summary>Anda sozinho na direção dada (no ritmo de caminhada) por <paramref name="seconds"/>; cancela a esquiva.</summary>
	public void StartScriptedWalk(Vector3 direction, float seconds)
	{
		direction.Y = 0.0f;
		if (direction.LengthSquared() < 0.001f || seconds <= 0.0f)
			return;
		if (_dodgeTimeRemaining > 0.0f)
		{
			_dodgeTimeRemaining = 0.0f;
			EndDodge();
		}
		_combat?.CancelCurrentAttack();
		_scriptedWalkDirection = direction.Normalized();
		_scriptedWalkTimeRemaining = seconds;
		_lastMoveDirection = _scriptedWalkDirection;
	}

	public bool IsScriptedWalking => _scriptedWalkTimeRemaining > 0.0f;

	private void UpdateFacing(Vector3 fallbackDirection)
	{
		if (_combat != null && _combat.IsAttackFacingLocked)
		{
			LookAt(GlobalPosition + _combat.AttackFacingDirection, Vector3.Up);
			return;
		}

		if (fallbackDirection.LengthSquared() > 0.001f)
			LookAt(GlobalPosition + fallbackDirection, Vector3.Up);
	}

	private void EndDodge()
	{
		if (_combat != null && _combat.TryReleaseQueuedQuickAttack(_dodgeDirection))
			return;

		_dashAttackGraceTimeRemaining = CompendiumTuning.GetFloat("player.dodge.grace", DashAttackGraceSeconds);
	}

	// DEBUG (painel de dev, aba Ações): stamina sempre cheia. Só vale em build de debug.
	public static bool DevInfiniteStamina { get; set; }

	private void RegenerateStamina(float delta)
	{
		var previous = CurrentStamina;
		CurrentStamina = DevInfiniteStamina && OS.IsDebugBuild()
			? MaxStamina
			: Mathf.Min(CurrentStamina + CompendiumTuning.GetFloat("player.stamina.regen", StaminaRegenPerSecond) * delta, MaxStamina);

		if (!Mathf.IsEqualApprox(previous, CurrentStamina))
			EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}

	private void OnDied()
	{
		Velocity = Vector3.Zero;
		_animator?.SetMovementState(false, false, 0.0f);
		SetPhysicsProcess(false);
	}

	private void OnDamaged(float amount)
	{
		if (amount <= 0.0f || _health.IsDead || IsDodging)
			return;

		var staggerDuration = CompendiumTuning.GetFloat("player.hit_stagger", DamageStaggerDuration);
		_damageStaggerTimeRemaining = Mathf.Max(_damageStaggerTimeRemaining, staggerDuration);
		_dodgeTimeRemaining = 0.0f;
		_dashAttackGraceTimeRemaining = 0.0f;
		_combat?.CancelCurrentAttack();
		Velocity = Vector3.Zero;
		_animator?.SetMovementState(false, false, 0.0f);
		_animator?.PlayHitReaction(staggerDuration);
		CombatFeedback.Instance?.PlayPlayerDamaged(amount);
	}

	private void RestoreSavedHealth()
	{
		if (RunState.SavedPlayerHealth >= 0.0f)
			_health.SetCurrentHealth(RunState.SavedPlayerHealth);
	}

	private void OnHealthChanged(float currentHealth, float maxHealth)
	{
		if (currentHealth > 0.0f)
			RunState.SavePlayerHealth(currentHealth);
	}
}
