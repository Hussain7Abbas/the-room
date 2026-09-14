using System;
using Godot;

namespace TheRoom.UI;

/// <summary>
/// A centred panel over a dimmed screen, with a title, a Close button and Esc to close. Subclasses
/// fill <see cref="Body"/>. Add one as a child of any full-screen Control (main menu, in-game menu);
/// it frees itself on close.
/// </summary>
public partial class ModalDialog : Control
{
    public event Action? Closed;

    protected VBoxContainer Body { get; } = new();

    public ModalDialog() : this("", 520f) { }

    public ModalDialog(string title, float width)
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Stop; // clicks never reach the screen underneath
        Theme = UiTheme.Get();

        AddChild(new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f, 0.78f), AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        center.AddChild(panel);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 14);
        panel.AddChild(column);

        var header = new HBoxContainer();
        header.AddChild(UiTheme.Label(title, "Heading", fontSize: 26));
        header.AddChild(UiTheme.Spacer());
        header.AddChild(UiTheme.Button("Close", "GhostButton", Close));
        column.AddChild(header);

        Body.AddThemeConstantOverride("separation", 12);
        column.AddChild(Body);
    }

    public void Close()
    {
        Closed?.Invoke();
        QueueFree();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // The dialog sits deeper in the tree than whatever opened it, so it sees Esc first.
        if (!@event.IsActionPressed("ui_cancel") && !@event.IsActionPressed("pause_menu"))
            return;
        Close();
        GetViewport().SetInputAsHandled();
    }
}
