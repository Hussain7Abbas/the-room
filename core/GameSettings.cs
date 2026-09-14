using Godot;

namespace TheRoom.Core;

/// <summary>
/// The player's local settings in <c>user://settings.cfg</c>: name and character (main menu) and
/// display mode (settings dialog). Every writer goes through Load → change → Save, so one
/// screen saving its section never wipes another's.
/// </summary>
public static class GameSettings
{
    /// <summary>Settable only so tests can use a scratch file instead of the player's.</summary>
    public static string FilePath { get; set; } = "user://settings.cfg";

    public enum DisplayMode { Maximized, Windowed, Fullscreen }
    public enum TextureQuality { Low, Mid, High }

    public static ConfigFile Load()
    {
        var config = new ConfigFile();
        config.Load(FilePath); // a missing file just means every value is at its default
        return config;
    }

    public static void Save(ConfigFile config) => config.Save(FilePath);

    public static DisplayMode GetDisplayMode() =>
        ParseDisplayMode(Load().GetValue("display", "mode", "maximized").AsString());

    public static DisplayMode ParseDisplayMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "windowed" => DisplayMode.Windowed,
        "fullscreen" => DisplayMode.Fullscreen,
        _ => DisplayMode.Maximized, // the default, and the fallback for anything unknown
    };

    /// <summary>Saves the mode and applies it right away.</summary>
    public static void SetDisplayMode(DisplayMode mode)
    {
        var config = Load();
        config.SetValue("display", "mode", mode.ToString().ToLowerInvariant());
        Save(config);
        ApplyDisplayMode(mode);
    }

    public static void ApplySavedDisplayMode() => ApplyDisplayMode(GetDisplayMode());

    public static TextureQuality GetTextureQuality() =>
        ParseTextureQuality(Load().GetValue("graphics", "texture_quality", "mid").AsString());

    public static TextureQuality ParseTextureQuality(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "low" => TextureQuality.Low,
        "high" => TextureQuality.High,
        _ => TextureQuality.Mid, // the default
    };

    /// <summary>Saves the quality and applies it right away.</summary>
    public static void SetTextureQuality(TextureQuality quality, Viewport viewport)
    {
        var config = Load();
        config.SetValue("graphics", "texture_quality", quality.ToString().ToLowerInvariant());
        Save(config);
        ApplyTextureQuality(quality, viewport);
    }

    public static void ApplySavedTextureQuality(Viewport viewport) => ApplyTextureQuality(GetTextureQuality(), viewport);

    /// <summary>
    /// The mipmap bias picks smaller (Low: softer, lighter on a weak GPU's bandwidth) or sharper
    /// versions of every texture. Anisotropic filtering keeps floors and walls crisp at a slant; the
    /// arena's materials use the anisotropic filter so the level applies. The root viewport
    /// outlives scenes, so applying once at launch covers the menu and every match.
    /// </summary>
    public static void ApplyTextureQuality(TextureQuality quality, Viewport viewport)
    {
        viewport.TextureMipmapBias = quality switch
        {
            TextureQuality.Low => 1.5f,
            TextureQuality.High => -0.25f,
            _ => 0f,
        };
        viewport.AnisotropicFilteringLevel = quality switch
        {
            TextureQuality.Low => Viewport.AnisotropicFiltering.Disabled,
            TextureQuality.High => Viewport.AnisotropicFiltering.Anisotropy16X,
            _ => Viewport.AnisotropicFiltering.Anisotropy4X,
        };
    }

    public static void ApplyDisplayMode(DisplayMode mode)
    {
        // Headless servers and bots have no window. Movie-writer renders (docs/testing.md) must
        // keep their fixed size, so they skip it too.
        if (DisplayServer.GetName() == "headless" || !string.IsNullOrEmpty(Engine.GetWriteMoviePath()))
            return;

        DisplayServer.WindowSetMode(mode switch
        {
            DisplayMode.Windowed => DisplayServer.WindowMode.Windowed,
            DisplayMode.Fullscreen => DisplayServer.WindowMode.Fullscreen,
            _ => DisplayServer.WindowMode.Maximized,
        });
    }
}
