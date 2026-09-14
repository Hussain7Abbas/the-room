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

        // Grey-box simplification: no collision check on the teleport itself (a real blink
        // would want to clamp to the nearest clear point if the target is inside geometry) —
        // fine for a Phase 3 framework example, worth revisiting before this ships on a real
        // character with a real map to abuse.
        var target = Caster.GlobalPosition + forward * range;
        GD.Print($"[Ability] {Main.GetPlayerName(Caster.PeerId)} blinked {range:F1}m.");
        Caster.ServerTeleport(target);
    }
}
