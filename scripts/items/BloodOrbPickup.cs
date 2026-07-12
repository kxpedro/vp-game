using Godot;

public partial class BloodOrbPickup : Area3D
{
    public float HealMaxHealthRatio { get; set; }

    public override void _Ready()
    {
        BodyEntered += body =>
        {
            if (!body.IsInGroup("player")) return;
            var health = body.GetNodeOrNull<Health>("Health");
            health?.Heal(health.MaxHealth * HealMaxHealthRatio);
            QueueFree();
        };
    }
}
