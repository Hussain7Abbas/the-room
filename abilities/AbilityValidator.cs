using System.Collections.Generic;
using System.Linq;
using Godot;
using TheRoom.Core;

namespace TheRoom.Config;

/// <summary>
/// Scans every registered CharacterDef/AbilityDef for grammar violations, per CHARACTER-SPEC.md
/// Part 3's review gate — a mechanical first pass, not a replacement for the actual human review
/// (questions 3, 5, 6 in Part 3 need a person). Run automatically at startup (see Net.cs) so a
/// broken ability is caught immediately rather than discovered mid-playtest.
/// </summary>
public static class AbilityValidator
{
    public readonly record struct Issue(string CharacterId, string Message);

    public static List<Issue> ValidateAll()
    {
        var issues = new List<Issue>();
        var tuning = TuningService.Instance;
        var slotOwners = new Dictionary<ArchetypeSlot, string>();

        foreach (var character in CharacterRegistry.All.Values)
        {
            var ability = character.Ability;
            if (ability is null)
            {
                issues.Add(new Issue(character.Id, "has no Ability assigned."));
                continue;
            }

            // Archetype slot: first-come, one per character until all eight are taken
            // (CHARACTER-SPEC.md Part 1). With only two example characters this can't actually
            // collide yet, but the check is real and will start doing something the moment a
            // third ability is registered.
            if (slotOwners.TryGetValue(ability.Slot, out var owner))
                issues.Add(new Issue(character.Id, $"archetype slot '{ability.Slot}' already claimed by '{owner}'."));
            else
                slotOwners[ability.Slot] = character.Id;

            // Power budget: must have a strong axis; weak axis (if any) must differ from strong.
            if (ability.StrongAxis == PowerAxis.None)
                issues.Add(new Issue(character.Id, $"'{ability.Id}' has no strong power-budget axis set."));
            if (ability.WeakAxis != PowerAxis.None && ability.WeakAxis == ability.StrongAxis)
                issues.Add(new Issue(character.Id, $"'{ability.Id}' sets the same axis as both strong and weak."));

            // Tell: must exist, and its configured seconds must clear the hard minimum.
            if (string.IsNullOrWhiteSpace(ability.TellDescription))
                issues.Add(new Issue(character.Id, $"'{ability.Id}' has no tell description (audio + visual required)."));
            var tellSeconds = tuning.GetAbilityNumber(ability.Id, "tell_seconds", 0f);
            if (tellSeconds < tuning.MinAbilityTellTime)
                issues.Add(new Issue(character.Id, $"'{ability.Id}' tell_seconds ({tellSeconds}s) is below the {tuning.MinAbilityTellTime}s minimum."));

            // Counterplay: must exist as a sentence (cheap check — non-empty and ends with '.').
            if (string.IsNullOrWhiteSpace(ability.CounterplaySentence))
                issues.Add(new Issue(character.Id, $"'{ability.Id}' has no counterplay sentence."));
            else if (!ability.CounterplaySentence.TrimEnd().EndsWith('.'))
                issues.Add(new Issue(character.Id, $"'{ability.Id}' counterplay text doesn't read as a sentence (no trailing '.')."));

            // Cooldown range (or a charge system, whose numbers this doesn't yet model — flag
            // rather than silently pass).
            if (ability.UsesCooldown)
            {
                var cooldown = tuning.GetAbilityNumber(ability.Id, "cooldown", -1f);
                if (cooldown < tuning.AbilityCooldownMin || cooldown > tuning.AbilityCooldownMax)
                    issues.Add(new Issue(character.Id,
                        $"'{ability.Id}' cooldown ({cooldown}s) is outside {tuning.AbilityCooldownMin}-{tuning.AbilityCooldownMax}s."));
            }

            // Review-gate questions 5/6 (does it create a decision, is the character
            // recognisable) need a human; this only checks the fields exist at all.
            if (string.IsNullOrWhiteSpace(ability.DynamicCreated))
                issues.Add(new Issue(character.Id, $"'{ability.Id}' has no answer for \"what dynamic does this create\"."));
            if (string.IsNullOrWhiteSpace(ability.FailureCase))
                issues.Add(new Issue(character.Id, $"'{ability.Id}' has no documented failure case."));
        }

        return issues;
    }

    /// <summary>Runs ValidateAll() and prints a pass/fail summary. Call once at startup
    /// (core/Net.cs) so a broken ability is caught immediately, not mid-playtest.</summary>
    public static void RunAndPrint()
    {
        var issues = ValidateAll();
        if (issues.Count == 0)
        {
            GD.Print($"[AbilityValidator] {CharacterRegistry.All.Count} character(s) checked, no grammar violations found.");
            return;
        }

        GD.PushError($"[AbilityValidator] {issues.Count} grammar violation(s) across {CharacterRegistry.All.Count} character(s):");
        foreach (var group in issues.GroupBy(i => i.CharacterId))
        {
            foreach (var issue in group)
                GD.PushError($"[AbilityValidator]   {group.Key}: {issue.Message}");
        }
    }
}
