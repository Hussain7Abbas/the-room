using Godot;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("Events" in project.godot). Global signal bus so unrelated systems
/// (combat, match state, UI, abilities) don't need direct references to each other.
/// Add signals here as systems are built — keep payloads primitive (ids, names, floats),
/// never Node references, so this stays serialization/network-friendly.
/// </summary>
public partial class Events : Node
{
    public static Events Instance { get; private set; } = null!;

    [Signal] public delegate void PlayerConnectedEventHandler(long peerId);
    [Signal] public delegate void PlayerDisconnectedEventHandler(long peerId);
    [Signal] public delegate void PlayerSpawnedEventHandler(long peerId);

    // Populated from Phase 2 onward:
    // PlayerDamaged, PlayerKilled, AbilityUsed, BountyAnnounced, GoldenKnifePickedUp, LastCallStarted, MatchEnded...

    public override void _Ready()
    {
        Instance = this;
    }
}
