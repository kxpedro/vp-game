using Godot;

public partial class BossController : Node
{
    public BossDefinition Definition { get; private set; }
    public int CurrentPhase { get; private set; } = 1;
    private EnemyBasic _enemy;
    private Health _health;
    private float _moveTimer;
    private int _moveIndex;

    public void Configure(EnemyBasic enemy, BossDefinition definition)
    {
        _enemy = enemy; Definition = definition; _health = enemy.GetNode<Health>("Health");
        AddToGroup("boss_controller");
        _health.SetMaxHealth(Mathf.Max(definition.EstimatedMaxHealth, 1000), true);
        _health.HealthChanged += OnHealthChanged;
        ApplyNextMove();
    }

    public override void _Process(double delta)
    {
        if (_enemy == null || _health == null || _health.IsDead) return;
        _moveTimer -= (float)delta;
        if (_moveTimer <= 0) ApplyNextMove();
    }

    private void ApplyNextMove()
    {
        if (Definition?.Moves == null || Definition.Moves.Length == 0) return;
        var available = Mathf.Clamp(CurrentPhase, 1, Definition.Moves.Length);
        var move = Definition.Moves[_moveIndex % available]; _moveIndex++;
        _moveTimer = move.Cooldown;
        _enemy.ApplyBossMoveTuning(move, CurrentPhase);
        _enemy.PlayBossMoveAnimation(move.Id, CurrentPhase);
        GetTree().CallGroup("hud", "AppendGameLog", $"{Definition.DisplayName} prepara {move.DisplayName}.");
    }

    private void OnHealthChanged(float current, float maximum)
    {
        var ratio = maximum > 0 ? current / maximum : 0;
        var phase = ratio <= 0.33f ? 3 : ratio <= 0.66f ? 2 : 1;
        if (phase == CurrentPhase) return;
        CurrentPhase = phase;
        _enemy.MoveSpeed *= 1.18f;
        GetTree().CallGroup("hud", "AppendGameLog", $"{Definition.DisplayName} entrou na fase {phase}!");
    }
}
