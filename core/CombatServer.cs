using System.Collections.Generic;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("CombatServer" in project.godot), server-side only. Keeps a short rolling history
/// of every player's position (Tuning.MaxRewindTimeSeconds deep) and resolves the Phase 1
/// network-spike "stab" verb against it with favor-the-shooter lag compensation: the attacker
/// stays at their current authoritative position, every other player is rewound to where they
/// were `rewindSeconds` ago before the hit check runs. See plan/phase-1-network-spike.md.
/// This whole temporary verb — and the Tuning.Stab* numbers it reads — gets replaced by the
/// real melee system in Phase 2 (light/heavy/parry/dash).
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

    /// <returns>The peer id of the hit victim, or null if the stab connected with nobody.</returns>
    public long? TryResolveStab(long attackerId, Vector3 attackerPos, Vector3 attackerForward, float rewindSeconds)
    {
        var tuning = TuningService.Instance;
        rewindSeconds = Mathf.Clamp(rewindSeconds, 0f, tuning.MaxRewindTimeSeconds);
        double targetTime = Time.GetTicksMsec() / 1000.0 - rewindSeconds;
        var forward = attackerForward.Normalized();

        foreach (var peerId in _players.Keys)
        {
            if (peerId == attackerId)
                continue;
            if (!_history.TryGetValue(peerId, out var samples) || samples.Count == 0)
                continue;

            var rewoundPos = SampleAt(samples, targetTime);
            var toTarget = rewoundPos - attackerPos;
            var forwardDistance = toTarget.Dot(forward);
            if (forwardDistance < 0f || forwardDistance > tuning.StabRange)
                continue;

            var lateral = (toTarget - forward * forwardDistance).Length();
            if (lateral <= tuning.StabHitRadius)
                return peerId;
        }

        return null;
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
