using System.Collections.Generic;
using Godot;

namespace TheRoom.Config;

/// <summary>
/// Every playable CharacterDef, keyed by id. Built directly in C# rather than authored as .tres
/// files — CharacterDef/AbilityDef are still real Godot Resources (AbilityValidator scans them
/// the same either way), this just skips hand-writing resource-file text for two example
/// characters that exist to prove the framework, not to ship.
///
/// IMPORTANT — these two ("blink", "firepatch") are placeholder examples Claude wrote to
/// exercise the ability grammar end-to-end (CHARACTER-SPEC.md Part 1) for Phase 3, NOT real
/// character submissions. Per Pillar 3 ("every character is a recognisable caricature of the
/// person who made them"), a real character needs a real owner writing their own spec — replace
/// these, don't build on them, once actual developers claim slots. See characters/blink/SPEC.md
/// and characters/firepatch/SPEC.md for the honest, filled-in review-gate answers.
/// </summary>
public static class CharacterRegistry
{
    private static Dictionary<string, CharacterDef>? _characters;

    public static IReadOnlyDictionary<string, CharacterDef> All => _characters ??= Build();

    public static CharacterDef GetOrDefault(string? id)
    {
        if (id is not null && All.TryGetValue(id, out var def))
            return def;

        // Deterministic fallback so an unknown/omitted --character still gets a real character
        // rather than crashing — first entry in insertion order.
        foreach (var d in All.Values)
            return d;

        throw new System.InvalidOperationException("CharacterRegistry has no characters registered.");
    }

    private static Dictionary<string, CharacterDef> Build()
    {
        var blinkAbility = new AbilityDef
        {
            Id = "blink",
            DisplayName = "Blink",
            Slot = ArchetypeSlot.Mobility,
            StrongAxis = PowerAxis.Movement,
            WeakAxis = PowerAxis.None,
            UsesCooldown = true,
            TellDescription = "A bright blue flash on the caster's whole body, 0.3s before the teleport lands (no audio asset yet — Phase 5).",
            CounterplaySentence = "You can reposition, attack, or parry during the 0.3s flash before the blink actually happens — it's not instant, and it doesn't grant any damage, healing, or invulnerability.",
            DynamicCreated = "Forces the victim to decide, the instant they see the flash, whether to commit to a punish on a now-repositioning target or hold their spacing — guessing wrong either way costs them something.",
            FailureCase = "Useless as a panic button mid-exchange: the 0.3s tell means anyone already swinging at you will land their hit before you're gone. It only helps you get TO or AWAY FROM a fight that hasn't started yet.",
        };

        var blink = new CharacterDef
        {
            Id = "blink",
            DisplayName = "Blink Test Character",
            OwnerName = "(placeholder — Phase 3 framework example, not a real submission)",
            OneLinePersonality = "Never actually there when you swing.",
            SilhouetteColor = new Color(0.25f, 0.55f, 1.0f), // blue
            Ability = blinkAbility,
            PassiveDescription = "5% faster acceleration out of a standstill.",
            PassiveWhyItFits = "Small, flavour-only, doesn't change a duel's outcome — matches the ability's whole identity of 'always just out of reach.'",
            VoiceLineOnKill = "(placeholder)",
            VoiceLineOnDeath = "(placeholder)",
            VoiceLineOnParry = "(placeholder)",
            DeathAnimationNotes = "(placeholder — Phase 5)",
        };

        var firePatchAbility = new AbilityDef
        {
            Id = "firepatch",
            DisplayName = "Fire Patch",
            Slot = ArchetypeSlot.ZoneDenial,
            StrongAxis = PowerAxis.Damage,
            WeakAxis = PowerAxis.None,
            UsesCooldown = true,
            TellDescription = "A thrown orange arc to the landing spot during the 0.3s tell, then a visible orange patch on the ground for its whole duration (no audio asset yet — Phase 5).",
            CounterplaySentence = "You can see exactly where the patch lands and how big it is before it starts ticking — just don't stand in it, or step out the moment you're in it.",
            DynamicCreated = "Turns a piece of ground into a toll: anyone who needs to cross it (a doorway, the Golden Knife plinth) has to decide whether the fight on the other side is worth the chip damage.",
            FailureCase = "Useless against anyone already repositioning who was never going to cross that ground — the caster gets nothing back for throwing it on empty space, and the cooldown is now spent.",
        };

        var firePatch = new CharacterDef
        {
            Id = "firepatch",
            DisplayName = "Fire Patch Test Character",
            OwnerName = "(placeholder — Phase 3 framework example, not a real submission)",
            OneLinePersonality = "Makes you pay a toll to walk through a doorway.",
            SilhouetteColor = new Color(1.0f, 0.45f, 0.1f), // orange
            Ability = firePatchAbility,
            PassiveDescription = "Footsteps are 10% quieter.",
            PassiveWhyItFits = "Small, flavour-only; a slightly sneakier fit for someone who wants you to walk into a trap you didn't hear coming.",
            VoiceLineOnKill = "(placeholder)",
            VoiceLineOnDeath = "(placeholder)",
            VoiceLineOnParry = "(placeholder)",
            DeathAnimationNotes = "(placeholder — Phase 5)",
        };

        return new Dictionary<string, CharacterDef>
        {
            [blink.Id] = blink,
            [firePatch.Id] = firePatch,
        };
    }
}
