using Godot;

public partial class HealthPotion : Area3D
{
    [Export] public float HealAmount { get; set; } = 10.0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (!body.IsInGroup("player"))
            return;

        body.GetNodeOrNull<Health>("Health")?.Heal(HealAmount);
        QueueFree();
    }
}
