using Chickensoft.GoDotTest;
using Godot;
using Shouldly;

namespace TheRoom.Tests;

/// <summary>
/// Regression test: the SpringArm3D in Player.tscn was once rotated 180°, putting the camera in
/// front of the player looking back at them — W walked toward the camera and A/D were mirrored.
/// The camera must look the same way the player body faces (-Z).
/// </summary>
public class CameraRigTests : TestClass
{
    public CameraRigTests(Node testScene) : base(testScene) { }

    [Test]
    public void CameraLooksAlongPlayerForward()
    {
        var player = GD.Load<PackedScene>("res://player/Player.tscn").Instantiate<Node3D>();
        try
        {
            // Compose local transforms by hand: the node is never added to the tree, so _Ready
            // (which needs the network autoloads) doesn't run.
            var pivot = player.GetNode<Node3D>("CameraPivot");
            var arm = pivot.GetNode<Node3D>("SpringArm3D");
            var camera = arm.GetNode<Node3D>("Camera3D");
            var cameraInPlayer = pivot.Transform * arm.Transform * camera.Transform;

            var cameraForward = -cameraInPlayer.Basis.Z;
            var playerForward = Vector3.Forward; // -Z
            cameraForward.Dot(playerForward).ShouldBeGreaterThan(0.9f);
        }
        finally
        {
            player.Free();
        }
    }

    /// <summary>Regression: the body turns to face where it moves (it used to stay locked to the
    /// camera, so walking back or sideways slid the character backwards/sideways).</summary>
    [Test]
    public void BodyYawFacesTheMoveDirection()
    {
        TheRoom.Entities.Player.YawFacing(Vector3.Forward).ShouldBe(0f, 0.0001f);
        Mathf.Abs(TheRoom.Entities.Player.YawFacing(Vector3.Back)).ShouldBe(Mathf.Pi, 0.0001f);
        TheRoom.Entities.Player.YawFacing(Vector3.Right).ShouldBe(-Mathf.Pi / 2f, 0.0001f);
        TheRoom.Entities.Player.YawFacing(Vector3.Left).ShouldBe(Mathf.Pi / 2f, 0.0001f);

        // And the yaw really points -Z (the body's forward) along the direction.
        foreach (var dir in new[] { Vector3.Back, Vector3.Right, new Vector3(1, 0, 1).Normalized() })
        {
            var forward = new Basis(Vector3.Up, TheRoom.Entities.Player.YawFacing(dir)) * Vector3.Forward;
            forward.DistanceTo(dir).ShouldBeLessThan(0.0001f);
        }
    }
}
