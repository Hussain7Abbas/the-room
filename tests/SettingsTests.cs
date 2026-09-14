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
        foreach (var action in new[] { "move_forward", "move_back", "move_left", "move_right", "jump", "dash",
                     "attack_light", "attack_heavy", "parry", "ability", "scoreboard" })
        {
            SettingsDialog.Bindings(action).ShouldNotBeEmpty($"action {action}");
        }
        SettingsDialog.Bindings("attack_light").ShouldContain("Left mouse");
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
