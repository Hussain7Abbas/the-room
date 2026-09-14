using System.Collections.Generic;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Autoload for everything you hear. Two buses under Master, created at startup:
///   Music:   the quiet background track, looping from the menu through every match;
///   Effects: stab, swing, jump, landing, roll, kick, footsteps, death, menu clicks.
/// Settings → Sound sets Main (Master: both), Music and Effects, saved in [audio] of
/// user://settings.cfg. Headless peers (server, bots) load no audio at all.
/// Sounds are CC0 (Kenney RPG Audio and Impact Sounds; music "EmptyCity" by yd), see CREDITS.md.
/// </summary>
public partial class GameAudio : Node
{
    public enum Channel { Main, Music, Effects }

    public const string MusicBus = "Music";
    public const string EffectsBus = "Effects";
    private const string MusicPath = "res://assets/audio/music/empty_city.ogg";
    private const string SfxDir = "res://assets/audio/sfx/";
    private const float MusicTrackDb = -9f; // "quiet" background: well under the effects even at 100%

    public static GameAudio? Instance { get; private set; }
    private static readonly Dictionary<string, AudioStream[]> Sfx = new();

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        EnsureBus(MusicBus);
        EnsureBus(EffectsBus);
        foreach (var channel in new[] { Channel.Main, Channel.Music, Channel.Effects })
            Apply(channel, GetVolume(channel));

        if (IsSilent)
            return;
        var music = GD.Load<AudioStream>(MusicPath);
        if (music is AudioStreamOggVorbis ogg)
            ogg.Loop = true;
        var player = new AudioStreamPlayer { Stream = music, Bus = MusicBus, VolumeDb = MusicTrackDb };
        AddChild(player);
        player.Play();

        GetTree().NodeAdded += node =>
        {
            if (node is BaseButton button)
                button.Pressed += () => PlayUi("ui_click", -10f);
        };
    }

    private static bool IsSilent => DisplayServer.GetName() == "headless";

    public static float DefaultVolume(Channel channel) => channel switch
    {
        Channel.Music => 0.4f,
        _ => 0.8f,
    };

    /// <summary>0..1 (a slider's position), from settings.cfg.</summary>
    public static float GetVolume(Channel channel) => Mathf.Clamp(
        GameSettings.Load().GetValue("audio", channel.ToString().ToLowerInvariant(), DefaultVolume(channel)).AsSingle(), 0f, 1f);

    public static void SetVolume(Channel channel, float value)
    {
        value = Mathf.Clamp(value, 0f, 1f);
        var config = GameSettings.Load();
        config.SetValue("audio", channel.ToString().ToLowerInvariant(), value);
        GameSettings.Save(config);
        Apply(channel, value);
    }

    private static void Apply(Channel channel, float value)
    {
        var bus = AudioServer.GetBusIndex(channel switch
        {
            Channel.Music => MusicBus,
            Channel.Effects => EffectsBus,
            _ => "Master",
        });
        if (bus < 0)
            return;
        // Sliders are linear to the ear only on a curve: squared keeps the bottom half usable.
        AudioServer.SetBusVolumeDb(bus, Mathf.LinearToDb(Mathf.Max(value * value, 0.0001f)));
        AudioServer.SetBusMute(bus, value <= 0.001f);
    }

    private static void EnsureBus(string name)
    {
        if (AudioServer.GetBusIndex(name) >= 0)
            return;
        AudioServer.AddBus();
        var index = AudioServer.BusCount - 1;
        AudioServer.SetBusName(index, name);
        AudioServer.SetBusSend(index, "Master");
    }

    /// <summary>A positional effect in the game world: <paramref name="name"/> picks a random
    /// variant among assets/audio/sfx/name_1, name_2, …</summary>
    public static void Play3D(Node from, string name, Vector3 at, float volumeDb = 0f, float pitch = 1f)
    {
        if (IsSilent || !from.IsInsideTree() || from.GetTree().CurrentScene is not Node3D root || Pick(name) is not { } stream)
            return;
        var player = new AudioStreamPlayer3D
        {
            Stream = stream,
            Bus = EffectsBus,
            Position = at, // the game scene's root sits at the origin; Position is safe before AddChild
            VolumeDb = volumeDb,
            // A little pitch spread so repeats (footsteps, a flurry of hits) don't sound identical.
            PitchScale = pitch * (float)GD.RandRange(0.93, 1.07),
            UnitSize = 8f,
            MaxDistance = 60f,
        };
        root.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    /// <summary>A non-positional effect (menus).</summary>
    public static void PlayUi(string name, float volumeDb = 0f)
    {
        if (IsSilent || Instance is null || Pick(name) is not { } stream)
            return;
        var player = new AudioStreamPlayer { Stream = stream, Bus = EffectsBus, VolumeDb = volumeDb };
        Instance.AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
    }

    private static AudioStream? Pick(string name)
    {
        if (!Sfx.TryGetValue(name, out var variants))
        {
            var found = new List<AudioStream>();
            for (var i = 1; i <= 8; i++)
            {
                foreach (var ext in new[] { "ogg", "mp3", "wav" })
                {
                    var path = $"{SfxDir}{name}_{i}.{ext}";
                    if (ResourceLoader.Exists(path))
                        found.Add(GD.Load<AudioStream>(path));
                }
            }
            variants = found.ToArray();
            Sfx[name] = variants;
            if (variants.Length == 0)
                GD.PushWarning($"[GameAudio] No sound named '{name}' in {SfxDir}.");
        }
        return variants.Length == 0 ? null : variants[GD.Randi() % (uint)variants.Length];
    }
}
