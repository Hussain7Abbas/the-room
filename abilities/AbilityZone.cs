using System;
using Godot;
using TheRoom.Core;
using TheRoom.Entities;

namespace TheRoom.Abilities;

/// <summary>
/// Shared-kit helper (CHARACTER-SPEC.md: abilities build on shared systems, they don't invent
/// their own) — a radius-based zone that ticks `onPlayerInZonePerTick` for everyone standing in
/// it, for `durationSeconds`, then frees itself. Used for both "Zone Denial" (sustained tick)
/// and "Trap" (pass `oneShot: true` and free the zone the first time the callback fires) per
/// CHARACTER-SPEC.md's archetype list.
///
/// The tick callback only actually runs where Net.Instance.IsServer is true — every peer still
/// gets its own local instance (spawned via a broadcast RPC from the caster, same pattern as the
/// death-ragdoll effect in Player.cs) purely so the zone is *visible* everywhere for its whole
/// duration, satisfying the review-gate tell requirement without a real VFX asset yet.
/// </summary>
public partial class AbilityZone : Node3D
{
    private float _radius;
    private float _timeRemaining;
    private bool _oneShot;
    private Action<Player, float>? _onPlayerInZonePerTick;

    public static AbilityZone Spawn(Vector3 position, float radius, float durationSeconds, Color color, bool oneShot, Action<Player, float>? onPlayerInZonePerTick)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var root = tree.CurrentScene ?? throw new InvalidOperationException("No current scene to spawn an AbilityZone into.");

        var zone = new AbilityZone
        {
            Position = position,
            _radius = radius,
            _timeRemaining = durationSeconds,
            _oneShot = oneShot,
            _onPlayerInZonePerTick = onPlayerInZonePerTick,
        };
        root.AddChild(zone);
        zone.BuildVisual(radius, color);
        return zone;
    }

    private void BuildVisual(float radius, Color color)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.08f },
            Position = Vector3.Up * 0.04f,
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(color.R, color.G, color.B, 0.55f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        mesh.SetSurfaceOverrideMaterial(0, mat);
        AddChild(mesh);
    }

    public override void _PhysicsProcess(double delta)
    {
        _timeRemaining -= (float)delta;
        if (_timeRemaining <= 0f)
        {
            QueueFree();
            return;
        }

        if (!Net.Instance.IsServer || _onPlayerInZonePerTick is null)
            return; // visual-only on clients; damage/effect logic is authoritative-only

        foreach (var player in CombatServer.Instance.AllPlayers())
        {
            if (player.IsDead)
                continue;

            var delta2D = new Vector2(player.GlobalPosition.X - GlobalPosition.X, player.GlobalPosition.Z - GlobalPosition.Z);
            if (delta2D.Length() > _radius)
                continue;

            _onPlayerInZonePerTick(player, (float)delta);

            if (_oneShot)
            {
                QueueFree();
                return;
            }
        }
    }
}
