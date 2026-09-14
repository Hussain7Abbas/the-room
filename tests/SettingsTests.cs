using Chickensoft.GoDotTest;
using Godot;
using Shouldly;
using TheRoom.Core;
using TheRoom.UI;

namespace TheRoom.Tests;

public class SettingsTests : TestClass
{
    public SettingsTests(Node testScene) : base(testScene) { }

    [Test]
    public void DisplayModeDefaultsToMaximized()
    {
        GameSettings.ParseDisplayMode(null).ShouldBe(GameSettings.DisplayMode.Maximized);
        GameSettings.ParseDisplayMode("").ShouldBe(GameSettings.DisplayMode.Maximized);
        GameSettings.ParseDisplayMode("nonsense").ShouldBe(GameSettings.DisplayMode.Maximized);
        GameSettings.ParseDisplayMode("Windowed").ShouldBe(GameSettings.DisplayMode.Windowed);
        GameSettings.ParseDisplayMode(" fullscreen ").ShouldBe(GameSettings.DisplayMode.Fullscreen);
    }

    /// <summary>The Controls tab reads bindings live from the InputMap. If an action is renamed in
    /// project.godot, this fails instead of the tab quietly showing "Not bound".</summary>
    [Test]
    public void EveryListedActionHasABinding()
    {
        foreach (var action in InputBindings.Actions)
        {
            InputBindings.EventsFor(action, InputBindings.Kind.Keyboard).ShouldNotBeEmpty($"keyboard {action}");
            InputBindings.EventsFor(action, InputBindings.Kind.Controller).ShouldNotBeEmpty($"controller {action}");
        }
        foreach (var action in new[] { "look_left", "look_right", "look_up", "look_down", "pause_menu" })
            InputMap.HasAction(action).ShouldBeTrue(action);
        InputGlyphs.Name(InputBindings.EventsFor("attack_light", InputBindings.Kind.Keyboard)[0]).ShouldBe("Left mouse");
        InputGlyphs.Icon(InputBindings.EventsFor("jump", InputBindings.Kind.Controller)[0]).ShouldNotBeNull();
    }

    /// <summary>Rebinding to an input another action uses swaps the two; the other device's
    /// bindings are untouched; reset restores project.godot's; all of it survives a reload.</summary>
    [Test]
    public void RebindSwapsSavesAndResets()
    {
        var realFile = GameSettings.FilePath;
        GameSettings.FilePath = "user://settings-rebind-test.cfg";
        try
        {
            string First(string action, InputBindings.Kind kind) =>
                InputBindings.Serialize(InputBindings.EventsFor(action, kind)[0]);
            var jumpKey = First("jump", InputBindings.Kind.Keyboard);
            var abilityKey = First("ability", InputBindings.Kind.Keyboard);
            var jumpPad = First("jump", InputBindings.Kind.Controller);

            InputBindings.Rebind("jump", InputBindings.Kind.Keyboard, InputBindings.Parse(abilityKey)!);
            First("jump", InputBindings.Kind.Keyboard).ShouldBe(abilityKey);
            First("ability", InputBindings.Kind.Keyboard).ShouldBe(jumpKey);
            First("jump", InputBindings.Kind.Controller).ShouldBe(jumpPad);

            InputMap.LoadFromProjectSettings();
            InputBindings.ApplySaved();
            First("jump", InputBindings.Kind.Keyboard).ShouldBe(abilityKey);

            InputBindings.Rebind("dodge", InputBindings.Kind.Controller, InputBindings.Parse("joyaxis:4:1")!);
            First("sprint", InputBindings.Kind.Controller).ShouldBe("joybutton:1"); // swapped with dodge's B

            InputBindings.ResetToDefaults(InputBindings.Kind.Keyboard);
            First("jump", InputBindings.Kind.Keyboard).ShouldBe(jumpKey);
            First("dodge", InputBindings.Kind.Controller).ShouldBe("joyaxis:4:1"); // controller kept
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(GameSettings.FilePath));
            GameSettings.FilePath = realFile;
            InputMap.LoadFromProjectSettings();
            InputBindings.ApplySaved();
        }
    }

    [Test]
    public void TextureQualityDefaultsToMid()
    {
        GameSettings.ParseTextureQuality(null).ShouldBe(GameSettings.TextureQuality.Mid);
        GameSettings.ParseTextureQuality("nonsense").ShouldBe(GameSettings.TextureQuality.Mid);
        GameSettings.ParseTextureQuality(" Low ").ShouldBe(GameSettings.TextureQuality.Low);
        GameSettings.ParseTextureQuality("high").ShouldBe(GameSettings.TextureQuality.High);
    }

    /// <summary>Volumes round-trip through settings.cfg, and the buses the sliders drive exist.</summary>
    [Test]
    public void VolumesSaveAndBusesExist()
    {
        var realFile = GameSettings.FilePath;
        GameSettings.FilePath = "user://settings-audio-test.cfg";
        try
        {
            GameAudio.GetVolume(GameAudio.Channel.Music).ShouldBe(GameAudio.DefaultVolume(GameAudio.Channel.Music));
            GameAudio.SetVolume(GameAudio.Channel.Effects, 0.25f);
            GameAudio.GetVolume(GameAudio.Channel.Effects).ShouldBe(0.25f, 0.001f);
            GameAudio.SetVolume(GameAudio.Channel.Music, 7f);
            GameAudio.GetVolume(GameAudio.Channel.Music).ShouldBe(1f); // clamped
            AudioServer.GetBusIndex(GameAudio.MusicBus).ShouldBeGreaterThan(0);
            AudioServer.GetBusIndex(GameAudio.EffectsBus).ShouldBeGreaterThan(0);
        }
        finally
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(GameSettings.FilePath));
            GameSettings.FilePath = realFile;
            foreach (var channel in new[] { GameAudio.Channel.Main, GameAudio.Channel.Music, GameAudio.Channel.Effects })
                GameAudio.SetVolume(channel, GameAudio.GetVolume(channel)); // re-apply the player's own
        }
    }

    [Test]
    public void SavingOneSectionKeepsTheOthers()
    {
        // The bug this guards against: the menu used to save name/character into a fresh
        // ConfigFile, wiping the display mode stored in the same file.
        var config = new ConfigFile();
        config.SetValue("display", "mode", "windowed");
        config.SetValue("player", "name", "Ana");
        var path = "user://settings-test.cfg";
        config.Save(path);

        var reloaded = new ConfigFile();
        reloaded.Load(path);
        reloaded.SetValue("player", "name", "Ben");
        reloaded.Save(path);

        var final = new ConfigFile();
        final.Load(path);
        final.GetValue("display", "mode").AsString().ShouldBe("windowed");
        final.GetValue("player", "name").AsString().ShouldBe("Ben");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
    }
}
