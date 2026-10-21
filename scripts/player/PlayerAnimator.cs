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
    [Export] public string ThrustAttackAnimation { get; set; } = "Punch";
    [Export] public string HitReactionAnimation { get; set; } = "RecieveHit";
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
    private string _thrustAttackAnimation = "";
    private float _oneShotLockTimer;
    private bool _isMoving;
    private bool _isRunning;
    private float _movementSpeed;
    private float _currentLoopPlaybackSpeed = 1.0f;
    private string _oneShotAnimation = "";
    private string _hitAnimation = "";

    // Perfil de golpe: StrikeFraction = ponto da animacao que coincide com o dano;
    // WindupTimeShare = parte do hitDelay gasta na preparacao lenta; WindupAnimShare = parte da animacao
    // pre-golpe coberta nela; HoldSeconds = pausa curta no impacto (tirada da recuperacao).
    private readonly record struct AttackAnimProfile(float StrikeFraction, float WindupTimeShare, float WindupAnimShare, float HoldSeconds);
    private readonly record struct AnimSegment(float Duration, float AnimEnd);

    private AnimSegment[] _segments = System.Array.Empty<AnimSegment>();
    private int _segmentIndex;
    private float _segmentTimer;
    private float _segmentAnimStart;
    private bool _segmentsActive;

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

        if (_segmentsActive)
            TickSegments((float)delta);

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

    public void PlayLightAttack(int comboIndex, float combatDuration, WeaponAttackShape attackShape)
    {
        var animation = attackShape == WeaponAttackShape.Line
            ? _thrustAttackAnimation
            : comboIndex % 2 == 0 ? _lightAttackAnimation : _heavyAttackAnimation;
        PlaySyncedOneShot(animation, combatDuration);
    }

    // Versao com anticipation: o quadro de golpe da animacao cai em hitDelay (instante do dano).
    public void PlayLightAttack(int comboIndex, float hitDelay, float recovery, WeaponAttackShape attackShape, bool isFinisher)
    {
        var animation = attackShape == WeaponAttackShape.Line
            ? _thrustAttackAnimation
            : comboIndex % 2 == 0 ? _lightAttackAnimation : _heavyAttackAnimation;
        var profile = attackShape == WeaponAttackShape.Line
            ? new AttackAnimProfile(0.40f, 0.60f, 0.25f, 0.035f)
            : isFinisher
                ? new AttackAnimProfile(0.50f, 0.65f, 0.25f, 0.050f)
                : new AttackAnimProfile(0.45f, 0.55f, 0.30f, 0.030f);
        PlaySegmentedOneShot(animation, hitDelay, recovery, profile);
    }

    public void PlayHeavyAttack(float combatDuration)
    {
        PlaySyncedOneShot(_heavyAttackAnimation, combatDuration);
    }

    // Especial: preparacao longa e hold marcante no impacto.
    public void PlayHeavyAttack(float hitDelay, float recovery)
    {
        PlaySegmentedOneShot(_heavyAttackAnimation, hitDelay, recovery, new AttackAnimProfile(0.50f, 0.70f, 0.20f, 0.070f));
    }

    public void PlayDashAttack(float combatDuration)
    {
        PlaySyncedOneShot(_heavyAttackAnimation, combatDuration);
    }

    // Rapido da esquiva: sem preparacao, estalo direto.
    public void PlayDashAttack(float hitDelay, float recovery)
    {
        PlaySegmentedOneShot(_heavyAttackAnimation, hitDelay, recovery, new AttackAnimProfile(0.42f, 0.30f, 0.45f, 0.025f));
    }

    public void PlayDodge(float duration)
    {
        if (!HasAnimation(_dodgeAnimation))
            return;

        var length = Mathf.Max(GetAnimationLength(_dodgeAnimation), 0.01f);
        var globalSpeed = Mathf.Max(GameTuning.GetAnimationSpeedMultiplier(), 0.01f);
        PlayOneShot(_dodgeAnimation, Mathf.Max(duration, 0.05f), length / (Mathf.Max(duration, 0.05f) * globalSpeed));
    }

    public void PlayHitReaction(float duration)
    {
        if (!HasAnimation(_hitAnimation))
            return;

        var length = Mathf.Max(GetAnimationLength(_hitAnimation), 0.01f);
        var globalSpeed = Mathf.Max(GameTuning.GetAnimationSpeedMultiplier(), 0.01f);
        var lockSeconds = Mathf.Max(duration, 0.05f);
        // Nunca mais lenta que o normal: se a animacao for curta, toca inteira e segura a ultima pose.
        var speed = Mathf.Max(length / (lockSeconds * globalSpeed), 1.0f / globalSpeed);
        PlayOneShot(_hitAnimation, lockSeconds, speed);
    }

    public void CancelOneShot()
    {
        EndSegments();
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

    private float CurrentAttackSpeed() => _combat?.GetCurrentAttackSpeedMultiplier() ?? 1.0f;

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
        _thrustAttackAnimation = ResolveAnimationName(ThrustAttackAnimation);
        _hitAnimation = _animationPlayer.HasAnimation(HitReactionAnimation)
            ? HitReactionAnimation
            : ResolveAnimationName(HitReactionAnimation);
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

    private void PlaySegmentedOneShot(string animationName, float hitDelay, float recovery, AttackAnimProfile profile)
    {
        if (!HasAnimation(animationName))
            return;

        hitDelay = Mathf.Max(hitDelay, 0.01f);
        recovery = Mathf.Max(recovery, 0.01f);
        var length = Mathf.Max(GetAnimationLength(animationName), 0.01f);
        var strike = length * profile.StrikeFraction;
        var windupDuration = hitDelay * profile.WindupTimeShare;
        var hold = Mathf.Min(profile.HoldSeconds, recovery * 0.3f);

        PlayOneShot(animationName, hitDelay + recovery, 1.0f);
        _segments = new[]
        {
            new AnimSegment(windupDuration, strike * profile.WindupAnimShare),
            new AnimSegment(hitDelay - windupDuration, strike),
            new AnimSegment(hold, strike + length * 0.01f),
            new AnimSegment(recovery - hold, length)
        };
        _segmentIndex = 0;
        _segmentTimer = 0.0f;
        _segmentAnimStart = 0.0f;
        _segmentsActive = true;
        ApplySegmentSpeed();
    }

    private void ApplySegmentSpeed()
    {
        var segment = _segments[_segmentIndex];
        var duration = Mathf.Max(segment.Duration, 0.001f);
        _animationPlayer.SpeedScale = Mathf.Max((segment.AnimEnd - _segmentAnimStart) / duration, 0.01f);
    }

    private void TickSegments(float delta)
    {
        _segmentTimer += delta;
        while (_segmentsActive && _segmentTimer >= _segments[_segmentIndex].Duration)
        {
            _segmentTimer -= _segments[_segmentIndex].Duration;
            _segmentAnimStart = _segments[_segmentIndex].AnimEnd;
            _segmentIndex++;
            if (_segmentIndex >= _segments.Length)
            {
                EndSegments();
                return;
            }
        }

        if (_segmentsActive)
            ApplySegmentSpeed();
    }

    private void EndSegments()
    {
        if (!_segmentsActive)
            return;

        _segmentsActive = false;
        ApplyRuntimeTuning();
    }

    private void PlayOneShot(string animationName, float lockSeconds, float speedScale)
    {
        if (!HasAnimation(animationName))
            return;

        EndSegments();
        _currentAnimation = animationName;
        _currentLoopPlaybackSpeed = speedScale;
        _oneShotAnimation = animationName;
        _oneShotLockTimer = lockSeconds;
        // Play() com o mesmo clipe ainda tocando não volta ao início: força o reinício do golpe.
        if (_animationPlayer.CurrentAnimation == animationName)
            _animationPlayer.Stop();
        _animationPlayer.Play(animationName, OneShotBlendSeconds, speedScale);
    }

    private void PlaySyncedOneShot(string animationName, float combatDuration)
    {
        if (!HasAnimation(animationName))
            return;

        var duration = Mathf.Max(combatDuration, 0.01f);
        var animationLength = Mathf.Max(GetAnimationLength(animationName), 0.01f);
        var globalSpeed = Mathf.Max(GameTuning.GetAnimationSpeedMultiplier(), 0.01f);
        var synchronizedSpeed = animationLength / (duration * globalSpeed);
        PlayOneShot(animationName, duration, synchronizedSpeed);
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
            if (IsRootMotionTrack(path))
                animation.RemoveTrack(i);
        }
    }

    private static bool IsRootMotionTrack(string path)
    {
        var separator = path.LastIndexOf(':');
        var trackName = separator >= 0 ? path[(separator + 1)..] : path;
        return trackName.Equals("root", System.StringComparison.OrdinalIgnoreCase)
            || trackName.Equals("rootnode", System.StringComparison.OrdinalIgnoreCase)
            || trackName.Equals("bone", System.StringComparison.OrdinalIgnoreCase);
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
