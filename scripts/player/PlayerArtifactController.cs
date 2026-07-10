using Godot;

public partial class PlayerArtifactController : Node
{
    [Export] public NodePath HealthPath { get; set; } = new("../Health");
    [Export] public float DefaultCooldownSeconds { get; set; } = 5.0f;
    [Export] public float DamageBurstRadius { get; set; } = 4.0f;

    private readonly float[] _slotCooldowns = new float[2];
    private Health _health;

    public override void _Ready()
    {
        AddToGroup("player_artifacts");
        _health = GetNodeOrNull<Health>(HealthPath);
    }

    public override void _Process(double delta)
    {
        TickCooldowns((float)delta);

        if (Input.IsActionJustPressed("artifact_slot_q"))
            TryUseArtifact(0, "Q");

        if (Input.IsActionJustPressed("artifact_slot_r"))
            TryUseArtifact(1, "R");
    }

    private void TryUseArtifact(int slotIndex, string keyName)
    {
        var artifact = RunState.GetActiveArtifactSlot(slotIndex);
        if (string.IsNullOrWhiteSpace(artifact.Name))
        {
            Notify($"{keyName}: nenhum artefato ativo equipado.");
            return;
        }

        if (_slotCooldowns[slotIndex] > 0.0f)
        {
            Notify($"{keyName}: recarga {_slotCooldowns[slotIndex]:0.0}s.");
            return;
        }

        if (!ArtifactCatalog.TryGet(artifact.Id, out var definition))
        {
            Notify($"{keyName}: artefato desconhecido.");
            return;
        }

        var used = false;
        for (var i = 0; i < definition.Effects.Length; i++)
            used |= ApplyEffect(definition.Id, i, definition.Effects[i]);

        if (!used)
        {
            Notify($"{artifact.Name} nao possui efeito ativo implementado.");
            return;
        }

        _slotCooldowns[slotIndex] = DefaultCooldownSeconds;
        Notify($"{keyName}: {artifact.Name} ativado.");
    }

    private bool ApplyEffect(string artifactId, int effectIndex, ArtifactEffectDefinition effect)
    {
        var value = CompendiumTuning.GetFloat($"{artifactId}.effect.{effectIndex}.value", effect.Value);

        if (effect.Type == "Heal")
        {
            _health?.Heal(value);
            return true;
        }

        if (effect.Type == "DamageBurst")
        {
            ApplyDamageBurst(value);
            return true;
        }

        return false;
    }

    private void ApplyDamageBurst(float damage)
    {
        var player = GetParent<Node3D>();
        if (player == null)
            return;

        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (node is not Node3D enemyNode)
                continue;

            if (enemyNode.GlobalPosition.DistanceTo(player.GlobalPosition) > DamageBurstRadius)
                continue;

            enemyNode.GetNodeOrNull<Health>("Health")?.ApplyDamage(damage);
        }
    }

    private void TickCooldowns(float delta)
    {
        for (var i = 0; i < _slotCooldowns.Length; i++)
            _slotCooldowns[i] = Mathf.Max(_slotCooldowns[i] - delta, 0.0f);
    }

    private void Notify(string message)
    {
        GetTree().CallGroup("hud", "NotifySkillMessage", message);
    }
}
