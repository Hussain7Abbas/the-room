using Godot;
using TheRoom.Config;
using TheRoom.Core;
using TheRoom.Entities;

namespace TheRoom.Abilities;


/// <summary>Mobility example ability — see abilities/CharacterRegistry.cs and
/// characters/blink/SPEC.md. Numbers: Tuning.AbilityNumbers "blink.range"/"blink.cooldown"/
/// "blink.tell_seconds".</summary>
public sealed class BlinkAbility : Ability
{
    public BlinkAbility(AbilityDef def, Player caster) : base(def, caster) { }

    protected override void ApplyEffect()
    {
        var range = TuningService.Instance.GetAbilityNumber(Def.Id, "range", 6f);
        var forward = -Caster.GlobalTransform.Basis.Z.Normalized();

        // Sweep the actual capsule instead of teleporting (the Phase 3 version had no collision
        // check at all and could land a player INSIDE a pillar or the centre plinth, whose CSG
        // trimesh colliders can't push a body back out). Player.ServerSweep slides along the floor
        // and stops at the first wall/pillar/player — shared with the heavy lunge. Still instant.
        var travelled = Caster.ServerSweep(forward * range);
        GD.Print($"[Ability] {Main.GetPlayerName(Caster.PeerId)} blinked {travelled:F1}m (of {range:F1}m).");
    }
}
