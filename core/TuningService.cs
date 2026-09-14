using Godot;
using TheRoom.Config;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("Tuning" in project.godot). Loads the single tuning.tres resource on startup
/// and exposes it as a static Instance so any gameplay script can read
/// e.g. <c>TuningService.Instance.HeavyDamagePercent</c>.
/// </summary>
public partial class TuningService : Node
{
    private const string TuningResourcePath = "res://tuning/tuning.tres";

    public static Tuning Instance { get; private set; } = new();

    public override void _Ready()
    {
        if (ResourceLoader.Exists(TuningResourcePath))
        {
            var loaded = GD.Load<Tuning>(TuningResourcePath);
            if (loaded is not null)
            {
                Instance = loaded;
                GD.Print("[TuningService] Loaded ", TuningResourcePath);
                return;
            }
        }

        GD.PushError($"[TuningService] Could not load {TuningResourcePath}; using hardcoded defaults.");
    }
}
