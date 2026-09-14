using Godot;

namespace TheRoom.Entities;

/// <summary>
/// Grey-box player controller: movement + third-person camera only.
/// No combat yet (Phase 2) and no client-prediction/reconciliation yet (Phase 1) —
/// this is the Phase 0 "capsule that walks around" for the local run gate.
/// </summary>
public partial class Player : CharacterBody3D
{
    [Export] public float MoveSpeed = 6.0f;
    [Export] public float MouseSensitivity = 0.0035f;
    [Export] public float MinPitchDegrees = -60f;
    [Export] public float MaxPitchDegrees = 70f;

    [Export] public NodePath CameraPivotPath = "CameraPivot";
    [Export] public NodePath SpringArmPath = "CameraPivot/SpringArm3D";
    [Export] public Label3D? NameLabel;

    private SpringArm3D _springArm = null!;
    private float _pitchRadians;

    public override void _Ready()
    {
        _springArm = GetNode<SpringArm3D>(SpringArmPath);

        // Multiplayer authority defaults to peer 1 (the server) unless a spawner sets it.
        // Offline (no MultiplayerPeer at all) IsMultiplayerAuthority() is always true, which
        // is what we want for solo local testing.
        if (IsMultiplayerAuthority())
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            var camera = _springArm.GetNodeOrNull<Camera3D>("Camera3D");
            camera?.MakeCurrent();
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsMultiplayerAuthority())
            return;

        if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            RotateY(-mouseMotion.Relative.X * MouseSensitivity);

            _pitchRadians = Mathf.Clamp(
                _pitchRadians - mouseMotion.Relative.Y * MouseSensitivity,
                Mathf.DegToRad(MinPitchDegrees),
                Mathf.DegToRad(MaxPitchDegrees));

            var armRotation = _springArm.Rotation;
            armRotation.X = _pitchRadians;
            _springArm.Rotation = armRotation;
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            Input.MouseMode = Input.MouseMode == Input.MouseModeEnum.Captured
                ? Input.MouseModeEnum.Visible
                : Input.MouseModeEnum.Captured;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsMultiplayerAuthority())
            return;

        var velocity = Velocity;

        if (!IsOnFloor())
            velocity.Y -= (float)ProjectSettings.GetSetting("physics/3d/default_gravity") * (float)delta;

        var inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        var direction = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

        if (direction.LengthSquared() > 0.0001f)
        {
            velocity.X = direction.X * MoveSpeed;
            velocity.Z = direction.Z * MoveSpeed;
        }
        else
        {
            velocity.X = Mathf.MoveToward(velocity.X, 0f, MoveSpeed);
            velocity.Z = Mathf.MoveToward(velocity.Z, 0f, MoveSpeed);
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    public void SetDisplayName(string playerName)
    {
        if (NameLabel is not null)
            NameLabel.Text = playerName;
    }
}
