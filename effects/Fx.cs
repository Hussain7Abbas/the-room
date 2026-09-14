using Godot;

namespace TheRoom.Effects;

/// <summary>
/// One-shot visual effects, built in code and freed when done:
///   Blood: a spray of droplets from a landed hit, then small drops on the floor that fade and
///          are gone 3 s later (the floor is never left permanently painted).
///   Dust:  a puff at the feet for sprinting, jumping, landing, rolling and kicks, sized by
///          how hard the move was.
/// Cosmetic only, so every call is a no-op on headless peers (dedicated server, bots).
/// Textures are Kenney's CC0 Particle Pack (assets/effects/textures, see CREDITS.md); the blood
/// splats are its dirt splotches made fully opaque.
/// </summary>
public static class Fx
{
    private const string Dir = "res://assets/effects/textures/";
    public const float BloodDropLifetime = 3f; // seconds until a floor drop has fully vanished
    private const float BloodFadeTime = 1f;    // the last second of that is the fade

    private static readonly Color BloodColor = new(0.36f, 0.01f, 0.02f);
    private static readonly Color DustColor = new(0.6f, 0.53f, 0.44f);

    private static Texture2D[]? _splats;
    private static QuadMesh? _dropMesh;
    private static QuadMesh?[] _smokeMeshes = new QuadMesh?[2];

    private static Node3D? SceneRoot(Node from) =>
        DisplayServer.GetName() == "headless" || !from.IsInsideTree() ? null : from.GetTree().CurrentScene as Node3D;

    public static void Blood(Node from, Vector3 at, Vector3 direction, bool heavy)
    {
        if (SceneRoot(from) is not { } root)
            return;
        direction.Y = 0f;
        direction = direction.LengthSquared() > 0.001f ? direction.Normalized() : Vector3.Forward;

        // The spray: fast droplets that arc down in the direction the blade was going.
        _dropMesh ??= Quad(GD.Load<Texture2D>(Dir + "drop.png"));
        var spray = new CpuParticles3D
        {
            Mesh = _dropMesh,
            Position = at,
            OneShot = true,
            Amount = heavy ? 36 : 22,
            Lifetime = 0.75,
            Explosiveness = 0.92f,
            Direction = direction + Vector3.Up * 0.5f,
            Spread = 40f,
            InitialVelocityMin = heavy ? 3.5f : 2.5f,
            InitialVelocityMax = heavy ? 7f : 5.5f,
            Gravity = new Vector3(0, -14f, 0),
            ScaleAmountMin = 0.05f,
            ScaleAmountMax = heavy ? 0.18f : 0.14f,
            ColorRamp = Fade(BloodColor, 1f, 0.7f),
        };
        root.AddChild(spray);
        spray.Emitting = true;
        FreeAfter(spray, 1.2f);

        // Drops on the floor where the spray comes down, appearing as it lands.
        var floorY = FloorBelow(from, at) ?? at.Y - 1f;
        var count = heavy ? 9 : 6;
        for (var i = 0; i < count; i++)
        {
            var along = (float)GD.RandRange(0.1, heavy ? 1.8 : 1.3);
            var side = new Vector3(direction.Z, 0, -direction.X) * (float)GD.RandRange(-0.55, 0.55);
            var spot = new Vector3(at.X, floorY, at.Z) + direction * along + side;
            var size = (float)GD.RandRange(0.22, heavy ? 0.75 : 0.5);
            SpawnDrop(root, spot, size, delay: 0.06f + along * 0.18f);
        }
    }

    private static void SpawnDrop(Node3D root, Vector3 spot, float size, float delay)
    {
        _splats ??= new[]
        {
            GD.Load<Texture2D>(Dir + "splat_1.png"), GD.Load<Texture2D>(Dir + "splat_2.png"), GD.Load<Texture2D>(Dir + "splat_3.png"),
        };
        // A shallow projector box, so the drop paints the floor (or a deck, a crate top) and not
        // the legs of whoever is standing there.
        var decal = new Decal
        {
            TextureAlbedo = _splats[GD.Randi() % (uint)_splats.Length],
            Modulate = new Color(BloodColor, 0f),
            Size = new Vector3(size, 0.5f, size),
            Position = spot,
            Rotation = new Vector3(0, (float)GD.RandRange(0, Mathf.Tau), 0),
            AlbedoMix = 1f,
            UpperFade = 0.3f,
            LowerFade = 0.3f,
        };
        root.AddChild(decal);
        var tween = decal.CreateTween();
        tween.TweenInterval(delay);
        tween.TweenProperty(decal, "modulate:a", 0.95f, 0.08f);
        tween.TweenInterval(Mathf.Max(0f, BloodDropLifetime - delay - 0.08f - BloodFadeTime));
        tween.TweenProperty(decal, "modulate:a", 0f, BloodFadeTime);
        tween.TweenCallback(Callable.From(decal.QueueFree));
    }

    /// <param name="near">Anywhere on or above the ground under the player (callers pass the
    /// player's origin, which is the middle of its capsule): the puff is snapped to the surface
    /// below, so it always sits on the floor, a deck or a crate top, never in the air.</param>
    /// <param name="strength">0.3 for a sprint step, about 1 for a landing or a kick, up to 2 for a big fall.</param>
    public static void Dust(Node from, Vector3 near, float strength)
    {
        if (SceneRoot(from) is not { } root)
            return;
        if (FloorBelow(from, near) is not { } floorY)
            return; // nothing below within reach (mid-air off an edge): no puff in the sky
        var feet = new Vector3(near.X, floorY, near.Z);
        strength = Mathf.Clamp(strength, 0.2f, 2f);
        var index = (int)(GD.Randi() % 2u);
        _smokeMeshes[index] ??= Quad(GD.Load<Texture2D>(Dir + $"smoke_{index + 1}.png"));

        var puff = new CpuParticles3D
        {
            Mesh = _smokeMeshes[index],
            Position = feet + Vector3.Up * 0.05f,
            OneShot = true,
            Amount = Mathf.RoundToInt(Mathf.Lerp(3f, 16f, strength / 2f)),
            Lifetime = 0.7 + 0.3 * strength,
            Explosiveness = 0.9f,
            Direction = Vector3.Up,
            Spread = 88f, // outward along the ground, barely rising
            InitialVelocityMin = 0.5f * strength,
            InitialVelocityMax = 1.6f * strength,
            Gravity = new Vector3(0, 0.15f, 0),
            DampingMin = 2.5f,
            DampingMax = 3.5f,
            ScaleAmountMin = 0.5f + 0.25f * strength,
            ScaleAmountMax = 0.9f + 0.5f * strength,
            ScaleAmountCurve = Grow(),
            AngleMin = 0f,
            AngleMax = 360f,
            ColorRamp = Fade(DustColor, 0.55f, 0.15f),
        };
        root.AddChild(puff);
        puff.Emitting = true;
        FreeAfter(puff, (float)puff.Lifetime + 0.2f);
    }

    /// <summary>Where the ground is under a point: the first static surface, ignoring players.</summary>
    private static float? FloorBelow(Node from, Vector3 at)
    {
        if (from is not Node3D node3D)
            return null;
        // From a little above the point (so a point right on the floor still finds it) down past
        // the feet; the ray starts inside the player's own capsule, which rays don't hit.
        var query = PhysicsRayQueryParameters3D.Create(at + Vector3.Up * 0.2f, at + Vector3.Down * 4f);
        var hit = node3D.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || hit["collider"].AsGodotObject() is CharacterBody3D)
            return null;
        return hit["position"].AsVector3().Y;
    }

    private static QuadMesh Quad(Texture2D texture) => new()
    {
        Size = Vector2.One,
        Material = new StandardMaterial3D
        {
            AlbedoTexture = texture,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            // Without this the billboard drops each particle's scale: every droplet and puff
            // rendered at the full 1 m quad size.
            BillboardKeepScale = true,
            VertexColorUseAsAlbedo = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        },
    };

    private static Gradient Fade(Color color, float startAlpha, float holdUntil)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(color, startAlpha));
        gradient.SetColor(1, new Color(color, 0f));
        gradient.AddPoint(holdUntil, new Color(color, startAlpha));
        return gradient;
    }

    private static Curve Grow()
    {
        var curve = new Curve();
        curve.AddPoint(new Vector2(0, 0.45f));
        curve.AddPoint(new Vector2(1, 1f));
        return curve;
    }

    private static void FreeAfter(Node node, float seconds) =>
        node.GetTree().CreateTimer(seconds).Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(node))
                node.QueueFree();
        };
}
