using Godot;

public partial class TrainingDummy : EnemyBasic
{
    private Health _trainingHealth;
    private bool _resetQueued;

    public override void _Ready()
    {
        MoveSpeed = 0.0f;
        PatrolSpeed = 0.0f;
        DetectionRange = 0.0f;
        AttackRange = 0.0f;
        AttackDamage = 0.0f;
        ContactDamage = 0.0f;
        SpawnAttackDelay = 999999.0f;
        base._Ready();
        Name = "TrainingDummy";
        _trainingHealth = GetNode<Health>("Health");
        _trainingHealth.HealthChanged += OnTrainingHealthChanged;
        SetOverheadHealthVisible(true);
    }

    public override void _ExitTree()
    {
        if (_trainingHealth != null)
            _trainingHealth.HealthChanged -= OnTrainingHealthChanged;
        base._ExitTree();
    }

    private void OnTrainingHealthChanged(float current, float maximum)
    {
        if (_resetQueued || current >= maximum) return;
        _resetQueued = true;
        CallDeferred(nameof(ResetTrainingHealth));
    }

    private void ResetTrainingHealth()
    {
        _resetQueued = false;
        if (_trainingHealth != null && !_trainingHealth.IsDead)
            _trainingHealth.SetCurrentHealth(_trainingHealth.MaxHealth);
    }
}
