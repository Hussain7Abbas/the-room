using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// In-game overlay (Main.tscn), clients only:
///   - "Connecting to …" while the room hasn't answered yet, with Cancel and a timeout.
///   - Esc menu: the room's name and code to share, Resume, Leave room. The match keeps running
///     while it's open (it's online); your own character just stops taking input (Player checks IsOpen).
/// </summary>
public partial class GameMenu : CanvasLayer
{
    private const double ConnectTimeoutSeconds = 12.0;

    /// <summary>Player reads this to ignore movement, attacks and mouse look while the menu is up.</summary>
    public static bool IsOpen { get; private set; }

    private Control _root = null!;
    private Control _connecting = null!;
    private Label _connectingTitle = null!;
    private Control _menu = null!;
    private Label _roomName = null!;
    private LineEdit _roomCode = null!;
    private Control _codeRow = null!;
    private Button _leave = null!;
    private double _connectingFor;

    public override void _Ready()
    {
        if (Net.Instance.IsServer || DisplayServer.GetName() == "headless")
        {
            QueueFree();
            return;
        }

        Layer = 20;
        var root = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Get() };
        AddChild(root);
        _root = root;

        _connecting = BuildConnecting();
        root.AddChild(_connecting);
        _menu = BuildMenu();
        root.AddChild(_menu);
        SetOpen(false);
    }

    public override void _ExitTree() => IsOpen = false;

    private Control Dim()
    {
        var dim = new ColorRect { Color = new Color(0.03f, 0.03f, 0.05f, 0.72f), AnchorRight = 1, AnchorBottom = 1 };
        dim.MouseFilter = Control.MouseFilterEnum.Stop; // clicks on the dim area must not become attacks
        return dim;
    }

    private static CenterContainer Center(Control parent)
    {
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        parent.AddChild(center);
        return center;
    }

    private Control BuildConnecting()
    {
        var overlay = Dim();
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        Center(overlay).AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        _connectingTitle = UiTheme.Label("Connecting…", "Heading");
        box.AddChild(_connectingTitle);
        box.AddChild(UiTheme.Label("Waiting for the room to answer.", "Muted"));
        box.AddChild(UiTheme.Button("Cancel", onPressed: () => Net.Instance.Leave()));
        overlay.Visible = false;
        return overlay;
    }

    private Control BuildMenu()
    {
        var overlay = Dim();
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
        Center(overlay).AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        panel.AddChild(box);

        box.AddChild(UiTheme.Label("PAUSED", "Title", fontSize: 44));
        box.AddChild(UiTheme.Label("The match keeps going while this is open.", "Muted", fontSize: 13));

        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        box.AddChild(card);
        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 8);
        card.AddChild(info);
        _roomName = UiTheme.Label("", "Heading", fontSize: 19);
        info.AddChild(_roomName);

        var codeRow = new HBoxContainer();
        codeRow.AddThemeConstantOverride("separation", 8);
        codeRow.AddChild(UiTheme.Label("Room code", "Muted"));
        // A read-only field, not a label, so the code can be selected and copied.
        _roomCode = new LineEdit { Editable = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = HorizontalAlignment.Center };
        _roomCode.AddThemeFontSizeOverride("font_size", 20);
        codeRow.AddChild(_roomCode);
        codeRow.AddChild(UiTheme.Button("Copy", onPressed: () => DisplayServer.ClipboardSet(_roomCode.Text)));
        info.AddChild(codeRow);
        _codeRow = codeRow;

        box.AddChild(UiTheme.Button("Resume", "AccentButton", () => SetOpen(false)));
        box.AddChild(UiTheme.Button("Settings", onPressed: () => _root.AddChild(new SettingsDialog())));
        _leave = UiTheme.Button("Leave room", onPressed: () => Net.Instance.Leave());
        box.AddChild(_leave);
        box.AddChild(UiTheme.Label("Esc to close  ·  Tab for the scoreboard", "Muted", fontSize: 12));
        return overlay;
    }

    public override void _Process(double delta)
    {
        var connecting = Net.Instance.IsClient &&
            Multiplayer.MultiplayerPeer?.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting;

        if (connecting)
        {
            if (!_connecting.Visible)
            {
                _connectingFor = 0;
                _connectingTitle.Text = $"Connecting to {Net.Instance.RoomName ?? "the room"}…";
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            _connecting.Visible = true;
            _connectingFor += delta;
            if (_connectingFor > ConnectTimeoutSeconds)
                Net.Instance.Leave("The room didn't answer. It may have closed, or your network blocks UDP.");
        }
        else if (_connecting.Visible)
        {
            _connecting.Visible = false;
            if (!IsOpen)
                Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // pause_menu is Esc or the controller's Start/Options. ui_cancel (which includes the pad's
        // B/Circle, the dodge button) may only close the menu, never open it mid-fight.
        var toggle = @event.IsActionPressed("pause_menu") || (IsOpen && @event.IsActionPressed("ui_cancel"));
        if (!toggle || _connecting.Visible)
            return;
        SetOpen(!IsOpen);
        GetViewport().SetInputAsHandled();
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        _menu.Visible = open;
        if (open)
        {
            var practice = Net.Instance.IsOffline;
            _roomName.Text = practice ? "Practice" : Net.Instance.RoomName ?? "Direct connection";
            _roomCode.Text = Net.Instance.RoomCode ?? "";
            _codeRow.Visible = !practice && Net.Instance.RoomCode is not null;
            _leave.Text = practice ? "Back to menu" : "Leave room";
        }
        Input.MouseMode = open ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;
    }
}
