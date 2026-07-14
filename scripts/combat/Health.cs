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
    public int ShieldHits { get; set; }
    public bool LastDamageWasCritical { get; private set; }
    public string LastDamageSource { get; private set; } = "Perigos do castelo";
    public float LastDamageAmount { get; private set; }

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

    public void ApplyDamage(float amount, bool isCritical = false, string sourceName = "Perigos do castelo")
    {
        if (IsDead || IsInvincible || amount <= 0.0f)
            return;

        if (ShieldHits > 0)
        {
            ShieldHits--;
            EmitSignal(SignalName.Damaged, 0.0f);
            return;
        }

        LastDamageWasCritical = isCritical;
        LastDamageSource = string.IsNullOrWhiteSpace(sourceName) ? "Perigos do castelo" : sourceName;

        var blessingRuntime = GetParent()?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime");
        amount = blessingRuntime?.AbsorbBarrierAndReduceDamage(amount) ?? amount;
        if (amount <= 0.0f) { EmitSignal(SignalName.Damaged, 0.0f); return; }
        if (blessingRuntime?.ProtectMinimumHealth(amount) == true)
            amount = Mathf.Max(CurrentHealth - 1.0f, 0.0f);
        LastDamageAmount = amount;

        if (amount >= CurrentHealth && blessingRuntime?.TrySacrificeCharmedOnFatal(out var restoredHealth) == true)
        {
            CurrentHealth = Mathf.Clamp(restoredHealth, 1.0f, MaxHealth);
            EmitSignal(SignalName.Damaged, amount);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            return;
        }

        if (amount >= CurrentHealth && GetParent()?.IsInGroup("player") == true && RunState.TryConsumeFatalProtection())
        {
            CurrentHealth = 1.0f;
            EmitSignal(SignalName.Damaged, amount);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            ApplyFatalInvulnerability();
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - amount, 0.0f);
        blessingRuntime?.OnPlayerDamaged(amount);
        if (GetParent()?.IsInGroup("player") == true) RunState.NotifyPlayerDamaged();
        EmitSignal(SignalName.Damaged, amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (IsDead)
            EmitSignal(SignalName.Died);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0.0f)
            return;

        var previous = CurrentHealth;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        var actual = CurrentHealth - previous;
        var overheal = Mathf.Max(amount - actual, 0.0f);
        if (GetParent()?.IsInGroup("player") == true) RunState.NotifyPlayerHealed();
        GetParent()?.GetNodeOrNull<BloodBlessingRuntime>("BloodBlessingRuntime")?.OnHealed(amount, actual, overheal);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    public void SetInvincible(bool isInvincible)
    {
        IsInvincible = isInvincible;
    }

    private async void ApplyFatalInvulnerability()
    {
        IsInvincible = true;
        var seconds = Mathf.Max(RunState.GetEffectTotal("InvulnerabilityOnFatal.Flat"), 0.5f);
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        IsInvincible = false;
    }
}
