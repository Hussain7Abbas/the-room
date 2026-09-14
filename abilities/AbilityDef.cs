using Godot;

namespace TheRoom.Config;

/// <summary>Archetype slots, CHARACTER-SPEC.md Part 1 — claimed first-come, one per character
/// until all eight are taken.</summary>
public enum ArchetypeSlot
{
    Mobility,
    ZoneDenial,
    Information,
    Burst,
    Displacement,
    Sustain,
    Trap,
    Disruption,
}

/// <summary>Power budget axes, CHARACTER-SPEC.md Part 1 — an ability may do exactly one of
/// these strongly, and touch one other weakly. `None` for the weak slot means "touches nothing
/// else."</summary>
public enum PowerAxis
{
    None,
    Damage,
    Movement,
    Control,
    Information,
}

/// <summary>
/// Metadata for one ability — the reviewable "shape" half of CHARACTER-SPEC.md Part 4. Numbers
/// (cooldown seconds, ranges, damage, durations) do NOT live here; they live in
/// Tuning.AbilityNumbers keyed by Id, which is what the central tuning-file owner edits. This
/// resource is what AbilityValidator.cs (see abilities/AbilityValidator.cs) scans for grammar
/// violations before art work is allowed to start (CHARACTER-SPEC.md Part 3, the review gate).
/// </summary>
[GlobalClass]
public partial class AbilityDef : Resource
{
    [Export] public string Id = ""; // lowercase, matches the Tuning.AbilityNumbers key prefix
    [Export] public string DisplayName = ""; // shown in killfeed as the kill method
    [Export] public ArchetypeSlot Slot = ArchetypeSlot.Mobility;
    [Export] public PowerAxis StrongAxis = PowerAxis.Movement;
    [Export] public PowerAxis WeakAxis = PowerAxis.None;

    [ExportGroup("Review gate (CHARACTER-SPEC.md Part 3, questions 2–3)")]
    [Export(PropertyHint.MultilineText)] public string CounterplaySentence = ""; // "You can X." — must exist, one sentence
    [Export(PropertyHint.MultilineText)] public string TellDescription = ""; // audio + visual, must exist
    [Export(PropertyHint.MultilineText)] public string DynamicCreated = ""; // "what decision does this force on the victim"
    [Export(PropertyHint.MultilineText)] public string FailureCase = ""; // when is this useless

    /// <summary>Whether this ability uses a cooldown (true, read from Tuning as "{Id}.cooldown")
    /// or a charge system (false — charge count/refill live in Tuning too, as "{Id}.charges" /
    /// "{Id}.charge_refill_seconds", with equivalent uptime per CHARACTER-SPEC.md Part 1).</summary>
    [Export] public bool UsesCooldown = true;
}
