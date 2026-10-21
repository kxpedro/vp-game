// Parametros do jogador, combate, armas e camera (T4). Os padroes referenciam as mesmas constantes publicas do codigo.
// Nao registrados de proposito: PlayerController.DodgeCost (sem uso), AttackControlProfile por golpe (GetTunedAttack
// nao repassa Control), CharacterSheet/Barreira (arquivos de outras tarefas).
public static partial class DevParameterRegistry
{
    private const string GPlayer = "Jogador";
    private const string GDodge = "Esquiva";
    private const string GCamera = "Câmera";

    static partial void RegisterPlayer()
    {
        RegisterPlayerMovement();
        RegisterPlayerCombat();
        RegisterPlayerCamera();
    }

    private static void RegisterPlayerMovement()
    {
        const string reapply = "Passa por PlayerController.ApplyRuntimeTuning (reaplicado automaticamente ao editar). Multiplicado pelos ajustes rápidos e bônus.";
        P("player.walk_speed", "Velocidade de caminhada (base)", GPlayer, PlayerController.DefaultWalkSpeed, 0.5, 20, 0.1, false, DevApplyMode.Reapply, reapply);
        P("player.sprint_speed", "Velocidade de corrida (base)", GPlayer, PlayerController.DefaultSprintSpeed, 0.5, 30, 0.1, false, DevApplyMode.Reapply, reapply);
        P("player.max_health", "Vida máxima base", GPlayer, PlayerController.DefaultMaxHealth, 1, 5000, 5, false, DevApplyMode.Reapply, "Base antes de atributos, bênçãos e Frutas (padrão = Health.MaxHealth de Player.tscn).");
        P("player.stamina.max", "Stamina máxima (base)", GPlayer, PlayerController.DefaultMaxStamina, 1, 1000, 5, false, DevApplyMode.Reapply, reapply);
        P("player.stamina.regen", "Regeneração de stamina por segundo", GPlayer, PlayerController.DefaultStaminaRegenPerSecond, 0, 500, 1, false);
        P("player.hit_stagger", "Atordoamento ao ser atingido (s)", GPlayer, PlayerController.DefaultDamageStaggerDuration, 0, 3, 0.01, false);

        P("player.dodge.speed", "Esquiva: velocidade (base)", GDodge, PlayerController.DefaultDodgeSpeed, 1, 60, 0.5, false, DevApplyMode.Reapply, reapply);
        P("player.dodge.duration", "Esquiva: duração (s, base)", GDodge, PlayerController.DefaultDodgeDuration, 0.02, 2, 0.01, false, DevApplyMode.Reapply, "Multiplicada pelo ajuste rápido de distância do dash.");
        P("player.dodge.grace", "Esquiva: janela do ataque de dash (s)", GDodge, PlayerController.DefaultDashAttackGraceSeconds, 0, 3, 0.05, false);
        P("player.dodge.cooldown", "Esquiva: recarga extra (s)", GDodge, PlayerController.DefaultDodgeCooldownSeconds, 0, 3, 0.01, false);
        P("player.dodge.buffer", "Esquiva: buffer de input (s)", GDodge, PlayerController.DefaultDodgeInputBufferSeconds, 0, 2, 0.01, false);
    }

    private static void RegisterPlayerCombat()
    {
        P("combat.combo.reset_time", "Combo básico: tempo para zerar (s)", GCombat, 1.0, 0.1, 10, 0.05, false);
        P("combat.quick.buffer", "Golpe rápido: buffer de input (s)", GCombat, 0.32, 0, 3, 0.01, false);
        P("combat.aim.radius", "Mira assistida: raio", GCombat, 4.25, 0, 20, 0.05, false, DevApplyMode.Live, "Quando editado, substitui o raio do perfil de controle do golpe.");
        P("combat.crit.base_multiplier", "Crítico: multiplicador base", GCombat, 1.5, 1, 20, 0.05, false);
        P("combat.knockback.tank_mult", "Knockback básico vs Tank (x)", GCombat, 0.5, 0, 5, 0.05, false);
        P("combat.knockback.finisher_mult", "Knockback do finalizador do combo (x)", GCombat, 1.2, 0, 10, 0.05, false);
        P("attack.fury.end_burst_ratio", "Fúria: explosão final (fração do dano básico)", GCombat, 0.15, 0, 10, 0.01, false);
        P("attack.fury.end_burst_radius", "Fúria: raio da explosão final", GCombat, 3.5, 0, 20, 0.1, false);
        P("attack.fury.end_burst_knockback", "Fúria: knockback da explosão final", GCombat, 2.5, 0, 50, 0.1, false);
    }

    // Nomes fixos (pt-BR) dos grupos: o registro nasce antes da troca de idioma e o grupo precisa ser estavel.
    private static string WeaponLabel(string id) => id switch
    {
        "longsword" => "Espada longa",
        "dual_revolvers" => "Revólver duplo",
        "whip" => "Chicote",
        _ => id
    };

    // Um grupo por arma do WeaponCatalog ("Armas: <nome>"), com os golpes (weapon.<id>.<golpe>.*, lidos em
    // PlayerCombat.GetTunedAttack) e os multiplicadores da arma (weapon.<id>.<campo>, WeaponDefinition.Tuned*).
    // Padroes = valores do catalogo. Arma nova no catalogo entra aqui sozinha.
    private static void RegisterWeapons()
    {
        foreach (var w in WeaponCatalog.All)
        {
            var label = WeaponLabel(w.Id);
            var group = $"{GWeapons}: {label}";
            var key = $"weapon.{w.Id}";
            var soon = w.IsImplemented ? "" : "Arma ainda sem moveset (Em breve): só vale quando ela puder ser equipada.";

            var attacks = new System.Collections.Generic.List<(string id, string name, WeaponAttackDefinition a)>();
            for (var i = 0; i < w.BasicCombo.Length; i++)
                attacks.Add(($"basic_{i + 1}", $"Golpe {i + 1}", w.BasicCombo[i]));
            attacks.Add(("quick", "Golpe rápido", w.QuickAttack));
            attacks.Add(("special", "Especial", w.SpecialAttack));

            foreach (var (id, name, a) in attacks)
            {
                var attackKey = $"{key}.{id}";
                var attackLabel = $"{label} - {name}";
                P($"{attackKey}.damage", $"{attackLabel}: dano", group, a.Damage, 0, 1000, 1, false, DevApplyMode.Live, soon);
                P($"{attackKey}.hit_delay", $"{attackLabel}: atraso do acerto (s)", group, a.HitDelay, 0, 2, 0.01, false, DevApplyMode.Live, soon);
                P($"{attackKey}.recovery", $"{attackLabel}: recuperação (s)", group, a.Recovery, 0, 2, 0.01, false, DevApplyMode.Live, soon);
                P($"{attackKey}.knockback", $"{attackLabel}: knockback", group, a.Knockback, 0, 50, 0.1, false, DevApplyMode.Live, soon);
                P($"{attackKey}.range", $"{attackLabel}: alcance", group, a.Range, 0.1, 20, 0.05, false, DevApplyMode.Live, ("O alcance efetivo é multiplicado pelo multiplicador de alcance da arma (range_mult, padrão 1,3). " + soon).Trim());
                P($"{attackKey}.width", $"{attackLabel}: largura", group, a.Width, 0.1, 20, 0.05, false, DevApplyMode.Live, soon);
                P($"{attackKey}.stagger", $"{attackLabel}: stagger (s)", group, a.StaggerDuration, 0, 3, 0.01, false, DevApplyMode.Live, soon);
            }

            P($"{key}.damage_mult", $"{label}: multiplicador de dano (x)", group, w.DamageMultiplier, 0, 10, 0.05, false, DevApplyMode.Live, ("Vale para todos os golpes da arma. " + soon).Trim());
            P($"{key}.basic_attack_speed", $"{label}: velocidade do combo básico (x)", group, w.BasicAttackSpeedMultiplier, 0.1, 5, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.attack_delay_mult", $"{label}: atraso do combo básico (x)", group, w.AttackDelayMultiplier, 0.1, 5, 0.05, false, DevApplyMode.Live, ("Multiplica atraso do acerto + recuperação do combo básico; maior = mais lento. " + soon).Trim());
            P($"{key}.range_mult", $"{label}: multiplicador de alcance (x)", group, w.RangeMultiplier, 0.1, 5, 0.05, false, DevApplyMode.Live, ("Vale para todos os golpes da arma (padrão 1,3, o antigo ajuste geral 65/50). " + soon).Trim());
            P($"{key}.arc_degrees", $"{label}: arco do golpe (graus)", group, w.AttackArcDegrees, 5, 360, 1, false, DevApplyMode.Live, soon);
            P($"{key}.edge_forgiveness", $"{label}: tolerância de borda", group, w.HitboxEdgeForgiveness, 0, 5, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.quick_hitbox_scale", $"{label}: escala do hitbox do golpe rápido", group, w.QuickAttackHitboxScale, 0.05, 3, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.quick_damage_mult", $"{label}: multiplicador de dano do golpe rápido", group, w.QuickAttackDamageMultiplier, 0, 10, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.backstab_mult", $"{label}: multiplicador de dano pelas costas", group, w.BackstabDamageMultiplier, 1, 10, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.wall_impact_mult", $"{label}: multiplicador de dano de impacto na parede", group, w.WallImpactDamageMultiplier, 0, 10, 0.05, false, DevApplyMode.Live, soon);
            P($"{key}.knockback_basic", $"{label}: multiplicador de knockback básico", group, w.BasicAttackKnockbackMultiplier, 0, 20, 0.1, false, DevApplyMode.Live, soon);
            P($"{key}.knockback_quick", $"{label}: multiplicador de knockback rápido", group, w.QuickAttackKnockbackMultiplier, 0, 20, 0.1, false, DevApplyMode.Live, soon);
            P($"{key}.knockback_special", $"{label}: multiplicador de knockback especial", group, w.SpecialAttackKnockbackMultiplier, 0, 20, 0.1, false, DevApplyMode.Live, soon);
            P($"{key}.lunge", $"{label}: avanço da estocada (distância)", group, w.LineAttackLungeDistance, 0, 10, 0.1, false, DevApplyMode.Live, soon);
            P($"{key}.special_cooldown", $"{label}: recarga do especial (s)", group, w.SpecialAttackCooldown, 0.05, 30, 0.05, false, DevApplyMode.Live, soon);
        }
    }

    private static void RegisterPlayerCamera()
    {
        P("camera.yaw", "Câmera: yaw (graus)", GCamera, IsoCamera.DefaultYawDegrees, -180, 360, 1, false, DevApplyMode.Live, "Só a câmera; o movimento isométrico do jogador continua em 45 graus.");
        P("camera.pitch", "Câmera: pitch (graus)", GCamera, IsoCamera.DefaultPitchDegrees, 10, 89, 1, false);
        P("camera.zoom_distance", "Câmera: distância base", GCamera, IsoCamera.DefaultStartZoomDistance, 3, 60, 0.5, false, DevApplyMode.Live, "Multiplicada pelo ajuste rápido de distância da câmera.");
        P("camera.follow_speed", "Câmera: velocidade de seguimento", GCamera, IsoCamera.DefaultFollowSpeed, 0.5, 100, 0.5, false);
        P("camera.shake_frequency", "Câmera: frequência do tremor", GCamera, IsoCamera.DefaultShakeFrequency, 1, 120, 1, false);
    }
}
