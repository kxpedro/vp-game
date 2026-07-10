using Godot;

public partial class PlayerSkillController : Node
{
    [Signal]
    public delegate void ActiveSkillUsedEventHandler(int slotIndex, string skillId, float cooldownSeconds);

    [Signal]
    public delegate void ActiveSkillFailedEventHandler(int slotIndex, string message);

    [Export] public NodePath CombatPath { get; set; } = new("../PlayerCombat");
    [Export] public NodePath HealthPath { get; set; } = new("../Health");

    private readonly float[] _slotCooldowns = new float[CharacterSheet.ActiveSkillSlotCount];
    private PlayerCombat _combat;
    private Health _health;

    public override void _Ready()
    {
        AddToGroup("player_skills");
        _combat = GetNodeOrNull<PlayerCombat>(CombatPath);
        _health = GetNodeOrNull<Health>(HealthPath);
    }

    public override void _Process(double delta)
    {
        TickCooldowns((float)delta);

        for (var slotIndex = 0; slotIndex < CharacterSheet.ActiveSkillSlotCount; slotIndex++)
        {
            if (Input.IsActionJustPressed(GetSlotActionName(slotIndex)))
                TryUseActiveSkill(slotIndex);
        }
    }

    public void OnEnemyDefeated()
    {
        if (!CharacterSheet.HasSkill(SkillCatalog.VampireFangsId) || _health == null)
            return;

        var healAmount = _health.MaxHealth * 0.05f;
        _health.Heal(healAmount);
        SpawnLifeStealNumber(healAmount);
    }

    public bool HasSurvivalInstinctSpeedBonus()
    {
        if (!CharacterSheet.HasSkill(SkillCatalog.SurvivalInstinctId) || _health == null || _health.MaxHealth <= 0.0f)
            return false;

        return _health.CurrentHealth / _health.MaxHealth <= 0.1f;
    }

    public float GetCooldownRemaining(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slotCooldowns.Length)
            return 0.0f;

        return _slotCooldowns[slotIndex];
    }

    private void TryUseActiveSkill(int slotIndex)
    {
        var skillId = CharacterSheet.GetActiveSkillSlot(slotIndex);
        if (string.IsNullOrWhiteSpace(skillId))
        {
            EmitSignal(SignalName.ActiveSkillFailed, slotIndex, "Nenhuma habilidade equipada.");
            return;
        }

        if (!CharacterSheet.HasSkill(skillId) || !SkillCatalog.TryGet(skillId, out var definition))
        {
            EmitSignal(SignalName.ActiveSkillFailed, slotIndex, "Habilidade indisponivel.");
            return;
        }

        if (definition.ActivationMode != SkillActivationMode.Active)
        {
            EmitSignal(SignalName.ActiveSkillFailed, slotIndex, "Habilidade passiva nao pode ser usada.");
            return;
        }

        if (_slotCooldowns[slotIndex] > 0.0f)
        {
            EmitSignal(SignalName.ActiveSkillFailed, slotIndex, $"Recarga: {_slotCooldowns[slotIndex]:0.0}s.");
            return;
        }

        if (!ApplyActiveSkill(definition))
        {
            EmitSignal(SignalName.ActiveSkillFailed, slotIndex, "Condicao de uso nao atendida.");
            return;
        }

        var cooldown = CompendiumTuning.GetFloat($"{definition.Id}.cooldown", definition.CooldownSeconds);
        _slotCooldowns[slotIndex] = cooldown;
        EmitSignal(SignalName.ActiveSkillUsed, slotIndex, skillId, cooldown);
    }

    private bool ApplyActiveSkill(SkillDefinition definition)
    {
        if (definition.Id == SkillCatalog.BestialTransformationId)
            return _combat != null && _combat.TryActivateFury();

        return false;
    }

    private void TickCooldowns(float delta)
    {
        for (var i = 0; i < _slotCooldowns.Length; i++)
            _slotCooldowns[i] = Mathf.Max(_slotCooldowns[i] - delta, 0.0f);
    }

    private string GetSlotActionName(int slotIndex)
    {
        return $"skill_slot_{slotIndex + 1}";
    }

    private async void SpawnLifeStealNumber(float amount)
    {
        var label = new Label3D
        {
            Text = $"+{Mathf.RoundToInt(amount)}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 70,
            Modulate = new Color(0.25f, 1.0f, 0.38f),
            OutlineSize = 10,
            OutlineModulate = Colors.Black,
            Position = Vector3.Up * 2.35f
        };

        GetParent()?.AddChild(label);
        var tween = label.CreateTween();
        tween.TweenProperty(label, "position", label.Position + Vector3.Up * 0.9f, 0.5f);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.5f);
        await ToSignal(tween, Tween.SignalName.Finished);
        label.QueueFree();
    }
}
