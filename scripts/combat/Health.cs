using Godot;

public partial class Health : Node
{
    [Signal]
    public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

    [Signal]
    public delegate void DamagedEventHandler(float amount);

    [Signal]
    public delegate void DiedEventHandler();

    [Export] public float MaxHealth { get; set; } = 100.0f;

    public float CurrentHealth { get; private set; }
    public bool IsInvincible { get; private set; }
    public bool IsDead => CurrentHealth <= 0.0f;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void SetMaxHealth(float maxHealth, bool fillHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = fillHealth ? MaxHealth : Mathf.Min(CurrentHealth, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void SetCurrentHealth(float currentHealth)
    {
        CurrentHealth = Mathf.Clamp(currentHealth, 0.0f, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (IsDead)
            EmitSignal(SignalName.Died);
    }

    public void SetMaxHealthPreservingRatio(float maxHealth)
    {
        var ratio = MaxHealth > 0.0f ? CurrentHealth / MaxHealth : 1.0f;
        MaxHealth = maxHealth;
        CurrentHealth = Mathf.Clamp(MaxHealth * ratio, 0.0f, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void ApplyDamage(float amount)
    {
        if (IsDead || IsInvincible || amount <= 0.0f)
            return;

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0.0f);
        EmitSignal(SignalName.Damaged, amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (IsDead)
            EmitSignal(SignalName.Died);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0.0f)
            return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void SetInvincible(bool isInvincible)
    {
        IsInvincible = isInvincible;
    }
}
