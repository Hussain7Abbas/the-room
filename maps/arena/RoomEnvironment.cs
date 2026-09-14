using Godot;

namespace TheRoom.Maps;

/// <summary>
/// The arena's WorldEnvironment. Adds the panorama sky at run time, and only on clients: the
/// dedicated server loads Room.tscn too but never imports textures (deploy/Makefile excludes
/// .godot), so a sky referenced from the scene file would fail to load there. Lighting is
/// unchanged: ambient light stays the scene's flat colour, only the background becomes the sky.
/// </summary>
public partial class RoomEnvironment : WorldEnvironment
{
    [Export(PropertyHint.File, "*.jpg,*.png,*.exr,*.hdr")]
    public string PanoramaPath = "res://assets/environment/in_the_clouds_6k.jpg";

    [Export] public float SkyEnergy = 1.0f;

    public override void _Ready()
    {
        if (DisplayServer.GetName() == "headless" || Environment is null || string.IsNullOrEmpty(PanoramaPath))
            return;

        var panorama = GD.Load<Texture2D>(PanoramaPath);
        if (panorama is null)
            return; // missing asset: keep the flat background rather than fail

        Environment.Sky = new Sky
        {
            SkyMaterial = new PanoramaSkyMaterial { Panorama = panorama, EnergyMultiplier = SkyEnergy },
        };
        Environment.BackgroundMode = Godot.Environment.BGMode.Sky;
    }
}
