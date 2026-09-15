using System;
using System.Linq;
using Godot;
using TheRoom.UI;

namespace TheRoom.Core;

public enum InputDevice { Keyboard, Xbox, PlayStation }

/// <summary>
/// Autoload that tracks what the player is holding. Any button on any controller switches to it
/// (a PlayStation pad by its name, anything else counts as Xbox); any key or click switches back.
/// Settings → Controls and the HUD show that device's bindings and button icons.
///
/// The menus are mouse-first (their buttons don't take focus), so while a controller is active,
/// or after an arrow key on the keyboard, this makes buttons focusable and keeps one focused:
/// the D-pad/stick or arrows move, A/Cross or Enter press, B/Circle or Esc go back.
/// </summary>
public partial class InputDevices : Node
{
    public static InputDevice Current { get; private set; } = InputDevice.Keyboard;
    public static bool IsGamepad => Current != InputDevice.Keyboard;

    /// <summary>Menus are being driven without the mouse: by a controller, or by the keyboard
    /// after an arrow key (Enter presses, Esc goes back). A mouse click ends keyboard navigation.</summary>
    public static bool IsNavigating => IsGamepad || _keyboardNavigation;
    private static bool _keyboardNavigation;

    /// <summary>Raised when <see cref="Current"/> changes.</summary>
    public static event Action? Changed;

    private const float StickThreshold = 0.5f;
    private double _focusCheckIn;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        InputBindings.ApplySaved();
        EnsurePadMenuActions();
        GetTree().NodeAdded += OnNodeAdded;
    }

    /// <summary>A/Cross presses the focused button and B/Circle backs out, on every platform. They
    /// are in Godot's defaults, but only as "any device" events that some pads' drivers miss; adding
    /// them for each connected pad too means menus never ignore the face buttons.</summary>
    private static void EnsurePadMenuActions()
    {
        void Ensure(string action, JoyButton button)
        {
            var bound = InputMap.ActionGetEvents(action)
                .Any(e => e is InputEventJoypadButton b && b.ButtonIndex == button && b.Device == -1);
            if (!bound)
                InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button, Device = -1 });
        }
        Ensure("ui_accept", JoyButton.A);
        Ensure("ui_cancel", JoyButton.B);
        Ensure("ui_up", JoyButton.DpadUp);
        Ensure("ui_down", JoyButton.DpadDown);
        Ensure("ui_left", JoyButton.DpadLeft);
        Ensure("ui_right", JoyButton.DpadRight);
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
        var wasNavigating = IsNavigating;
        if (next is { } device && device != Current)
            SetCurrent(device, @event.Device);

        // Keyboard: an arrow key in a menu starts navigation (the first press just shows the
        // focus); a click hands the menus back to the mouse.
        var menus = Input.MouseMode != Input.MouseModeEnum.Captured;
        if (@event is InputEventKey { Pressed: true } && menus && !_keyboardNavigation && IsArrow(@event))
        {
            _keyboardNavigation = true;
            if (!wasNavigating)
            {
                RefreshNavigation(wasNavigating);
                GetViewport().SetInputAsHandled();
                return;
            }
        }
        else if (@event is InputEventMouseButton { Pressed: true } && _keyboardNavigation)
        {
            _keyboardNavigation = false;
            RefreshNavigation(true);
        }

        if (IsNavigating && menus)
        {
            var down = @event.IsActionPressed("ui_down");
            var vertical = down || @event.IsActionPressed("ui_up");
            if ((vertical || @event.IsActionPressed("ui_left") || @event.IsActionPressed("ui_right"))
                && Time.GetTicksMsec() - _lastNudge > 90)
            {
                _lastNudge = Time.GetTicksMsec();
                var before = GetViewport().GuiGetFocusOwner();
                Callable.From(() => AfterNavigate(before, vertical, down)).CallDeferred();
            }
        }
    }

    /// <summary>After a D-pad/stick move. Godot picks the next control by position, which in a
    /// dialog can jump to the menu behind it, or go nowhere (the dialog's "Close" has nothing
    /// directly below it). Then: stay in the dialog and step to the next control in reading
    /// order; at the very end, scroll the list so rows below the last button come into view.</summary>
    private void AfterNavigate(Control? before, bool vertical, bool down)
    {
        if (before is null || !IsInstanceValid(before))
            return;
        var now = GetViewport().GuiGetFocusOwner();
        var top = TopModal();
        var escaped = top is not null && top.IsAncestorOf(before) && (now is null || !top.IsAncestorOf(now));
        if (!escaped && now != before)
            return; // a normal move
        if (!vertical)
        {
            if (escaped)
                before.GrabFocus();
            return;
        }
        var scope = (Node?)top ?? before.GetParent();
        if (top is not null && StepInReadingOrder(top, before, down) is { } next)
        {
            next.GrabFocus();
            return;
        }
        if (escaped)
            before.GrabFocus();
        NudgeScroll(before, down, scope);
    }

    private static Control? StepInReadingOrder(Node scope, Control from, bool forward)
    {
        var list = new System.Collections.Generic.List<Control>();
        foreach (var node in scope.FindChildren("*", "Control", true, false))
        {
            if (node is Control c && c.FocusMode == Control.FocusModeEnum.All && c.IsVisibleInTree()
                && !(c is BaseButton { Disabled: true }))
                list.Add(c);
        }
        var index = list.IndexOf(from);
        if (index < 0)
            return null;
        var target = index + (forward ? 1 : -1);
        return target >= 0 && target < list.Count ? list[target] : null;
    }

    private ModalDialog? TopModal()
    {
        ModalDialog? top = null;
        foreach (var node in GetTree().Root.FindChildren("*", "Control", true, false))
        {
            if (node is ModalDialog modal && modal.IsVisibleInTree() && !modal.IsQueuedForDeletion())
                top = modal; // later in tree order means drawn on top
        }
        return top;
    }

    private ulong _lastNudge;

    private static bool IsArrow(InputEvent e) =>
        e.IsActionPressed("ui_up") || e.IsActionPressed("ui_down") || e.IsActionPressed("ui_left") || e.IsActionPressed("ui_right");

    /// <summary>After up/down: if the focus didn't move (nothing focusable further that way) but
    /// its scroll area has more content, scroll it, so rows below the last button (and plain rows
    /// with nothing to press) can still be brought into view with the pad.</summary>
    private static void NudgeScroll(Control before, bool down, Node? scope)
    {
        if (!IsInstanceValid(before))
            return;
        ScrollContainer? scroll = null;
        for (Node? n = before.GetParent(); n is not null && scroll is null; n = n.GetParent())
            scroll = n as ScrollContainer;
        // The focus may be outside the list (a dialog's Close button): use the dialog's list.
        if (scroll is null && scope is not null)
        {
            foreach (var node in scope.FindChildren("*", "ScrollContainer", true, false))
            {
                if (node is ScrollContainer candidate && candidate.IsVisibleInTree())
                {
                    scroll = candidate;
                    break;
                }
            }
        }
        if (scroll is not null)
            scroll.ScrollVertical += down ? 120 : -120;
    }

    public override void _Process(double delta)
    {
        if (!IsNavigating || Input.MouseMode == Input.MouseModeEnum.Captured)
            return;
        _focusCheckIn -= delta;
        if (_focusCheckIn > 0)
            return;
        _focusCheckIn = 0.1; // dialogs open and close under us; re-home the focus when it's lost
        var focused = GetViewport().GuiGetFocusOwner();
        var top = TopModal();
        // A dialog that just opened over a menu: the focus is still on the menu's button behind it
        // (the pause menu's "Settings", the main menu's "Refresh"). Move it into the dialog.
        if (focused is null || !focused.IsVisibleInTree() || (top is not null && !top.IsAncestorOf(focused)))
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
        var wasNavigating = IsNavigating;
        Current = device;
        GD.Print($"[Input] Now using {device}{(IsGamepad ? $" ({Input.GetJoyName(joypad)})" : "")}.");
        RefreshNavigation(wasNavigating);
        Changed?.Invoke();
    }

    /// <summary>Makes buttons focusable (or not) when navigation starts or stops, and puts the
    /// focus somewhere useful, or clears it so no focus ring is left behind for the mouse.</summary>
    private void RefreshNavigation(bool wasNavigating)
    {
        if (IsNavigating == wasNavigating)
            return;
        foreach (var node in GetTree().Root.FindChildren("*", "Control", true, false))
            SetFocusable(node);
        if (IsNavigating)
            _focusCheckIn = 0;
        else
            GetViewport().GuiReleaseFocus();
    }

    private static void OnNodeAdded(Node node) => SetFocusable(node);

    private static void SetFocusable(Node node)
    {
        // LinkButtons and the menus' option pickers keep their own mode (they open the browser or
        // a popup); everything else follows the device.
        if (node is Button button and not OptionButton)
            button.FocusMode = IsNavigating ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
        else if (node is Slider slider) // Settings → Sound: left/right on the D-pad moves it
            slider.FocusMode = IsNavigating ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
        else if (node is ScrollContainer scroll)
            scroll.FollowFocus = true; // moving the focus with the pad scrolls it into view
    }

    /// <summary>Focuses the first button of the top-most open dialog, or of the screen.</summary>
    private void FocusFirstButton()
    {
        Node scope = (Node?)TopModal() ?? GetTree().Root;
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
