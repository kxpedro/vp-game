using Godot;

public partial class PlayerController : CharacterBody3D
{
	[Signal]
	public delegate void StaminaChangedEventHandler(float currentStamina, float maxStamina);

	[Export] public float WalkSpeed { get; set; } = 3.6f;
	[Export] public float SprintSpeed { get; set; } = 7.4f;
	[Export] public float DodgeSpeed { get; set; } = 13.5f;
	[Export] public float DodgeDuration { get; set; } = 0.22f;
	[Export] public float DodgeCooldown { get; set; } = 2.0f;
	[Export] public float DashAttackGraceSeconds { get; set; } = 0.5f;
	[Export] public float MaxStamina { get; set; } = 100.0f;
	[Export] public float StaminaRegenPerSecond { get; set; } = 28.0f;
	[Export] public float DodgeCost { get; set; } = 28.0f;
	[Export] public NodePath HealthPath { get; set; } = new("Health");

	public float CurrentStamina { get; private set; }
	public bool IsDodging => _dodgeTimeRemaining > 0.0f;
	public bool CanDashAttack => IsDodging || _dashAttackGraceTimeRemaining > 0.0f;
	public Vector3 DodgeDirection => _dodgeDirection;
	public Vector3 LastMoveDirection => _lastMoveDirection;
	public float DodgeCooldownRemaining => _dodgeCooldownTimeRemaining;
	public float DodgeCooldownTotal => DodgeCooldown;

	private Health _health;
	private PlayerSkillController _skillController;
	private PlayerCombat _combat;
	private PlayerAnimator _animator;
	private BloodBlessingRuntime _blessings;
	private float _baseWalkSpeed;
	private float _baseSprintSpeed;
	private float _baseDodgeSpeed;
	private float _baseDodgeDuration;
	private float _baseDodgeCooldown;
	private float _baseMaxHealth;
	private float _baseMaxStamina;
	private Vector3 _lastMoveDirection = Vector3.Forward;
	private Vector3 _dodgeDirection = Vector3.Zero;
	private float _dodgeTimeRemaining;
	private float _dodgeCooldownTimeRemaining;
	private float _dashAttackGraceTimeRemaining;

	public override void _Ready()
	{
		AddToGroup("player");
		_baseWalkSpeed = WalkSpeed;
		_baseSprintSpeed = SprintSpeed;
		_baseDodgeSpeed = DodgeSpeed;
		_baseDodgeDuration = DodgeDuration;
		_baseDodgeCooldown = DodgeCooldown;
		CurrentStamina = MaxStamina;
		_baseMaxStamina = MaxStamina;
		_health = GetNode<Health>(HealthPath);
		_skillController = GetNodeOrNull<PlayerSkillController>("PlayerSkillController");
		_combat = GetNodeOrNull<PlayerCombat>("PlayerCombat");
		_animator = GetNodeOrNull<PlayerAnimator>("PlayerAnimator");
		_blessings = GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
		_baseMaxHealth = _health.MaxHealth;

		GameTuning.Changed += ApplyRuntimeTuning;
		ApplyRuntimeTuning();
		CurrentStamina = MaxStamina;
		RestoreSavedHealth();

		_health.HealthChanged += OnHealthChanged;
		_health.Died += OnDied;
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}

	public override void _ExitTree()
	{
		GameTuning.Changed -= ApplyRuntimeTuning;
		if (_health != null)
			_health.HealthChanged -= OnHealthChanged;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_health.IsDead)
		{
			_animator?.SetMovementState(false, false, 0.0f);
			return;
		}

		var deltaF = (float)delta;
		RunState.Tick(deltaF);
		if (_dodgeCooldownTimeRemaining > 0.0f)
			_dodgeCooldownTimeRemaining = Mathf.Max(_dodgeCooldownTimeRemaining - deltaF, 0.0f);

		if (_dashAttackGraceTimeRemaining > 0.0f)
		{
			_dashAttackGraceTimeRemaining = Mathf.Max(_dashAttackGraceTimeRemaining - deltaF, 0.0f);
			if (_dashAttackGraceTimeRemaining <= 0.0f && !IsDodging)
				_health?.SetInvincible(false);
		}

		if (!IsDodging && !CanDashAttack && (_combat == null || !_combat.IsDashAttacking) && _health.IsInvincible)
			_health.SetInvincible(false);

		var moveInput = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
		var moveDirection = GetIsometricDirection(moveInput);

		if (moveDirection.LengthSquared() > 0.001f)
			_lastMoveDirection = moveDirection;

		UpdateFacing(moveDirection);

		if (_dodgeTimeRemaining > 0.0f)
		{
			_dodgeTimeRemaining -= deltaF;
			Velocity = _dodgeDirection * DodgeSpeed;
			_animator?.SetMovementState(true, true, DodgeSpeed);
			MoveAndSlide();

			if (_dodgeTimeRemaining <= 0.0f)
				EndDodge();

			return;
		}

		if (Input.IsActionJustPressed("dodge") && _dodgeCooldownTimeRemaining <= 0.0f)
		{
			_dodgeDirection = moveDirection.LengthSquared() > 0.001f ? moveDirection : _lastMoveDirection;
		_dodgeTimeRemaining = DodgeDuration;
			_dashAttackGraceTimeRemaining = 0.0f;
			_dodgeCooldownTimeRemaining = DodgeCooldown;
			_health?.SetInvincible(true);
			_blessings?.OnDodged();
			Velocity = _dodgeDirection * DodgeSpeed;
			_animator?.SetMovementState(true, true, DodgeSpeed);
			MoveAndSlide();
			return;
		}

		var isWalking = IsWalkPressed() && moveDirection.LengthSquared() > 0.001f;

		var speed = isWalking ? WalkSpeed : SprintSpeed;
		var missingHealth = _health.MaxHealth > 0 ? 1.0f - _health.CurrentHealth / _health.MaxHealth : 0;
		speed *= 1.0f + RunState.GetEffectTotal("MoveSpeedMissingHealth.Multiplier") * missingHealth + RunState.ContextMoveSpeed;
		speed *= _blessings?.MovementMultiplier() ?? 1.0f;
		if (HasSurvivalInstinctBonus())
			speed *= 1.05f;

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

	private bool HasSurvivalInstinctBonus()
	{
		return _skillController != null && _skillController.HasSurvivalInstinctSpeedBonus();
	}

	public void ApplyRuntimeTuning()
	{
		var moveMultiplier = CharacterSheet.GetMoveSpeedMultiplier() * (1.0f + RunState.GetEffectTotal("MoveSpeed.Multiplier"));
		WalkSpeed = _baseWalkSpeed * moveMultiplier * GameTuning.GetWalkSpeedMultiplier();
		SprintSpeed = _baseSprintSpeed * moveMultiplier * GameTuning.GetRunSpeedMultiplier();
		DodgeSpeed = _baseDodgeSpeed * GameTuning.GetDashSpeedMultiplier();
		DodgeDuration = _baseDodgeDuration * GameTuning.GetDashDistanceMultiplier();
		DodgeCooldown = _baseDodgeCooldown * GameTuning.GetDashCooldownMultiplier();
		MaxStamina = _baseMaxStamina + CharacterSheet.GetMaxStaminaBonus();
		CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);

		var maxHealthBonus = RunState.MaxHealthBonus + CharacterSheet.GetMaxHealthBonus();
		var tunedMaxHealth = (_baseMaxHealth + maxHealthBonus) * RunState.MaxHealthMultiplier * GameTuning.GetHealthMultiplier();
		_health?.SetMaxHealth(tunedMaxHealth, false);
	}

	public void ConsumeDashAttackGrace()
	{
		_dashAttackGraceTimeRemaining = 0.0f;
	}

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
		if (_combat != null && _combat.TryReleaseQueuedDashAttack(_dodgeDirection))
			return;

		_dashAttackGraceTimeRemaining = DashAttackGraceSeconds;
	}

	private void SpendStamina(float amount)
	{
		CurrentStamina = Mathf.Max(CurrentStamina - amount, 0.0f);
		EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}

	private void RegenerateStamina(float delta)
	{
		var previous = CurrentStamina;
		CurrentStamina = Mathf.Min(CurrentStamina + StaminaRegenPerSecond * delta, MaxStamina);

		if (!Mathf.IsEqualApprox(previous, CurrentStamina))
			EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
	}

	private void OnDied()
	{
		Velocity = Vector3.Zero;
		_animator?.SetMovementState(false, false, 0.0f);
		SetPhysicsProcess(false);
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
