using Godot;
using TheRoom.Config;
using TheRoom.Core;
using TheRoom.Entities;

namespace TheRoom.Abilities;

/// <summary>
/// Base class for every ability's *shape* (CHARACTER-SPEC.md Part 4) — the lifecycle itself is
/// fixed and not something a subclass can shortcut: input → tell (≥ Tuning.MinAbilityTellTime)
/// → effect → cooldown. TryActivate() refuses to even start an ability whose configured tell or
/// cooldown breaks the hard rules (CHARACTER-SPEC.md Part 1) — those are enforced here in code,
/// not left to a reviewer to catch by reading numbers.
///
/// Server-authoritative only: one instance per (Player, CharacterDef) pair, constructed and
/// ticked exclusively where the combat state machine itself runs (server, or the offline solo
/// instance which is its own authority — see Player.cs class doc).
/// </summary>
public abstract class Ability
{
    public readonly AbilityDef Def;
    protected readonly Player Caster;

    private enum State { Idle, Telling }
    private State _state = State.Idle;
    private float _tellTimeRemaining;
    private float _cooldownRemaining;

    protected Ability(AbilityDef def, Player caster)
    {
        Def = def;
        Caster = caster;
    }

    public bool IsReady => _state == State.Idle && _cooldownRemaining <= 0f;
    public float CooldownRemaining => _cooldownRemaining;

    /// <summary>Call from the owning Player's combat-verb request path. Returns false (and logs
    /// why) if not ready, or if the tuning numbers for this ability violate the grammar.</summary>
    public bool TryActivate()
    {
        if (!IsReady)
            return false;

        var tuning = TuningService.Instance;
        var tellSeconds = tuning.GetAbilityNumber(Def.Id, "tell_seconds", tuning.MinAbilityTellTime);

        if (tellSeconds < tuning.MinAbilityTellTime)
        {
            GD.PushError($"[Ability] {Def.Id}: tell_seconds ({tellSeconds}s) is below Tuning.MinAbilityTellTime " +
                          $"({tuning.MinAbilityTellTime}s) — refusing to activate. Fix the tuning number, not this check.");
            return false;
        }

        if (Def.UsesCooldown)
        {
            var cooldownSeconds = tuning.GetAbilityNumber(Def.Id, "cooldown", tuning.AbilityCooldownMin);
            if (cooldownSeconds < tuning.AbilityCooldownMin || cooldownSeconds > tuning.AbilityCooldownMax)
            {
                GD.PushError($"[Ability] {Def.Id}: cooldown ({cooldownSeconds}s) is outside the " +
                              $"{tuning.AbilityCooldownMin}-{tuning.AbilityCooldownMax}s grammar range " +
                              "(CHARACTER-SPEC.md Part 1) — refusing to activate. Fix the tuning number.");
                return false;
            }
        }

        _state = State.Telling;
        _tellTimeRemaining = tellSeconds;
        Caster.BroadcastAbilityTell(Def.Id, tellSeconds);
        GD.Print($"[Ability] {Main.GetPlayerName(Caster.PeerId)} used {Def.DisplayName} (tell: {tellSeconds:F2}s).");
        return true;
    }

    /// <summary>Call every server (or offline-solo) physics tick from the owning Player.</summary>
    public void Tick(float delta)
    {
        if (_cooldownRemaining > 0f)
            _cooldownRemaining = Mathf.Max(0f, _cooldownRemaining - delta);

        if (_state != State.Telling)
            return;

        _tellTimeRemaining -= delta;
        if (_tellTimeRemaining > 0f)
            return;

        _state = State.Idle;
        var tuning = TuningService.Instance;
        _cooldownRemaining = Def.UsesCooldown
            ? tuning.GetAbilityNumber(Def.Id, "cooldown", tuning.AbilityCooldownMin)
            : 0f;

        ApplyEffect();
    }

    /// <summary>Called once, the instant the tell finishes. Do the actual thing here.</summary>
    protected abstract void ApplyEffect();

    // ------------------------------------------------------------------
    // Shared-kit helpers (CHARACTER-SPEC.md: "requiring new shared systems... raise it as a
    // proposal separately" — abilities build on these, they don't invent their own).
    // ------------------------------------------------------------------

    protected void ApplyDisplacement(Player target, Vector3 impulse) => target.ServerApplyDisplacement(impulse);

    /// <summary>Clamped to Tuning.MaxControlEffectDuration — CHARACTER-SPEC.md's hard 1s cap on
    /// any control effect, enforced here so a subclass literally cannot exceed it.</summary>
    protected void ApplySlow(Player target, float speedMultiplier, float durationSeconds)
    {
        var clamped = Mathf.Min(durationSeconds, TuningService.Instance.MaxControlEffectDuration);
        target.ServerApplySlow(speedMultiplier, clamped);
    }

    protected void Reveal(Player target, float durationSeconds) => target.ServerApplyReveal(durationSeconds);

    /// <summary>Zone Denial / Trap shared kit: a damage-over-time (or one-shot) area, broadcast
    /// from the caster so every client gets a visible copy — see Player.ServerSpawnDamageZone
    /// and AbilityZone's doc comment for why this goes through an RPC instead of a direct spawn.</summary>
    protected void SpawnDamageZone(Vector3 position, float radius, float durationSeconds, float damagePerSecond, Color color)
    {
        Caster.ServerSpawnDamageZone(position, radius, durationSeconds, damagePerSecond, Def.DisplayName, color);
    }
}
