using System.Collections.Generic;
using System.Linq;
using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// Settings, opened from the main menu and the in-game Esc menu.
///   Display: Display Mode (Maximized by default, Windowed, Fullscreen). It applies immediately and
///            is saved, and MainMenu re-applies it at every launch (core/GameSettings.cs).
///   Controls: the bindings of the device in use (keyboard and mouse, or an Xbox/PlayStation
///             controller with its button icons), rebindable (core/InputBindings.cs).
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
    private readonly Dictionary<string, Button> _bindButtons = new();
    private VBoxContainer _controls = null!;
    private (string Action, InputBindings.Kind Kind, Button Button, ulong StartedAt)? _capture;
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

    private Control BuildControlsPage()
    {
        _controls = new VBoxContainer { CustomMinimumSize = new Vector2(0, 360) };
        _controls.AddThemeConstantOverride("separation", 8);
        RebuildControls();
        InputDevices.Changed += OnDeviceChanged;
        return _controls;
    }

    public override void _ExitTree() => InputDevices.Changed -= OnDeviceChanged;

    private void OnDeviceChanged()
    {
        _capture = null;
        RebuildControls();
    }

    /// <summary>The Controls tab shows (and rebinds) the device in use: keyboard and mouse, or the
    /// controller, with its Xbox or PlayStation icons. Touching the other device swaps it.</summary>
    private void RebuildControls(string? focusAction = null)
    {
        foreach (var child in _controls.GetChildren())
            child.QueueFree();
        _bindButtons.Clear();
        var kind = InputBindings.KindOf(InputDevices.Current);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 12);
        var headerText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headerText.AddChild(UiTheme.Label(InputDevices.Current switch
        {
            InputDevice.Xbox => "Xbox controller",
            InputDevice.PlayStation => "PlayStation controller",
            _ => "Keyboard & mouse",
        }, fontSize: 17));
        var hint = UiTheme.Label(kind == InputBindings.Kind.Keyboard
            ? "Click a binding, then press the new key or mouse button (Esc cancels). Press any controller button to see the controller's."
            : "Select a binding, then press the new button (Start / Options cancels). Press any key to see the keyboard's.", "Muted", fontSize: 12);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        headerText.AddChild(hint);
        header.AddChild(headerText);
        var reset = UiTheme.Button("Reset to defaults", "GhostButton", () =>
        {
            InputBindings.ResetToDefaults(kind);
            RebuildControls();
        });
        reset.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(reset);
        _controls.AddChild(header);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        _controls.AddChild(scroll);

        foreach (var (groupName, rows) in Controls)
        {
            list.AddChild(UiTheme.Label(groupName, "Muted", fontSize: 12));
            foreach (var (label, action, fixedBinding) in rows)
            {
                var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 38) };
                var name = UiTheme.Label(label);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                if (action is not null && InputBindings.IsRebindable(action, kind))
                    row.AddChild(BindButton(action, kind));
                else if (action is not null)
                    row.AddChild(InputGlyphs.ForAction(action));
                else
                    row.AddChild(FixedGlyph(fixedBinding!, kind));
                list.AddChild(row);
            }
            list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        }

        var note = UiTheme.Label(kind == InputBindings.Kind.Keyboard
            ? "Keys follow your keyboard layout. A key already in use swaps with the old one."
            : "Movement is the left stick and the camera the right stick. A button already in use swaps with the old one.",
            "Muted", fontSize: 12);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        list.AddChild(note);

        if (focusAction is not null && InputDevices.IsGamepad && _bindButtons.TryGetValue(focusAction, out var again))
            again.CallDeferred(Control.MethodName.GrabFocus);
    }

    private Button BindButton(string action, InputBindings.Kind kind)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(170, 36),
            IconAlignment = HorizontalAlignment.Center,
            FocusMode = InputDevices.IsGamepad ? FocusModeEnum.All : FocusModeEnum.None,
        };
        button.AddThemeConstantOverride("icon_max_width", 28);
        ShowBinding(button, action, kind);
        button.Pressed += () =>
        {
            if (_capture is { } previous)
                ShowBinding(previous.Button, previous.Action, previous.Kind);
            _capture = (action, kind, button, Time.GetTicksMsec());
            button.Icon = null;
            button.Text = kind == InputBindings.Kind.Keyboard ? "Press a key…" : "Press a button…";
        };
        _bindButtons[action] = button;
        return button;
    }

    private static void ShowBinding(Button button, string action, InputBindings.Kind kind)
    {
        var events = InputBindings.EventsFor(action, kind);
        var icon = events.Count > 0 ? InputGlyphs.Icon(events[0]) : null;
        button.Icon = icon;
        button.Text = icon is not null ? "" : events.Count == 0 ? "Not bound" : string.Join("  /  ", events.Select(InputGlyphs.Name));
    }

    private static Control FixedGlyph(string text, InputBindings.Kind kind)
    {
        if (kind == InputBindings.Kind.Controller)
        {
            var icon = InputGlyphs.Named(text == "Mouse" ? "stick_r" : "start");
            if (icon is not null)
                return InputGlyphs.IconRect(icon, 30);
        }
        return InputGlyphs.KeyCap(text);
    }

    /// <summary>While a binding waits for its new input, take the next matching press before
    /// anything else sees it (so Esc doesn't close the dialog and a click doesn't press a button).
    /// A press from the other kind of device isn't taken: it switches the tab to that device.</summary>
    public override void _Input(InputEvent @event)
    {
        if (_capture is not { } capture)
            return;
        if (Time.GetTicksMsec() - capture.StartedAt < 150)
        {
            if (@event.IsPressed())
                GetViewport().SetInputAsHandled(); // the press that opened the capture
            return;
        }

        InputEvent? bound = null;
        var cancel = false;
        if (capture.Kind == InputBindings.Kind.Keyboard)
        {
            switch (@event)
            {
                case InputEventKey { Pressed: true, Echo: false } key:
                    if (key.PhysicalKeycode == Key.Escape || key.Keycode == Key.Escape)
                        cancel = true;
                    else
                        bound = new InputEventKey { PhysicalKeycode = key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode, Device = -1 };
                    break;
                case InputEventMouseButton { Pressed: true } mouse
                    when mouse.ButtonIndex is MouseButton.Left or MouseButton.Right or MouseButton.Middle or MouseButton.Xbutton1 or MouseButton.Xbutton2:
                    bound = new InputEventMouseButton { ButtonIndex = mouse.ButtonIndex, Device = -1 };
                    break;
                case InputEventKey or InputEventMouseButton:
                    break;
                default:
                    return;
            }
        }
        else
        {
            switch (@event)
            {
                case InputEventJoypadButton { Pressed: true } pad:
                    if (pad.ButtonIndex == JoyButton.Start)
                        cancel = true;
                    else if (pad.ButtonIndex != JoyButton.Guide)
                        bound = new InputEventJoypadButton { ButtonIndex = pad.ButtonIndex, Device = -1 };
                    break;
                case InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } trigger when trigger.AxisValue > 0.6f:
                    bound = new InputEventJoypadMotion { Axis = trigger.Axis, AxisValue = 1f, Device = -1 };
                    break;
                case InputEventJoypadButton or InputEventJoypadMotion:
                    break;
                case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                    cancel = true;
                    break;
                default:
                    return;
            }
        }

        GetViewport().SetInputAsHandled();
        if (!cancel && bound is null)
            return;
        _capture = null;
        if (bound is not null)
            InputBindings.Rebind(capture.Action, capture.Kind, bound);
        RebuildControls(capture.Action);
    }
}
