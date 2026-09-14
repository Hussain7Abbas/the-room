using System;
using Godot;

namespace TheRoom.Core;

public enum InputDevice { Keyboard, Xbox, PlayStation }

/// <summary>
/// Autoload that tracks what the player is holding. Any button on any controller switches to it
/// (a PlayStation pad by its name, anything else counts as Xbox); any key or click switches back.
/// Settings → Controls and the HUD show that device's bindings and button icons.
///
/// The menus are mouse-first (their buttons don't take focus), so while a controller is active
/// this also makes buttons focusable and keeps one focused, which lets the D-pad/stick and A
/// drive every screen.
/// </summary>
public partial class InputDevices : Node
{
    public static InputDevice Current { get; private set; } = InputDevice.Keyboard;
    public static bool IsGamepad => Current != InputDevice.Keyboard;

    /// <summary>Raised when <see cref="Current"/> changes.</summary>
    public static event Action? Changed;

    private const float StickThreshold = 0.5f;
    private double _focusCheckIn;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        InputBindings.ApplySaved();
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _Input(InputEvent @event)
    {
        InputDevice? next = @event switch
        {
            InputEventJoypadButton { Pressed: true } b => FromController(b.Device),
            InputEventJoypadMotion m when Mathf.Abs(m.AxisValue) > StickThreshold => FromController(m.Device),
            InputEventKey { Pressed: true } => InputDevice.Keyboard,
            // Not mouse motion: capturing the mouse (closing the menu with Start) warps the cursor
            // and sends a motion event, which flipped a controller player back to keyboard icons.
            InputEventMouseButton { Pressed: true } => InputDevice.Keyboard,
            _ => null,
        };
        if (next is { } device && device != Current)
            SetCurrent(device, @event.Device);
    }

    public override void _Process(double delta)
    {
        if (!IsGamepad || Input.MouseMode == Input.MouseModeEnum.Captured)
            return;
        _focusCheckIn -= delta;
        if (_focusCheckIn > 0)
            return;
        _focusCheckIn = 0.2; // dialogs open and close under us; re-home the focus when it's lost
        var focused = GetViewport().GuiGetFocusOwner();
        if (focused is null || !focused.IsVisibleInTree())
            FocusFirstButton();
    }

    /// <summary>Which icon set a controller gets, from the name SDL reports for it.</summary>
    public static InputDevice FromController(int device)
    {
        var name = Input.GetJoyName(device).ToLowerInvariant();
        foreach (var hint in new[] { "playstation", "dualshock", "dualsense", "ps3", "ps4", "ps5", "sony" })
        {
            if (name.Contains(hint))
                return InputDevice.PlayStation;
        }
        return InputDevice.Xbox;
    }

    private void SetCurrent(InputDevice device, int joypad)
    {
        var wasGamepad = IsGamepad;
        Current = device;
        GD.Print($"[Input] Now using {device}{(IsGamepad ? $" ({Input.GetJoyName(joypad)})" : "")}.");
        if (IsGamepad != wasGamepad)
        {
            foreach (var node in GetTree().Root.FindChildren("*", "Control", true, false))
                SetFocusable(node);
            if (IsGamepad)
                _focusCheckIn = 0;
            else
                GetViewport().GuiReleaseFocus(); // no focus rings left behind for the mouse
        }
        Changed?.Invoke();
    }

    private static void OnNodeAdded(Node node) => SetFocusable(node);

    private static void SetFocusable(Node node)
    {
        // LinkButtons and the menus' option pickers keep their own mode (they open the browser or
        // a popup); everything else follows the device.
        if (node is Button button and not OptionButton)
            button.FocusMode = IsGamepad ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
        else if (node is Slider slider) // Settings → Sound: left/right on the D-pad moves it
            slider.FocusMode = IsGamepad ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
    }

    /// <summary>Focuses the first button of the top-most open dialog, or of the screen.</summary>
    private void FocusFirstButton()
    {
        Node scope = GetTree().Root;
        foreach (var dialog in GetTree().Root.FindChildren("*", "Control", true, false))
        {
            if (dialog is UI.ModalDialog { Visible: true } modal && modal.IsVisibleInTree())
                scope = modal; // later in tree order means drawn on top
        }
        foreach (var node in scope.FindChildren("*", "Button", true, false))
        {
            if (node is Button { Disabled: false } button && button.IsVisibleInTree()
                && button.FocusMode == Control.FocusModeEnum.All)
            {
                button.GrabFocus();
                return;
            }
        }
    }
}
