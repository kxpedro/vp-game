using Godot;
using System.Threading.Tasks;

// Encontro com o boss: ele ja esta na sala (sem circulo de spawn), parado e invulneravel, esperando o jogador.
// Quando o jogador chega perto, abre a conversa boss_pre (DialogueManager.TryStartBossEncounter); ao fechar, o boss
// acorda: barra de vida no HUD, aviso de inicio e o primeiro golpe depois de boss.first_move_delay.
// A Guardia Invisivel espera quase transparente e se materializa antes de falar.
public partial class BossController
{
    private bool _dormant;
    private bool _encounterStarted;

    private static float EncounterRadius => G("boss.encounter.radius", 7.5f);
    private static float DormantAlpha => G("boss.invisible_woman.dormant_alpha", 0.2f);

    private void EnterDormant()
    {
        _dormant = true;
        _encounterStarted = false;
        _enemy.SetBossDormant(true);
        _health.SetInvincible(true);
        if (IsInvisibleWoman)
            _enemy.PixelVisual?.SetAlpha(DormantAlpha);
    }

    private void TickDormant()
    {
        if (_encounterStarted) return;
        var target = _enemy.GetCombatTarget();
        if (target == null) return;
        var offset = target.GlobalPosition - _enemy.GlobalPosition;
        offset.Y = 0.0f;
        if (offset.Length() <= EncounterRadius)
            BeginEncounter();
    }

    private async void BeginEncounter()
    {
        _encounterStarted = true;
        _enemy.FaceCombatTarget();
        if (IsInvisibleWoman)
            await Materialize(G("boss.invisible_woman.materialize_seconds", 0.8f));
        if (!IsInstanceValid(this) || !CanContinueEncounter()) return;

        // Outra janela aberta (oferta, ficha): tenta de novo por alguns segundos; sem conversa, acorda assim mesmo.
        var dialogue = DialogueManager.Instance;
        for (var attempt = 0; attempt < 16 && dialogue != null; attempt++)
        {
            if (dialogue.TryStartBossEncounter())
            {
                await ToSignal(dialogue, DialogueManager.SignalName.ConversationEnded);
                break;
            }
            await WaitSeconds(0.25f);
            if (!IsInstanceValid(this) || !CanContinueEncounter()) return;
        }
        if (!IsInstanceValid(this) || !CanContinueEncounter()) return;
        Wake();
    }

    private void Wake()
    {
        if (!_dormant) return;
        _dormant = false;
        _enemy.SetBossDormant(false);
        _health.SetInvincible(false);
        _enemy.PixelVisual?.SetAlpha(1.0f);
        _enemy.FaceCombatTarget();
        GetTree().CallGroup("hud", "BindBoss", _enemy, Definition.DisplayName);
        PlayCue("boss_phase");
        _enemy.FlashWindup();
        if (IsInvisibleWoman)
            BossRuneFx.Flare(GetTree().CurrentScene, _enemy.GlobalPosition, 2.4f, BossRuneFx.Violet, _enemy.GlobalPosition.Y, 0.5f);
        else
            SpawnRing(_enemy.GlobalPosition, 1.0f, 5.0f, 0.5f, new Color(1.0f, 0.85f, 0.4f));
        _moveTimer = G("boss.first_move_delay", 1.1f);
    }

    // Some ou reaparece aos poucos (so no sprite 2D; o modelo 3D nao tem transparencia).
    private async Task Materialize(float seconds)
    {
        var sprite = _enemy.PixelVisual;
        PlayCue("boss_teleport");
        BossRuneFx.Flare(GetTree().CurrentScene, _enemy.GlobalPosition, 1.6f, BossRuneFx.Pale, _enemy.GlobalPosition.Y, seconds);
        if (sprite != null)
            await TweenAlpha(sprite, 1.0f, seconds);
        else
            await WaitSeconds(seconds);
    }

    private async Task TweenAlpha(PixelEnemySprite sprite, float to, float seconds)
    {
        var tween = sprite.CreateTween();
        tween.TweenMethod(Callable.From<float>(sprite.SetAlpha), sprite.Alpha, to, Mathf.Max(seconds, 0.02f));
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private bool CanContinueEncounter() => IsInsideTree() && IsInstanceValid(_enemy) && _health != null && !_health.IsDead;
}
