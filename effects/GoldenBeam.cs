using Godot;

namespace TheRoom.Effects;

/// <summary>
/// The Golden Knife holder's marker: a column of gold light from the holder's head up into the
/// sky, a warm glow around them and sparks drifting up. It's a child of the holder's Player node on
/// every client, so everyone in the room can see who has the knife from anywhere on the map.
/// Added and removed by MatchServer.BroadcastKnifeHolder; cosmetic only (clients, not headless).
/// </summary>
public partial class GoldenBeam : Node3D
{
    private static readonly Color Gold = new(1f, 0.78f, 0.22f);
    private const float BeamHeight = 160f;  // well past the camera's view: it reads as "to the sky"
    private const float HeadHeight = 1.0f;  // above the player's origin (the capsule's middle)

    private StandardMaterial3D _outer = null!;
    private StandardMaterial3D _core = null!;
    private OmniLight3D _glow = null!;
    private float _time;

    public override void _Ready()
    {
        // The outer column is blended normally so it stays gold against a bright sky (additive
        // light over blue sky went white-lavender); the thin core is additive, for the glow.
        _outer = BeamMaterial(0.22f, BaseMaterial3D.BlendModeEnum.Mix);
        _core = BeamMaterial(0.55f, BaseMaterial3D.BlendModeEnum.Add);
        AddChild(Column(0.55f, 0.9f, _outer));
        AddChild(Column(0.16f, 0.22f, _core));

        _glow = new OmniLight3D
        {
            LightColor = Gold,
            LightEnergy = 3f,
            OmniRange = 7f,
            Position = new Vector3(0, 0.2f, 0),
            ShadowEnabled = false,
        };
        AddChild(_glow);

        var sparkMesh = new QuadMesh
        {
            Size = Vector2.One * 0.12f,
            Material = new StandardMaterial3D
            {
                AlbedoTexture = GD.Load<Texture2D>("res://assets/effects/textures/drop.png"), // round sparks
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                BillboardKeepScale = true,
                VertexColorUseAsAlbedo = true,
            },
        };
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(Gold, 1f));
        ramp.SetColor(1, new Color(Gold, 0f));
        AddChild(new CpuParticles3D
        {
            Mesh = sparkMesh,
            Amount = 40,
            Lifetime = 2.5,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring,
            EmissionRingAxis = Vector3.Up,
            EmissionRingRadius = 0.7f,
            EmissionRingInnerRadius = 0.3f,
            EmissionRingHeight = 0.1f,
            Position = new Vector3(0, -0.8f, 0),
            Direction = Vector3.Up,
            Spread = 8f,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3.5f,
            Gravity = Vector3.Zero,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.4f,
            ColorRamp = ramp,
            Emitting = true,
        });
    }

    public override void _Process(double delta)
    {
        // A slow pulse so it reads as alive, not a static prop.
        _time += (float)delta;
        var pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3f);
        _outer.AlbedoColor = new Color(Gold, 0.3f + 0.12f * pulse);
        _core.AlbedoColor = new Color(Gold, 0.45f + 0.2f * pulse);
        _glow.LightEnergy = 2.4f + 1.4f * pulse;
    }

    private static MeshInstance3D Column(float bottomRadius, float topRadius, Material material) => new()
    {
        Mesh = new CylinderMesh
        {
            BottomRadius = bottomRadius,
            TopRadius = topRadius,
            Height = BeamHeight,
            RadialSegments = 24,
            CapTop = false,
            CapBottom = false,
            Material = material,
        },
        Position = new Vector3(0, HeadHeight + BeamHeight / 2f, 0),
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    private static StandardMaterial3D BeamMaterial(float alpha, BaseMaterial3D.BlendModeEnum blend) => new()
    {
        AlbedoColor = new Color(Gold, alpha),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = blend,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        DisableFog = true, // stays bright at the far side of the arena
    };
}
