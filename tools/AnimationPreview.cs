using Godot;
using TheRoom.Animation;

namespace TheRoom.Tools;

/// <summary>
/// Side-view preview of a character model playing every clip in the shared animation set,
/// through the same CharacterModel code the game uses. Use it to check a new model before
/// registering it: `make preview-animations MODEL=res://assets/characters/&lt;name&gt;/&lt;name&gt;.fbx`.
/// Cycles idle, run, jump, light attack, heavy attack, then repeats.
/// </summary>
public partial class AnimationPreview : Node3D
{
    private const float Height = 1.8f; // the Player capsule's height, which CharacterModel scales to

    private CharacterModel _model = null!;
    private HumanoidAnimationSet _set = null!;
    private int _step = -1;
    private float _stepRemaining;

    public override void _Ready()
    {
        var modelPath = CharacterModel.DefaultModelPath;
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--model="))
                modelPath = arg["--model=".Length..];
        }

        var camera = new Camera3D { Position = new Vector3(4.2f, 1.0f, 0f), Current = true };
        AddChild(camera);
        camera.LookAt(new Vector3(0f, 0.9f, 0f));

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-45f, 60f, 0f), ShadowEnabled = true });
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.15f, 0.15f, 0.18f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.55f, 0.55f, 0.6f),
            },
        });
        AddChild(new MeshInstance3D { Mesh = new PlaneMesh { Size = new Vector2(6f, 6f) } });

        _set = GD.Load<HumanoidAnimationSet>(HumanoidAnimationSet.DefaultPath);
        _model = CharacterModel.Create(GD.Load<PackedScene>(modelPath), _set, Height);
        AddChild(_model);
        GD.Print($"[Preview] {modelPath}");
    }

    public override void _Process(double delta)
    {
        _stepRemaining -= (float)delta;
        if (_stepRemaining > 0f)
        {
            // Locomotion clips need a steady "velocity" to stay selected.
            if (_step is 0 or 1 or 2)
                _model.UpdateLocomotion((float)delta, _step == 1 ? new Vector3(0f, 0f, -6f) : Vector3.Zero, _step != 2);
            return;
        }

        _step = (_step + 1) % 5;
        switch (_step)
        {
            case 0: _stepRemaining = 1.0f; GD.Print("[Preview] idle"); break;
            case 1: _stepRemaining = 1.6f; GD.Print("[Preview] run"); break;
            case 2: _stepRemaining = 1.0f; GD.Print("[Preview] jump"); break;
            case 3:
                _stepRemaining = _set.LightAttackDuration + 0.4f;
                _model.PlayAttack(heavy: false, _set.LightAttackDuration);
                GD.Print("[Preview] light attack");
                break;
            case 4:
                _stepRemaining = _set.HeavyAttackDuration + 0.4f;
                _model.PlayAttack(heavy: true, _set.HeavyAttackDuration);
                GD.Print("[Preview] heavy attack");
                break;
        }
    }
}
