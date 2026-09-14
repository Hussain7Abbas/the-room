using System.Collections.Generic;
using Godot;

namespace TheRoom.Config;

/// <summary>
/// Every playable CharacterDef, keyed by id. Built directly in C# rather than authored as .tres
/// files — CharacterDef/AbilityDef are still real Godot Resources (AbilityValidator scans them
/// the same either way), this just skips hand-writing resource-file text for two example
/// characters that exist to prove the framework, not to ship.
///
/// IMPORTANT — "firepatch" is a placeholder example Claude wrote to
/// exercise the ability grammar end-to-end (CHARACTER-SPEC.md Part 1) for Phase 3, NOT real
/// character submissions. Per Pillar 3 ("every character is a recognisable caricature of the
/// person who made them"), a real character needs a real owner writing their own spec — replace
/// these, don't build on them, once actual developers claim slots. See characters/firepatch/SPEC.md for the honest, filled-in review-gate answers.
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
            VoiceLineOnDodge = "(placeholder)",
            DeathAnimationNotes = "(placeholder — Phase 5)",
        };

        // Zain: the team's own character (the model under every character today), with the
        // drop kick the user designed (2026-09-14). See characters/zain/SPEC.md.
        var dropKickAbility = new AbilityDef
        {
            Id = "dropkick",
            DisplayName = "Drop Kick",
            Slot = ArchetypeSlot.Burst,
            StrongAxis = PowerAxis.Damage,
            WeakAxis = PowerAxis.Movement,
            UsesCooldown = true,
            TellDescription = "Zain crouches and leaps for 0.45s before both feet land. The jump itself is the warning.",
            CounterplaySentence = "Roll or sidestep during the 0.45s leap: it only hits what is straight in front of him when he lands.",
            DynamicCreated = "Zain can close 3.5m and hit twice as hard as a knife, so standing still in front of him is a gamble, but a whiffed kick puts him on a 14s cooldown right in your face.",
            FailureCase = "Useless against anyone who rolls through it or isn't dead ahead; it's a straight line, and the leap announces it.",
        };

        var zain = new CharacterDef
        {
            Id = "zain",
            DisplayName = "Zain",
            OwnerName = "Zain (Voidra team)",
            OneLinePersonality = "Settles every argument with both feet.",
            SilhouetteColor = new Color(0.35f, 0.85f, 0.45f), // green
            Ability = dropKickAbility,
            AnimationsPath = "res://assets/characters/zain/zain_animations.tres",
            PassiveDescription = "(none yet)",
            PassiveWhyItFits = "(none yet)",
            VoiceLineOnKill = "(placeholder)",
            VoiceLineOnDeath = "(placeholder)",
            VoiceLineOnDodge = "(placeholder)",
            DeathAnimationNotes = "Mixamo death clip (shared).",
        };

        return new Dictionary<string, CharacterDef>
        {
            [zain.Id] = zain,
            [firePatch.Id] = firePatch,
        };
    }
}
