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
        value = effect.Type == "EnemyTimeScale"
            ? value / Mathf.Max(ArtifactCatalog.BossDropPowerMultiplier, 0.01f)
            : value * ArtifactCatalog.BossDropPowerMultiplier;

        if (effect.Type == "Heal")
        {
            _health?.Heal(value);
            return true;
        }

        if (effect.Type == "DamageBurst")
        {
            SpawnAreaIndicator(DamageBurstRadius, new Color(0.9f, 0.02f, 0.05f, 0.32f));
            ApplyDamageBurst(value);
            return true;
        }

        if (effect.Type == "EnemyTimeScale")
        {
            SpawnAreaIndicator(DamageBurstRadius * 1.5f, new Color(0.15f, 0.4f, 1.0f, 0.28f));
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy) enemy.ApplyTimedSlow(value, 5.0f);
            return true;
        }

        if (effect.Type == "Intangible")
        {
            ApplyTemporaryInvulnerability(value);
            return true;
        }

        if (effect.Type == "FearArea")
        {
            SpawnAreaIndicator(value, new Color(0.65f, 0.12f, 0.8f, 0.3f));
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy && enemy.GlobalPosition.DistanceTo(GetParent<Node3D>().GlobalPosition) <= value) enemy.ApplyFear(3.0f);
            return true;
        }

        if (effect.Type == "SummonSpectralServants")
        {
            ApplyDamageBurst(value * 24.0f);
            return true;
        }

        if (effect.Type == "ExecuteNormalEnemies")
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic { Rarity: EnemyRarity.Normal } enemy) enemy.GetNodeOrNull<Health>("Health")?.ApplyDamage(999999);
            return true;
        }

        if (effect.Type == "BossCurrentHealthDamage")
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies"))
                if (node is EnemyBasic enemy && enemy.Name.ToString().Contains("Boss"))
                {
                    var health = enemy.GetNodeOrNull<Health>("Health");
                    health?.ApplyDamage(health.CurrentHealth * value);
                }
            return true;
        }

        // Companion effects such as Duration and temporary stat bonuses are consumed
        // by the primary active effect and remain data-driven in the catalog.
        if (effect.Type == "GuaranteedCriticalAfterStealth") { RunState.GrantGuaranteedCritical(); return true; }
        if (effect.Type is "MoveSpeed" or "CriticalChance" or "CriticalDamage") { RunState.AddTemporaryBuff(effect.Type, value); return true; }
        if (effect.Type == "DamageTakenDebuff")
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies")) if (node is EnemyBasic enemy) enemy.ApplyDamageTakenDebuff(value, 5);
            return true;
        }
        if (effect.Type == "EliteDamageTakenDebuff")
        {
            foreach (var node in GetTree().GetNodesInGroup("enemies")) if (node is EnemyBasic { Rarity: not EnemyRarity.Normal } enemy) enemy.ApplyDamageTakenDebuff(value, 6);
            return true;
        }
        if (effect.Type == "Duration") return true;

        return false;
    }

    private async void SpawnAreaIndicator(float radius, Color color)
    {
        var player = GetParent<Node3D>(); if (player == null) return;
        var indicator = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.045f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color, EmissionEnabled = true, Emission = new Color(color.R, color.G, color.B),
                EmissionEnergyMultiplier = 1.2f, Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            },
            Position = Vector3.Up * 0.04f,
            Scale = Vector3.One * 0.15f
        };
        player.AddChild(indicator);
        var tween = indicator.CreateTween();
        tween.TweenProperty(indicator, "scale", Vector3.One, 0.14f);
        tween.TweenInterval(0.2f);
        tween.TweenProperty(indicator, "transparency", 1.0f, 0.18f);
        await ToSignal(tween, Tween.SignalName.Finished);
        indicator.QueueFree();
    }

    public void NotifyCriticalHit()
    {
        var reduction = RunState.GetEffectTotal("ActiveCooldownReductionOnCritical.Flat");
        if (reduction <= 0) return;
        for (var i = 0; i < _slotCooldowns.Length; i++) _slotCooldowns[i] = Mathf.Max(_slotCooldowns[i] - reduction, 0);
    }

    private async void ApplyTemporaryInvulnerability(float seconds)
    {
        _health?.SetInvincible(true);
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        _health?.SetInvincible(false);
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
