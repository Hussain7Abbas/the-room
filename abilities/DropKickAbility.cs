using Godot;
using TheRoom.Config;
using TheRoom.Core;
using TheRoom.Entities;

namespace TheRoom.Abilities;

/// <summary>
/// Zain's Drop Kick (Burst: strong = damage, weak = movement). After a 0.45 s tell (the leap,
/// acted out by the drop-kick animation), Zain lunges forward and both feet hit whoever is
/// straight ahead for 2× a normal (light) knife hit, and stagger them. Dodgeable, like every
/// strike. Numbers: tuning.tres AbilityNumbers "dropkick.*".
/// </summary>
public sealed class DropKickAbility : Ability
{
    public DropKickAbility(AbilityDef def, Player caster) : base(def, caster) { }

    protected override void ApplyEffect()
    {
        var tuning = TuningService.Instance;
        var range = tuning.GetAbilityNumber(Def.Id, "range", 3.5f);
        var reach = tuning.GetAbilityNumber(Def.Id, "reach", 1.8f);
        var radius = tuning.GetAbilityNumber(Def.Id, "hit_radius", 1.2f);
        var damage = tuning.MaxHealth * tuning.LightDamagePercent * tuning.GetAbilityNumber(Def.Id, "damage_multiplier", 2f);

        // Same sweep as the heavy lunge: slides, stops at walls and players, never teleports.
        var forward = -Caster.GlobalTransform.Basis.Z.Normalized();
        var travelled = Caster.ServerSweep(forward * range);
        var hit = Strike(reach, radius, damage, staggers: true);
        GD.Print($"[Ability] {Main.GetPlayerName(Caster.PeerId)} drop-kicked {travelled:F1}m, {(hit ? "hit" : "missed")}.");
    }
}
