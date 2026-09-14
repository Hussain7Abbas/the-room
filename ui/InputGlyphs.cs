using System.Collections.Generic;
using System.Linq;
using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// How a binding looks on screen: a key cap with the key's name for keyboard and mouse, the
/// button's icon for a controller (Xbox or PlayStation style, following InputDevices.Current).
/// Icons are Kenney's CC0 Input Prompts in assets/ui/input/ (see CREDITS.md).
/// </summary>
public static class InputGlyphs
{
    private static readonly Dictionary<string, Texture2D?> Cache = new();

    /// <summary>Everything bound to an action for the current device, as glyphs in a row.</summary>
    public static HBoxContainer ForAction(string action, int iconSize = 30)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        var events = InputBindings.EventsFor(action, InputBindings.KindOf(InputDevices.Current));
        if (events.Count == 0)
            row.AddChild(KeyCap("Not bound"));
        foreach (var e in events)
            row.AddChild(For(e, iconSize));
        return row;
    }

    public static Control For(InputEvent e, int iconSize = 30) =>
        Icon(e) is { } texture ? IconRect(texture, iconSize) : KeyCap(Name(e));

    public static TextureRect IconRect(Texture2D texture, int size) => new()
    {
        Texture = texture,
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(size, size),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static PanelContainer KeyCap(string text)
    {
        var cap = new PanelContainer { ThemeTypeVariation = "KeyCap", MouseFilter = Control.MouseFilterEnum.Ignore };
        cap.AddChild(UiTheme.Label(text, fontSize: 14));
        return cap;
    }

    /// <summary>The icon for the first controller binding of an action, if the device is a controller.</summary>
    public static Texture2D? ActionIcon(string action) =>
        InputDevices.IsGamepad && InputBindings.EventsFor(action, InputBindings.Kind.Controller).FirstOrDefault() is { } e
            ? Icon(e)
            : null;

    /// <summary>The short text for the first binding of an action on the current device.</summary>
    public static string ActionName(string action) =>
        InputBindings.EventsFor(action, InputBindings.KindOf(InputDevices.Current)).FirstOrDefault() is { } e ? Name(e) : "?";

    /// <summary>A named icon ("start", "stick_r", …) in the current controller's style.</summary>
    public static Texture2D? Named(string icon)
    {
        var style = InputDevices.Current == InputDevice.PlayStation ? "playstation" : "xbox";
        var path = $"res://assets/ui/input/{style}/{icon}.png";
        if (!Cache.TryGetValue(path, out var texture))
        {
            texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            Cache[path] = texture;
        }
        return texture;
    }

    public static Texture2D? Icon(InputEvent e) => IconName(e) is { } name ? Named(name) : null;

    private static string? IconName(InputEvent e) => e switch
    {
        InputEventJoypadButton b => b.ButtonIndex switch
        {
            JoyButton.A => "a",
            JoyButton.B => "b",
            JoyButton.X => "x",
            JoyButton.Y => "y",
            JoyButton.Back => "back",
            JoyButton.Guide => "guide",
            JoyButton.Start => "start",
            JoyButton.LeftStick => "ls",
            JoyButton.RightStick => "rs",
            JoyButton.LeftShoulder => "lb",
            JoyButton.RightShoulder => "rb",
            JoyButton.DpadUp => "dpad_up",
            JoyButton.DpadDown => "dpad_down",
            JoyButton.DpadLeft => "dpad_left",
            JoyButton.DpadRight => "dpad_right",
            _ => null,
        },
        InputEventJoypadMotion m => m.Axis switch
        {
            JoyAxis.TriggerLeft => "lt",
            JoyAxis.TriggerRight => "rt",
            JoyAxis.LeftX or JoyAxis.LeftY => "stick_l",
            JoyAxis.RightX or JoyAxis.RightY => "stick_r",
            _ => null,
        },
        _ => null,
    };

    public static string Name(InputEvent e)
    {
        var ps = InputDevices.Current == InputDevice.PlayStation;
        return e switch
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
            InputEventJoypadButton b => b.ButtonIndex switch
            {
                JoyButton.A => ps ? "Cross" : "A",
                JoyButton.B => ps ? "Circle" : "B",
                JoyButton.X => ps ? "Square" : "X",
                JoyButton.Y => ps ? "Triangle" : "Y",
                JoyButton.LeftShoulder => ps ? "L1" : "LB",
                JoyButton.RightShoulder => ps ? "R1" : "RB",
                JoyButton.Start => ps ? "Options" : "Menu",
                JoyButton.Back => ps ? "Create" : "View",
                _ => $"Button {(int)b.ButtonIndex}",
            },
            InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft } => ps ? "L2" : "LT",
            InputEventJoypadMotion { Axis: JoyAxis.TriggerRight } => ps ? "R2" : "RT",
            InputEventJoypadMotion m => $"Axis {(int)m.Axis}",
            _ => e.AsText(),
        };
    }

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
