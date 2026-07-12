using Godot;

public partial class HealthPotion : Area3D
{
    [Export(PropertyHint.Range, "0.01,1.0,0.01")] public float HealPercent { get; set; } = 0.15f;
    [Export] public int FullHealthBloodCoins { get; set; } = 5;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        var health = body.GetNodeOrNull<Health>("Health");
        if (health == null) return;
        if (health.CurrentHealth < health.MaxHealth)
        {
            var amount = health.MaxHealth * HealPercent;
            health.Heal(amount);
            GetTree().CallGroup("hud", "AppendGameLog", $"Pocao restaurou {Mathf.RoundToInt(amount)} de vida.");
        }
        else
        {
            GetTree().CallGroup("run_manager", "AddBloodCoins", FullHealthBloodCoins);
            GetTree().CallGroup("hud", "AppendGameLog", $"Vida cheia: pocao convertida em {FullHealthBloodCoins} moedas.");
        }
        QueueFree();
    }
}
