using System.Collections.Generic;
using System.Linq;
using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// Settings, opened from the main menu and the in-game Esc menu.
///   Display: Display Mode (Maximized by default, Windowed, Fullscreen). It applies immediately and
///            is saved, and MainMenu re-applies it at every launch (core/GameSettings.cs).
///   Controls: every action's current binding, read live from the InputMap (project.godot).
/// </summary>
public partial class SettingsDialog : ModalDialog
{
    public enum Tab { Display, Controls }

    private static readonly (string Label, GameSettings.DisplayMode Mode)[] DisplayModes =
    {
        ("Maximized", GameSettings.DisplayMode.Maximized),
        ("Windowed", GameSettings.DisplayMode.Windowed),
        ("Fullscreen", GameSettings.DisplayMode.Fullscreen),
    };

    // Grouped as players think about them. Names are InputMap actions; "Look around" and "Menu"
    // are fixed bindings with no action behind them.
    private static readonly (string Group, (string Label, string? Action, string? Fixed)[] Rows)[] Controls =
    {
        ("MOVEMENT", new (string, string?, string?)[]
        {
            ("Move forward", "move_forward", null),
            ("Move back", "move_back", null),
            ("Move left", "move_left", null),
            ("Move right", "move_right", null),
            ("Jump", "jump", null),
            ("Sprint (hold)", "sprint", null),
            ("Dodge (roll)", "dodge", null),
            ("Look around", null, "Mouse"),
        }),
        ("COMBAT", new (string, string?, string?)[]
        {
            ("Light attack", "attack_light", null),
            ("Heavy attack (lunge)", "attack_heavy", null),
            ("Ability", "ability", null),
        }),
        ("INTERFACE", new (string, string?, string?)[]
        {
            ("Scoreboard (hold)", "scoreboard", null),
            ("Menu", null, "Esc"),
        }),
    };

    private readonly Dictionary<Tab, Button> _tabs = new();
    private readonly Dictionary<Tab, Control> _pages = new();

    public SettingsDialog() : this(Tab.Display) { }

    public SettingsDialog(Tab initialTab) : base("Settings", 560f)
    {
        var tabRow = new HBoxContainer();
        tabRow.AddThemeConstantOverride("separation", 4);
        var group = new ButtonGroup();
        foreach (var tab in new[] { Tab.Display, Tab.Controls })
        {
            var button = UiTheme.Button(tab == Tab.Display ? "Display" : "Controls", "TabButton", () => ShowTab(tab));
            button.ToggleMode = true;
            button.ButtonGroup = group;
            _tabs[tab] = button;
            tabRow.AddChild(button);
        }
        Body.AddChild(tabRow);

        _pages[Tab.Display] = BuildDisplayPage();
        _pages[Tab.Controls] = BuildControlsPage();
        foreach (var page in _pages.Values)
            Body.AddChild(page);

        ShowTab(initialTab);
    }

    private void ShowTab(Tab tab)
    {
        foreach (var (t, page) in _pages)
            page.Visible = t == tab;
        _tabs[tab].ButtonPressed = true;
    }

    private static Control BuildDisplayPage()
    {
        var page = new VBoxContainer { CustomMinimumSize = new Vector2(0, 360) };
        page.AddThemeConstantOverride("separation", 10);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddChild(UiTheme.Label("Display Mode", fontSize: 17));
        var help = UiTheme.Label("Maximized fills the screen and keeps the window frame. Fullscreen hides everything else.", "Muted", fontSize: 13);
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.AddChild(help);
        row.AddChild(text);

        var picker = new OptionButton { CustomMinimumSize = new Vector2(180, 0), FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        foreach (var (label, _) in DisplayModes)
            picker.AddItem(label);
        var current = GameSettings.GetDisplayMode();
        picker.Selected = System.Array.FindIndex(DisplayModes, m => m.Mode == current);
        picker.ItemSelected += index => GameSettings.SetDisplayMode(DisplayModes[index].Mode);
        row.AddChild(picker);

        page.AddChild(row);
        return page;
    }

    private static Control BuildControlsPage()
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 360),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);

        foreach (var (groupName, rows) in Controls)
        {
            list.AddChild(UiTheme.Label(groupName, "Muted", fontSize: 12));
            foreach (var (label, action, fixedBinding) in rows)
            {
                var row = new HBoxContainer();
                var name = UiTheme.Label(label);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                var keys = fixedBinding is not null ? new List<string> { fixedBinding } : Bindings(action!);
                foreach (var key in keys.DefaultIfEmpty("Not bound"))
                    row.AddChild(KeyCap(key));
                list.AddChild(row);
            }
            list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }

        var note = UiTheme.Label("Keys follow your keyboard layout. Rebinding isn't available yet.", "Muted", fontSize: 12);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        list.AddChild(note);
        return scroll;
    }

    private static PanelContainer KeyCap(string text)
    {
        var cap = new PanelContainer { ThemeTypeVariation = "KeyCap" };
        cap.AddChild(UiTheme.Label(text, fontSize: 14));
        return cap;
    }

    /// <summary>Human names of everything bound to an action, e.g. "W", "Space", "Left mouse".</summary>
    public static List<string> Bindings(string action) =>
        InputMap.HasAction(action) ? InputMap.ActionGetEvents(action).Select(Describe).ToList() : new List<string>();

    private static string Describe(InputEvent e) => e switch
    {
        InputEventKey { PhysicalKeycode: Key.Meta } => OS.GetName() == "macOS" ? "Cmd" : "Win",
        InputEventKey key => OS.GetKeycodeString(LayoutKey(key)),
        InputEventMouseButton mouse => mouse.ButtonIndex switch
        {
            MouseButton.Left => "Left mouse",
            MouseButton.Right => "Right mouse",
            MouseButton.Middle => "Middle mouse",
            _ => $"Mouse {(int)mouse.ButtonIndex}",
        },
        _ => e.AsText(),
    };

    /// <summary>Bindings are physical keys (the key's position). Show what that key is labelled on
    /// this player's layout: the W position reads "Z" on AZERTY.</summary>
    private static Key LayoutKey(InputEventKey key)
    {
        if (key.PhysicalKeycode == Key.None)
            return key.Keycode;
        var local = DisplayServer.KeyboardGetKeycodeFromPhysical(key.PhysicalKeycode);
        return local == Key.None ? key.PhysicalKeycode : local;
    }
}
