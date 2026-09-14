using System.Collections.Generic;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("CombatServer" in project.godot), server-side only. Keeps a short rolling history
/// of every player's position (Tuning.MaxRewindTimeSeconds deep) and resolves melee hits
/// (light/heavy — see Player.cs's combat state machine) against it with favor-the-shooter lag
/// compensation: the attacker stays at their current authoritative position, every other player
/// is rewound to where they were `rewindSeconds` ago before the hit check runs.
/// Proven against a real WAN connection during the Phase 1 network spike (see
/// plan/phase-1-network-spike.md) using a placeholder single verb; Phase 2 is the first real
/// consumer, with per-verb range/radius.
/// </summary>
public partial class CombatServer : Node
{
    public static CombatServer Instance { get; private set; } = null!;

    private readonly struct Sample
    {
        public readonly double Time;
        public readonly Vector3 Position;
        public Sample(double time, Vector3 position) { Time = time; Position = position; }
    }

    private readonly Dictionary<long, Node3D> _players = new();
    private readonly Dictionary<long, List<Sample>> _history = new();

    public override void _Ready()
    {
        Instance = this;
    }

    public void RegisterPlayer(long peerId, Node3D node)
    {
        _players[peerId] = node;
        _history[peerId] = new List<Sample>();
    }

    public void UnregisterPlayer(long peerId)
    {
        _players.Remove(peerId);
        _history.Remove(peerId);
    }

    public Player? GetPlayerNode(long peerId) => _players.TryGetValue(peerId, out var node) ? node as Player : null;

    /// <summary>All currently-registered players (server only) — used by area-effect abilities
    /// (e.g. abilities/FirePatchZone.cs) that need to check everyone in a radius, not one target.</summary>
    public IEnumerable<Player> AllPlayers()
    {
        foreach (var node in _players.Values)
        {
            if (IsInstanceValid(node) && node is Player p)
                yield return p;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!Net.Instance.IsServer)
            return;

        double now = Time.GetTicksMsec() / 1000.0;
        float maxAge = TuningService.Instance.MaxRewindTimeSeconds;

        foreach (var (peerId, node) in _players)
        {
            if (!IsInstanceValid(node))
                continue;

            var list = _history[peerId];
            list.Add(new Sample(now, node.GlobalPosition));
            while (list.Count > 1 && now - list[0].Time > maxAge)
                list.RemoveAt(0);
        }
    }

    /// <summary>
    /// Cone-shaped melee hit check: the closest rewound victim within `range` in front of the
    /// attacker and within `hitRadius` laterally. `range`/`hitRadius` let light and heavy share
    /// this one implementation despite having different reach (Tuning.Light*/Heavy* in Player.cs).
    /// </summary>
    /// <returns>The peer id of the closest hit victim, or null if nobody was in the cone.</returns>
    public long? TryResolveMeleeHit(long attackerId, Vector3 attackerPos, Vector3 attackerForward, float range, float hitRadius, float rewindSeconds)
    {
        var tuning = TuningService.Instance;
        rewindSeconds = Mathf.Clamp(rewindSeconds, 0f, tuning.MaxRewindTimeSeconds);
        double targetTime = Time.GetTicksMsec() / 1000.0 - rewindSeconds;
        var forward = attackerForward.Normalized();

        long? closestPeerId = null;
        var closestDistance = float.MaxValue;

        foreach (var peerId in _players.Keys)
        {
            if (peerId == attackerId)
                continue;
            if (!_history.TryGetValue(peerId, out var samples) || samples.Count == 0)
                continue;

            var rewoundPos = SampleAt(samples, targetTime);
            var toTarget = rewoundPos - attackerPos;
            var forwardDistance = toTarget.Dot(forward);
            if (forwardDistance < 0f || forwardDistance > range)
                continue;

            var lateral = (toTarget - forward * forwardDistance).Length();
            if (lateral > hitRadius)
                continue;

            if (forwardDistance < closestDistance)
            {
                closestDistance = forwardDistance;
                closestPeerId = peerId;
            }
        }

        return closestPeerId;
    }

    /// <summary>The rewound position used for hit-resolution, exposed for execute's
    /// "was the attacker behind the victim" angle check (Player.cs).</summary>
    public Vector3? GetRewoundPosition(long peerId, float rewindSeconds)
    {
        if (!_history.TryGetValue(peerId, out var samples) || samples.Count == 0)
            return null;

        rewindSeconds = Mathf.Clamp(rewindSeconds, 0f, TuningService.Instance.MaxRewindTimeSeconds);
        double targetTime = Time.GetTicksMsec() / 1000.0 - rewindSeconds;
        return SampleAt(samples, targetTime);
    }

    private static Vector3 SampleAt(List<Sample> samples, double time)
    {
        if (samples.Count == 1)
            return samples[0].Position;

        for (var i = samples.Count - 1; i > 0; i--)
        {
            if (samples[i - 1].Time <= time && time <= samples[i].Time)
            {
                var span = samples[i].Time - samples[i - 1].Time;
                var t = span > 0 ? (float)((time - samples[i - 1].Time) / span) : 0f;
                return samples[i - 1].Position.Lerp(samples[i].Position, t);
            }
        }

        // Requested time is outside the buffered range entirely — clamp to the nearest end.
        return time <= samples[0].Time ? samples[0].Position : samples[^1].Position;
    }
}
