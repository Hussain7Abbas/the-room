using Godot;
using TheRoom.Config;
using TheRoom.Core;
using TheRoom.Entities;

namespace TheRoom.Abilities;

/// <summary>Zone Denial example ability — see abilities/CharacterRegistry.cs and
/// characters/firepatch/SPEC.md. Numbers: Tuning.AbilityNumbers "firepatch.range"/
/// "firepatch.radius"/"firepatch.duration"/"firepatch.damage_per_second"/
/// "firepatch.cooldown"/"firepatch.tell_seconds".</summary>
public sealed class FirePatchAbility : Ability
{
    private static readonly Color PatchColor = new(1f, 0.35f, 0.05f);

    public FirePatchAbility(AbilityDef def, Player caster) : base(def, caster) { }

    protected override void ApplyEffect()
    {
        var tuning = TuningService.Instance;
        var range = tuning.GetAbilityNumber(Def.Id, "range", 8f);
        var radius = tuning.GetAbilityNumber(Def.Id, "radius", 2.5f);
        var duration = tuning.GetAbilityNumber(Def.Id, "duration", 4f);
        var damagePerSecond = tuning.GetAbilityNumber(Def.Id, "damage_per_second", 20f);

        var forward = -Caster.GlobalTransform.Basis.Z.Normalized();
        var landingPosition = Caster.GlobalPosition + forward * range;

        SpawnDamageZone(landingPosition, radius, duration, damagePerSecond, PatchColor);
    }
}
