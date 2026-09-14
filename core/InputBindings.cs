using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Player rebinding. The defaults are the InputMap in project.godot; the player's changes live in
/// <c>user://settings.cfg</c>, one section per kind of device, and are applied over the defaults
/// at launch (InputDevices._Ready). Keyboard and controller bindings are separate: rebinding one
/// never touches the other.
/// </summary>
public static class InputBindings
{
    public enum Kind { Keyboard, Controller }

    /// <summary>Actions a player can rebind, in the order Settings lists them.</summary>
    public static readonly string[] Actions =
    {
        "move_forward", "move_back", "move_left", "move_right", "jump", "sprint", "dodge",
        "attack_light", "attack_heavy", "ability", "scoreboard",
    };

    /// <summary>On a controller, movement is the left stick and isn't rebindable.</summary>
    public static bool IsRebindable(string action, Kind kind) =>
        Actions.Contains(action) && !(kind == Kind.Controller && action.StartsWith("move_"));

    public static Kind KindOf(InputDevice device) =>
        device == InputDevice.Keyboard ? Kind.Keyboard : Kind.Controller;

    private static string Section(Kind kind) => kind == Kind.Keyboard ? "bindings_keyboard" : "bindings_controller";

    public static bool IsKind(InputEvent e, Kind kind) => kind == Kind.Keyboard
        ? e is InputEventKey or InputEventMouseButton
        : e is InputEventJoypadButton or InputEventJoypadMotion;

    public static List<InputEvent> EventsFor(string action, Kind kind) =>
        InputMap.HasAction(action)
            ? InputMap.ActionGetEvents(action).Where(e => IsKind(e, kind)).ToList()
            : new List<InputEvent>();

    /// <summary>
    /// Binds <paramref name="input"/> to <paramref name="action"/>, replacing that action's
    /// bindings of the same kind. If another action already used it, the two swap, so nothing
    /// is ever left doubly bound or unbound.
    /// </summary>
    public static void Rebind(string action, Kind kind, InputEvent input)
    {
        var previous = EventsFor(action, kind);
        foreach (var other in Actions)
        {
            if (other == action || !IsRebindable(other, kind))
                continue;
            var clash = EventsFor(other, kind).FirstOrDefault(e => Same(e, input));
            if (clash is null)
                continue;
            InputMap.ActionEraseEvent(other, clash);
            if (previous.Count > 0)
                InputMap.ActionAddEvent(other, previous[0]);
        }
        foreach (var e in previous)
            InputMap.ActionEraseEvent(action, e);
        InputMap.ActionAddEvent(action, input);
        Save(kind);
    }

    /// <summary>Puts one kind's bindings back to project.godot's and forgets the saved ones.</summary>
    public static void ResetToDefaults(Kind kind)
    {
        var otherKind = kind == Kind.Keyboard ? Kind.Controller : Kind.Keyboard;
        var keep = Actions.ToDictionary(a => a, a => EventsFor(a, otherKind));
        InputMap.LoadFromProjectSettings();
        foreach (var (action, events) in keep)
            Replace(action, otherKind, events);
        var config = GameSettings.Load();
        if (config.HasSection(Section(kind)))
            config.EraseSection(Section(kind));
        GameSettings.Save(config);
    }

    public static void ApplySaved()
    {
        var config = GameSettings.Load();
        foreach (var kind in new[] { Kind.Keyboard, Kind.Controller })
        {
            if (!config.HasSection(Section(kind)))
                continue;
            foreach (var action in Actions)
            {
                if (!IsRebindable(action, kind) || !config.HasSectionKey(Section(kind), action))
                    continue;
                var events = config.GetValue(Section(kind), action).AsStringArray()
                    .Select(Parse).OfType<InputEvent>().ToList();
                if (events.Count > 0) // a garbled entry keeps the default rather than unbinding
                    Replace(action, kind, events);
            }
        }
    }

    private static void Save(Kind kind)
    {
        var config = GameSettings.Load();
        foreach (var action in Actions.Where(a => IsRebindable(a, kind)))
            config.SetValue(Section(kind), action, EventsFor(action, kind).Select(Serialize).ToArray());
        GameSettings.Save(config);
    }

    private static void Replace(string action, Kind kind, List<InputEvent> events)
    {
        foreach (var e in EventsFor(action, kind))
            InputMap.ActionEraseEvent(action, e);
        foreach (var e in events)
            InputMap.ActionAddEvent(action, e);
    }

    /// <summary>The same physical input, ignoring pressed state, device and modifiers.</summary>
    public static bool Same(InputEvent a, InputEvent b) => Serialize(a) == Serialize(b);

    public static string Serialize(InputEvent e) => e switch
    {
        InputEventKey key => $"key:{(long)(key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode)}",
        InputEventMouseButton mouse => $"mouse:{(int)mouse.ButtonIndex}",
        InputEventJoypadButton button => $"joybutton:{(int)button.ButtonIndex}",
        InputEventJoypadMotion motion => $"joyaxis:{(int)motion.Axis}:{(motion.AxisValue < 0 ? -1 : 1)}",
        _ => "",
    };

    public static InputEvent? Parse(string text)
    {
        var parts = text.Split(':');
        if (parts.Length < 2 || !long.TryParse(parts[1], out var value))
            return null;
        // Device -1 means "any device", matching project.godot's events.
        return parts[0] switch
        {
            "key" => new InputEventKey { PhysicalKeycode = (Key)value, Device = -1 },
            "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)value, Device = -1 },
            "joybutton" => new InputEventJoypadButton { ButtonIndex = (JoyButton)value, Device = -1 },
            "joyaxis" when parts.Length == 3 && int.TryParse(parts[2], out var sign) =>
                new InputEventJoypadMotion { Axis = (JoyAxis)value, AxisValue = sign < 0 ? -1f : 1f, Device = -1 },
            _ => null,
        };
    }
}
