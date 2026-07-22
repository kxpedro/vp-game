using Godot;

public partial class PlayerAnimator : Node
{
    [Export] public NodePath ModelRootPath { get; set; } = new("../CharacterModel");
    [Export] public string IdleAnimation { get; set; } = "Rig|Idle";
    [Export] public string WalkAnimation { get; set; } = "Rig|Walk";
    [Export] public string RunAnimation { get; set; } = "Rig|Sprint";
    [Export] public string DodgeAnimation { get; set; } = "Rig|Roll";
    [Export] public string LightAttackAnimation { get; set; } = "Rig|Sword_Attack";
    [Export] public string HeavyAttackAnimation { get; set; } = "Rig|Sword_Attack_RM";
    [Export] public float OneShotLockSeconds { get; set; } = 0.52f;
    [Export] public float IdleAnimationSpeed { get; set; } = 1.0f;
    [Export] public float WalkAnimationSpeed { get; set; } = 1.0f;
    [Export] public float RunAnimationSpeed { get; set; } = 1.0f;
    [Export] public float DodgeAnimationSpeed { get; set; } = 1.0f;
    [Export] public float LightAttackAnimationSpeed { get; set; } = 1.65f;
    [Export] public float HeavyAttackAnimationSpeed { get; set; } = 1.48f;
    [Export] public float DashAttackAnimationSpeed { get; set; } = 1.9f;
    [Export] public bool StripRootMotion { get; set; } = true;
    [Export] public float LoopBlendSeconds { get; set; } = 0.03f;
    [Export] public float OneShotBlendSeconds { get; set; } = 0.02f;
    [Export] public float WalkReferenceSpeed { get; set; } = 2.0f;
    [Export] public float RunReferenceSpeed { get; set; } = 4.0f;
    [Export] public float AnimationSpeedMin { get; set; } = 0.65f;
    [Export] public float AnimationSpeedMax { get; set; } = 2.25f;

    private AnimationPlayer _animationPlayer;
    private CharacterBody3D _player;
    private PlayerCombat _combat;
    private string _currentAnimation = "";
    private string _idleAnimation = "";
    private string _walkAnimation = "";
    private string _runAnimation = "";
    private string _dodgeAnimation = "";
    private string _lightAttackAnimation = "";
    private string _heavyAttackAnimation = "";
    private float _oneShotLockTimer;
    private bool _isMoving;
    private bool _isRunning;
    private float _movementSpeed;
    private float _currentLoopPlaybackSpeed = 1.0f;
    private string _oneShotAnimation = "";

    public override void _Ready()
    {
        _player = GetParent<CharacterBody3D>();
        _combat = _player.GetNodeOrNull<PlayerCombat>("PlayerCombat");
        var modelRoot = GetNodeOrNull<Node>(ModelRootPath);
        _animationPlayer = FindAnimationPlayer(modelRoot ?? _player);

        if (_animationPlayer == null)
            return;

        GameTuning.Changed += ApplyRuntimeTuning;
        CacheAnimationNames();
        if (StripRootMotion)
            StripRootMotionTracks();

        ApplyRuntimeTuning();
        SetLoopMode(_idleAnimation, true);
        SetLoopMode(_walkAnimation, true);
        SetLoopMode(_runAnimation, true);
        PlayLoop(_idleAnimation, IdleAnimationSpeed);
    }

    public override void _ExitTree()
    {
        GameTuning.Changed -= ApplyRuntimeTuning;
    }

    public override void _Process(double delta)
    {
        if (_animationPlayer == null || _player == null)
            return;

        if (_oneShotLockTimer > 0.0f)
        {
            _oneShotLockTimer -= (float)delta;
            return;
        }

        if (!_isMoving)
        {
            PlayLoop(_idleAnimation, IdleAnimationSpeed);
            return;
        }

        if (_isRunning)
            PlayLoop(_runAnimation, ComputeMovementPlaybackSpeed(true));
        else
            PlayLoop(_walkAnimation, ComputeMovementPlaybackSpeed(false));
    }

    public void SetMovementState(bool isMoving, bool isRunning, float movementSpeed)
    {
        _isMoving = isMoving;
        _isRunning = isRunning;
        _movementSpeed = Mathf.Max(movementSpeed, 0.0f);

        if (_animationPlayer == null || _oneShotLockTimer > 0.0f)
            return;

        if (!_isMoving)
            PlayLoop(_idleAnimation, IdleAnimationSpeed);
        else if (_isRunning)
            PlayLoop(_runAnimation, ComputeMovementPlaybackSpeed(true));
        else
            PlayLoop(_walkAnimation, ComputeMovementPlaybackSpeed(false));
    }

    public void PlayDodge()
    {
        var dashSpeed = (_player as PlayerController)?.DodgeAnimationSpeedMultiplier ?? 1.0f;
        PlayOneShot(_dodgeAnimation, GetAnimationDuration(_dodgeAnimation, DodgeAnimationSpeed * dashSpeed, true), DodgeAnimationSpeed * dashSpeed);
    }

    public void PlayLightAttack(int comboIndex)
    {
        if (comboIndex % 2 == 0)
            PlayOneShot(_lightAttackAnimation, GetLightAttackDuration(), LightAttackAnimationSpeed * CurrentAttackSpeed());
        else
            PlayOneShot(_heavyAttackAnimation, GetHeavyAttackDuration(), HeavyAttackAnimationSpeed * CurrentAttackSpeed());
    }

    public void PlayHeavyAttack()
    {
        PlayOneShot(_heavyAttackAnimation, GetSpecialAttackDuration(), HeavyAttackAnimationSpeed);
    }

    public void PlayDashAttack()
    {
        PlayOneShot(_heavyAttackAnimation, GetDashAttackDuration(), DashAttackAnimationSpeed);
    }

    public void CancelOneShot()
    {
        _oneShotLockTimer = 0.0f;
        _oneShotAnimation = "";
        if (_animationPlayer == null)
            return;

        if (!_isMoving)
            PlayLoop(_idleAnimation, IdleAnimationSpeed);
        else if (_isRunning)
            PlayLoop(_runAnimation, ComputeMovementPlaybackSpeed(true));
        else
            PlayLoop(_walkAnimation, ComputeMovementPlaybackSpeed(false));
    }

    public float GetLightAttackDuration()
    {
        return GetAnimationDuration(_lightAttackAnimation, LightAttackAnimationSpeed * CurrentAttackSpeed(), false);
    }

    public float GetHeavyAttackDuration()
    {
        return GetAnimationDuration(_heavyAttackAnimation, HeavyAttackAnimationSpeed * CurrentAttackSpeed(), false);
    }

    public float GetDashAttackDuration()
    {
        return GetAnimationDuration(_heavyAttackAnimation, DashAttackAnimationSpeed, false);
    }

    public float GetSpecialAttackDuration()
    {
        return GetAnimationDuration(_heavyAttackAnimation, HeavyAttackAnimationSpeed, false);
    }

    private float CurrentAttackSpeed() => _combat?.GetCurrentAttackSpeedMultiplier() ?? GameTuning.GetAttackSpeedMultiplier();

    public float GetAnimationLength(string animationName)
    {
        if (!HasAnimation(animationName))
            return 0.0f;

        return (float)_animationPlayer.GetAnimation(animationName).Length;
    }

    private void CacheAnimationNames()
    {
        _idleAnimation = ResolveAnimationName(IdleAnimation);
        _walkAnimation = ResolveAnimationName(WalkAnimation);
        _runAnimation = ResolveAnimationName(RunAnimation);
        _dodgeAnimation = ResolveAnimationName(DodgeAnimation);
        _lightAttackAnimation = ResolveAnimationName(LightAttackAnimation);
        _heavyAttackAnimation = ResolveAnimationName(HeavyAttackAnimation);
    }

    private void PlayLoop(string animationName, float speedScale)
    {
        if (_currentAnimation == animationName && Mathf.IsEqualApprox(_currentLoopPlaybackSpeed, speedScale))
            return;

        if (!HasAnimation(animationName))
            return;

        _currentAnimation = animationName;
        _currentLoopPlaybackSpeed = speedScale;
        _oneShotAnimation = "";
        _animationPlayer.Play(animationName, LoopBlendSeconds, speedScale);
    }

    private void PlayOneShot(string animationName, float lockSeconds, float speedScale)
    {
        if (!HasAnimation(animationName))
            return;

        _currentAnimation = animationName;
        _currentLoopPlaybackSpeed = speedScale;
        _oneShotAnimation = animationName;
        _oneShotLockTimer = lockSeconds;
        _animationPlayer.Play(animationName, OneShotBlendSeconds, speedScale);
    }

    private float GetAnimationDuration(string animationName, float speedScale, bool clampToFullLength)
    {
        var length = GetAnimationLength(animationName);
        if (length <= 0.0f)
            return OneShotLockSeconds;

        var duration = length / Mathf.Max(speedScale * GameTuning.GetAnimationSpeedMultiplier(), 0.01f);
        return clampToFullLength ? duration : Mathf.Max(duration, 0.01f);
    }

    private float ComputeMovementPlaybackSpeed(bool isRunning)
    {
        var referenceSpeed = isRunning ? RunReferenceSpeed : WalkReferenceSpeed;
        var baseSpeed = isRunning ? RunAnimationSpeed : WalkAnimationSpeed;
        var speedFactor = referenceSpeed > 0.0f ? _movementSpeed / referenceSpeed : 1.0f;
        var tunedSpeed = baseSpeed * speedFactor * GameTuning.GetAnimationSpeedMultiplier();
        return Mathf.Clamp(tunedSpeed, AnimationSpeedMin, AnimationSpeedMax);
    }

    private string ResolveAnimationName(string requestedName)
    {
        if (_animationPlayer.HasAnimation(requestedName))
            return requestedName;

        if (requestedName.EndsWith("_Loop"))
        {
            var nonLoopName = requestedName[..^"_Loop".Length];
            if (_animationPlayer.HasAnimation(nonLoopName))
                return nonLoopName;
        }

        var requestedSuffix = requestedName.Contains("/")
            ? requestedName[(requestedName.LastIndexOf('/') + 1)..]
            : requestedName;

        if (requestedSuffix.EndsWith("_Loop"))
            requestedSuffix = requestedSuffix[..^"_Loop".Length];

        foreach (var animationName in _animationPlayer.GetAnimationList())
        {
            var candidate = animationName.ToString();
            if (candidate.EndsWith(requestedSuffix))
                return candidate;
        }

        GD.PushWarning($"PlayerAnimator: animation '{requestedName}' not found on '{_animationPlayer.GetPath()}'.");
        return requestedName;
    }

    private void SetLoopMode(string animationName, bool shouldLoop)
    {
        if (!HasAnimation(animationName))
            return;

        var animation = _animationPlayer.GetAnimation(animationName);
        animation.LoopMode = shouldLoop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None;
    }

    private void ApplyRuntimeTuning()
    {
        if (_animationPlayer == null)
            return;

        _animationPlayer.SpeedScale = GameTuning.GetAnimationSpeedMultiplier();
    }

    private void StripRootMotionTracks()
    {
        StripRootMotionTracks(_walkAnimation);
        StripRootMotionTracks(_runAnimation);
        StripRootMotionTracks(_dodgeAnimation);
        StripRootMotionTracks(_lightAttackAnimation);
        StripRootMotionTracks(_heavyAttackAnimation);
    }

    private void StripRootMotionTracks(string animationName)
    {
        if (!HasAnimation(animationName))
            return;

        var animation = _animationPlayer.GetAnimation(animationName);
        for (var i = animation.GetTrackCount() - 1; i >= 0; i--)
        {
            if (animation.TrackGetType(i) != Animation.TrackType.Position3D)
                continue;

            var path = animation.TrackGetPath(i).ToString();
            if (path.EndsWith(":root"))
                animation.RemoveTrack(i);
        }
    }

    private bool HasAnimation(string animationName)
    {
        return _animationPlayer != null
            && !string.IsNullOrEmpty(animationName)
            && _animationPlayer.HasAnimation(animationName);
    }

    private AnimationPlayer FindAnimationPlayer(Node node)
    {
        if (node == null)
            return null;

        if (node is AnimationPlayer animationPlayer)
            return animationPlayer;

        foreach (var child in node.GetChildren())
        {
            var found = FindAnimationPlayer(child);
            if (found != null)
                return found;
        }

        return null;
    }
}
