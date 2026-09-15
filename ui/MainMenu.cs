using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using TheRoom.Config;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// The first screen (project main scene). Pick a name and character, then:
///   Play: browse open rooms, join one, join a private room by code, or create a room.
///   Match history: every finished match on the server, paginated, with a full scoreboard per match.
///   Leaderboard: the season standings, paginated.
///   Practice: offline, alone in the room.
/// Rooms, history and leaderboard come from the lobby service (services/lobby) through LobbyApi.
/// Launching with --server or --connect skips straight to the game.
/// </summary>
public partial class MainMenu : Control
{
    private const double RoomRefreshSeconds = 5.0;
    private const double NewRoomStartTimeoutSeconds = 10.0;

    private enum Page { Play, History, Leaderboard, Wiki }

    private readonly Dictionary<Page, Button> _nav = new();
    private readonly Dictionary<Page, Control> _pages = new();
    private Page _current = Page.Play;

    private LineEdit _nameEdit = null!;
    private OptionButton _characterPick = null!;
    private Label _lobbyStatus = null!;
    private PanelContainer _toast = null!;
    private Label _toastText = null!;
    private readonly List<Button> _actionButtons = new();
    private double _toastHideIn;

    // Play
    private Tree _roomTree = null!;
    private Label _roomEmpty = null!;
    private Button _joinSelected = null!;
    private LineEdit _codeEdit = null!;
    private LineEdit _createName = null!;
    private OptionButton _createMode = null!;
    private CheckBox _createPrivate = null!;
    private List<LobbyApi.RoomInfo> _rooms = new();
    private double _roomRefreshIn;
    private bool _roomsLoading;

    // History
    private Tree _matchTree = null!;
    private Label _matchEmpty = null!;
    private PaginationBar _matchPager = null!;
    private CheckBox _onlyMine = null!;
    private VBoxContainer _detailBox = null!;
    private bool _historyLoaded;
    private long _detailRequest;

    // Leaderboard
    private Tree _boardTree = null!;
    private Label _boardEmpty = null!;
    private PaginationBar _boardPager = null!;
    private bool _boardLoaded;

    public override void _Ready()
    {
        // The saved window mode (Settings → Display), applied before anything else so scripted
        // --connect clients get it too. Headless servers and bots skip it inside.
        if (!Net.Instance.IsServer)
            GameSettings.ApplySavedDisplayMode();
        GameSettings.ApplySavedTextureQuality(GetViewport());

        // --server / --connect: no menu, straight into the game scene.
        if (Net.Instance.Mode != Net.SessionMode.None || Net.Instance.Pending is not null)
        {
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, Net.GameScenePath);
            return;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Theme = UiTheme.Get();
        BuildUi();
        LoadSettings();
        ShowPage(StartPage());
        _ = RefreshRooms(); // also sets the lobby status line, whichever page opened first
        OpenStartDialog();

        if (Net.Instance.LastDisconnectReason is { } reason)
        {
            ShowToast(reason, UiTheme.Danger);
            Net.Instance.LastDisconnectReason = null;
        }
    }

    public override void _Process(double delta)
    {
        if (_current == Page.Play)
        {
            _roomRefreshIn -= delta;
            if (_roomRefreshIn <= 0)
            {
                _roomRefreshIn = RoomRefreshSeconds;
                _ = RefreshRooms();
            }
        }

        if (_toastHideIn > 0)
        {
            _toastHideIn -= delta;
            if (_toastHideIn <= 0)
                _toast.Visible = false;
        }
    }

    // ------------------------------------------------------------------
    // Layout
    // ------------------------------------------------------------------

    private void BuildUi()
    {
        AddChild(new ColorRect { Color = UiTheme.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        // A dim red glow from the top-left corner, like light spilling under the room's door.
        var glow = new TextureRect
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            Texture = new GradientTexture2D
            {
                Width = 256,
                Height = 256,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.05f, 0f),
                FillTo = new Vector2(0.75f, 0.9f),
                Gradient = new Gradient
                {
                    Colors = new[] { new Color(UiTheme.Accent, 0.22f), new Color(UiTheme.Accent, 0f) },
                    Offsets = new[] { 0f, 1f },
                },
            },
        };
        AddChild(glow);

        var margin = new MarginContainer { AnchorRight = 1, AnchorBottom = 1 };
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_top", 26);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        root.AddChild(BuildHeader());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 22);
        root.AddChild(body);
        body.AddChild(BuildNav());

        var content = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddChild(content);
        _pages[Page.Play] = BuildPlayPage();
        _pages[Page.History] = BuildHistoryPage();
        _pages[Page.Leaderboard] = BuildLeaderboardPage();
        _pages[Page.Wiki] = new WikiView();
        foreach (var page in _pages.Values)
            content.AddChild(page);

        // Notices float over the layout at the bottom centre, so a tall page can never push
        // them off-screen (they used to sit in the column below the content, and did).
        _toast = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetTop = -74,
            OffsetBottom = -28,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Begin,
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false,
        };
        _toastText = UiTheme.Label("", fontSize: 15);
        _toast.AddChild(_toastText);
        AddChild(_toast);
    }

    private Control BuildHeader()
    {
        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 18);

        var brand = new VBoxContainer();
        brand.AddThemeConstantOverride("separation", -6);
        brand.AddChild(UiTheme.Label("THE ROOM", "Title"));
        brand.AddChild(UiTheme.Label("Knife-fight arena. Everyone in, one knife each.", "Muted"));
        header.AddChild(brand);
        header.AddChild(UiTheme.Spacer());

        _nameEdit = new LineEdit { PlaceholderText = "Your name", MaxLength = 16, CustomMinimumSize = new Vector2(210, 0) };
        _nameEdit.TextChanged += _ => SaveSettings();
        header.AddChild(Field("YOUR NAME", _nameEdit));

        _characterPick = new OptionButton { CustomMinimumSize = new Vector2(190, 0), FocusMode = FocusModeEnum.None };
        foreach (var def in CharacterRegistry.All.Values)
            _characterPick.AddItem(UiTheme.CharacterLabel(def.Id));
        _characterPick.ItemSelected += _ => SaveSettings();
        header.AddChild(Field("CHARACTER", _characterPick));

        return header;
    }

    private static Control Field(string caption, Control input)
    {
        var box = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkEnd };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(UiTheme.Label(caption, "Muted", fontSize: 12));
        box.AddChild(input);
        return box;
    }

    private Control BuildNav()
    {
        var nav = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
        nav.AddThemeConstantOverride("separation", 4);

        var group = new ButtonGroup();
        void Add(Page page, string text)
        {
            var button = UiTheme.Button(text, "NavButton", () => ShowPage(page));
            button.ToggleMode = true;
            button.ButtonGroup = group;
            button.Alignment = HorizontalAlignment.Left;
            _nav[page] = button;
            nav.AddChild(button);
        }

        Add(Page.Play, "Play");
        Add(Page.History, "Match history");
        Add(Page.Leaderboard, "Leaderboard");
        Add(Page.Wiki, "Wiki");
        nav.AddChild(new HSeparator());

        var practice = UiTheme.Button("Practice alone", "NavButton", OnPractice);
        practice.Alignment = HorizontalAlignment.Left;
        _actionButtons.Add(practice);
        nav.AddChild(practice);
        var settings = UiTheme.Button("Settings", "NavButton", () => AddChild(new SettingsDialog()));
        settings.Alignment = HorizontalAlignment.Left;
        nav.AddChild(settings);
        var about = UiTheme.Button("About", "NavButton", () => AddChild(new AboutDialog()));
        about.Alignment = HorizontalAlignment.Left;
        nav.AddChild(about);
        var quit = UiTheme.Button("Quit", "NavButton", () => GetTree().Quit());
        quit.Alignment = HorizontalAlignment.Left;
        nav.AddChild(quit);

        nav.AddChild(UiTheme.Spacer(horizontal: false));
        _lobbyStatus = UiTheme.Label("•  Connecting to lobby…", "Muted", fontSize: 13);
        _lobbyStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        nav.AddChild(_lobbyStatus);
        return nav;
    }

    private Control BuildPlayPage()
    {
        var page = new HBoxContainer();
        page.AddThemeConstantOverride("separation", 20);

        // Left: the room list.
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 2 };
        left.AddThemeConstantOverride("separation", 12);
        page.AddChild(left);

        var top = new HBoxContainer();
        top.AddChild(UiTheme.Label("Open rooms", "Heading"));
        top.AddChild(UiTheme.Spacer());
        top.AddChild(UiTheme.Label("updates every 5 s", "Muted", fontSize: 13));
        top.AddChild(UiTheme.Button("Refresh", "GhostButton", () => _ = RefreshRooms()));
        left.AddChild(top);

        _roomTree = MakeTree(("Room", 90, true), ("Code", 66, false), ("Mode", 90, false), ("Players", 66, false), ("Status", 80, false));
        _roomTree.ItemSelected += () => _joinSelected.Disabled = SelectedRoom() is not { IsFull: false };
        _roomTree.ItemActivated += () => { if (SelectedRoom() is { } room) Join(room); };
        _roomEmpty = EmptyLabel("Loading rooms…");
        left.AddChild(Stack(_roomTree, _roomEmpty));

        var bottom = new HBoxContainer();
        bottom.AddChild(UiTheme.Label("Double-click a room to join.", "Muted", fontSize: 13));
        bottom.AddChild(UiTheme.Spacer());
        _joinSelected = UiTheme.Button("Join room", "AccentButton", () => { if (SelectedRoom() is { } room) Join(room); });
        _joinSelected.Disabled = true;
        _joinSelected.CustomMinimumSize = new Vector2(140, 0);
        _actionButtons.Add(_joinSelected);
        bottom.AddChild(_joinSelected);
        left.AddChild(bottom);

        // Right: join by code, create.
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(290, 0) };
        right.AddThemeConstantOverride("separation", 14);
        page.AddChild(right);

        var codeCard = Card(right, "Join with a code", "Private rooms aren't listed. Ask the host for their 5-letter code.");
        var codeRow = new HBoxContainer();
        codeRow.AddThemeConstantOverride("separation", 8);
        _codeEdit = new LineEdit { PlaceholderText = "ABCDE", MaxLength = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _codeEdit.AddThemeFontSizeOverride("font_size", 18);
        _codeEdit.TextChanged += text =>
        {
            var upper = text.ToUpperInvariant();
            if (upper == text) return;
            var caret = _codeEdit.CaretColumn;
            _codeEdit.Text = upper;
            _codeEdit.CaretColumn = caret;
        };
        _codeEdit.TextSubmitted += submitted => _ = JoinByCode();
        codeRow.AddChild(_codeEdit);
        var codeJoin = UiTheme.Button("Join", onPressed: () => _ = JoinByCode());
        _actionButtons.Add(codeJoin);
        codeRow.AddChild(codeJoin);
        codeCard.AddChild(codeRow);

        var createCard = Card(right, "Create a room", "You get a code to share. Empty rooms close after 5 minutes.");
        _createName = new LineEdit { PlaceholderText = "Room name, e.g. Friday knives", MaxLength = 24 };
        _createName.TextSubmitted += submitted => _ = CreateRoom();
        createCard.AddChild(_createName);
        _createMode = new OptionButton { FocusMode = FocusModeEnum.None };
        _createMode.AddItem("Duel Pit  ·  6 to 10 players");
        _createMode.AddItem("Chaos  ·  12 to 20 players");
        createCard.AddChild(_createMode);
        _createPrivate = new CheckBox { Text = "Private: only people with the code", FocusMode = FocusModeEnum.None };
        createCard.AddChild(_createPrivate);
        var create = UiTheme.Button("Create & join", "AccentButton", () => _ = CreateRoom());
        _actionButtons.Add(create);
        createCard.AddChild(create);

        return page;
    }

    private Control BuildHistoryPage()
    {
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 12);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        top.AddChild(UiTheme.Label("Match history", "Heading"));
        top.AddChild(UiTheme.Spacer());
        _onlyMine = new CheckBox { Text = "Only my matches", FocusMode = FocusModeEnum.None };
        _onlyMine.Toggled += on => _ = LoadMatches(1, _matchPager.PageSize);
        top.AddChild(_onlyMine);
        top.AddChild(UiTheme.Button("Refresh", "GhostButton", () => _ = LoadMatches(1, _matchPager.PageSize)));
        page.AddChild(top);

        var split = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 16);
        page.AddChild(split);

        _matchTree = MakeTree(("When", 100, false), ("Room", 84, true), ("Winner", 100, true), ("Length", 56, false), ("Players", 62, false));
        _matchTree.ItemSelected += () =>
        {
            if (_matchTree.GetSelected()?.GetMetadata(0).AsInt64() is { } id)
                _ = LoadMatchDetail(id);
        };
        _matchEmpty = EmptyLabel("Loading matches…");
        var stack = Stack(_matchTree, _matchEmpty);
        stack.SizeFlagsStretchRatio = 3;
        split.AddChild(stack);

        var detailCard = new PanelContainer { ThemeTypeVariation = "Card", CustomMinimumSize = new Vector2(285, 0) };
        split.AddChild(detailCard);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        detailCard.AddChild(scroll);
        _detailBox = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _detailBox.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(_detailBox);
        ShowDetailPlaceholder("Select a match to see its full scoreboard.");

        _matchPager = new PaginationBar("matches");
        _matchPager.PageRequested += (p, size) => _ = LoadMatches(p, size);
        page.AddChild(_matchPager);
        return page;
    }

    private Control BuildLeaderboardPage()
    {
        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 12);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        top.AddChild(UiTheme.Label("Season leaderboard", "Heading"));
        top.AddChild(UiTheme.Spacer());
        top.AddChild(UiTheme.Label("Ranked by wins, then total score", "Muted", fontSize: 13));
        top.AddChild(UiTheme.Button("Refresh", "GhostButton", () => _ = LoadBoard(1, _boardPager.PageSize)));
        page.AddChild(top);

        _boardTree = MakeTree(("#", 50, false), ("Player", 180, true), ("Matches", 80, false), ("Wins", 70, false),
            ("Win %", 70, false), ("K / D", 70, false), ("Kills", 70, false), ("Best", 70, false));
        _boardEmpty = EmptyLabel("Loading leaderboard…");
        page.AddChild(Stack(_boardTree, _boardEmpty));

        _boardPager = new PaginationBar("players");
        _boardPager.PageRequested += (p, size) => _ = LoadBoard(p, size);
        page.AddChild(_boardPager);
        return page;
    }

    /// <summary>A raised card with a title and blurb. Returns its inner column to add controls to.</summary>
    private static VBoxContainer Card(Container parent, string title, string blurb)
    {
        var card = new PanelContainer { ThemeTypeVariation = "Card" };
        parent.AddChild(card);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        card.AddChild(box);
        box.AddChild(UiTheme.Label(title, "Heading", fontSize: 19));
        var text = UiTheme.Label(blurb, "Muted", fontSize: 13);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(text);
        return box;
    }

    private static Tree MakeTree(params (string Title, int MinWidth, bool Expand)[] columns)
    {
        var tree = new Tree
        {
            Columns = columns.Length,
            HideRoot = true,
            ColumnTitlesVisible = true,
            SelectMode = Tree.SelectModeEnum.Row,
            AnchorRight = 1,
            AnchorBottom = 1,
            FocusMode = FocusModeEnum.Click,
        };
        for (var i = 0; i < columns.Length; i++)
        {
            tree.SetColumnTitle(i, columns[i].Title);
            tree.SetColumnExpand(i, columns[i].Expand);
            tree.SetColumnCustomMinimumWidth(i, columns[i].MinWidth);
            tree.SetColumnClipContent(i, true);
            tree.SetColumnTitleAlignment(i, i == 0 || columns[i].Expand ? HorizontalAlignment.Left : HorizontalAlignment.Center);
        }
        return tree;
    }

    private static Label EmptyLabel(string text) => new()
    {
        Text = text,
        AnchorRight = 1,
        AnchorBottom = 1,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        AutowrapMode = TextServer.AutowrapMode.WordSmart,
        ThemeTypeVariation = "Muted",
        MouseFilter = MouseFilterEnum.Ignore,
    };

    /// <summary>A table with a message layered on top (loading, empty, error).</summary>
    private static Control Stack(Tree tree, Label overlay)
    {
        var stack = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stack.AddChild(tree);
        stack.AddChild(overlay);
        return stack;
    }

    private static TreeItem AddRow(Tree tree, params string[] cells)
    {
        var root = tree.GetRoot() ?? tree.CreateItem();
        var item = tree.CreateItem(root);
        for (var i = 0; i < cells.Length; i++)
        {
            item.SetText(i, cells[i]);
            if (i > 0 && !tree.IsColumnExpanding(i))
                item.SetTextAlignment(i, HorizontalAlignment.Center);
        }
        return item;
    }

    /// <summary>--menu-page=history|leaderboard opens the menu on that page (screenshots, testing).</summary>
    /// <summary>--menu-open=settings|controls|about opens that dialog on top (screenshots, testing).</summary>
    private void OpenStartDialog()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--menu-open=settings") AddChild(new SettingsDialog());
            if (arg == "--menu-open=controls") AddChild(new SettingsDialog(SettingsDialog.Tab.Controls));
            if (arg == "--menu-open=graphics") AddChild(new SettingsDialog(SettingsDialog.Tab.Graphics));
            if (arg == "--menu-open=sound") AddChild(new SettingsDialog(SettingsDialog.Tab.Sound));
            if (arg == "--menu-open=about") AddChild(new AboutDialog());
        }
    }

    private static Page StartPage()
    {
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--menu-page=history") return Page.History;
            if (arg == "--menu-page=leaderboard") return Page.Leaderboard;
            if (arg == "--menu-page=wiki") return Page.Wiki;
        }
        return Page.Play;
    }

    private void ShowPage(Page page)
    {
        _current = page;
        foreach (var (p, control) in _pages)
            control.Visible = p == page;
        _nav[page].ButtonPressed = true;

        switch (page)
        {
            case Page.Play:
                _roomRefreshIn = 0;
                break;
            case Page.History when !_historyLoaded:
                _ = LoadMatches(1, _matchPager.PageSize);
                break;
            case Page.Leaderboard when !_boardLoaded:
                _ = LoadBoard(1, _boardPager.PageSize);
                break;
        }
    }

    // ------------------------------------------------------------------
    // Play
    // ------------------------------------------------------------------

    private LobbyApi.RoomInfo? SelectedRoom()
    {
        var index = _roomTree.GetSelected()?.GetMetadata(0).AsInt32() ?? -1;
        return index >= 0 && index < _rooms.Count ? _rooms[index] : null;
    }

    private async Task RefreshRooms()
    {
        if (_roomsLoading)
            return;
        _roomsLoading = true;
        var selectedCode = SelectedRoom()?.Code;
        var result = await LobbyApi.ListRooms();
        _roomsLoading = false;
        if (!IsInsideTree())
            return;

        _roomTree.Clear();
        if (!result.Ok)
        {
            _rooms = new();
            SetLobbyStatus(false, "Lobby offline");
            _roomEmpty.Text = $"Can't reach the lobby.\n{result.Error}\n\nPractice alone still works.";
            _roomEmpty.Visible = true;
            _joinSelected.Disabled = true;
            return;
        }

        _rooms = result.Value!;
        SetLobbyStatus(true, $"Lobby online  ·  {_rooms.Count} room{(_rooms.Count == 1 ? "" : "s")}, {_rooms.Sum(r => r.Players)} playing");
        _roomEmpty.Visible = _rooms.Count == 0;
        _roomEmpty.Text = "No open rooms right now.\nCreate one on the right, or practice alone.";

        for (var i = 0; i < _rooms.Count; i++)
        {
            var room = _rooms[i];
            var item = AddRow(_roomTree, room.Name, room.Code, UiTheme.ModeLabel(room.Mode), $"{room.Players} / {room.MaxPlayers}", Capitalize(room.State));
            item.SetMetadata(0, i);
            item.SetCustomColor(1, UiTheme.Muted);
            item.SetCustomColor(4, room.State switch
            {
                "playing" => UiTheme.Success,
                "last call" => UiTheme.Gold,
                _ => UiTheme.Muted,
            });
            if (room.IsFull)
            {
                item.SetText(3, "Full");
                item.SetCustomColor(3, UiTheme.Danger);
            }
            if (room.Code == selectedCode)
                item.Select(0);
        }
        _joinSelected.Disabled = SelectedRoom() is not { IsFull: false };
    }

    private void SetLobbyStatus(bool online, string text)
    {
        _lobbyStatus.Text = $"•  {text}";
        _lobbyStatus.AddThemeColorOverride("font_color", online ? UiTheme.Success : UiTheme.Danger);
    }

    private bool ReadyToPlay()
    {
        if (_nameEdit.Text.Trim().Length == 0)
        {
            ShowToast("Pick a name first. It's what everyone sees in the killfeed.", UiTheme.Danger);
            _nameEdit.GrabFocus();
            return false;
        }
        SaveSettings();
        Net.Instance.Configure(_nameEdit.Text.Trim(), SelectedCharacterId());
        return true;
    }

    private void Join(LobbyApi.RoomInfo room)
    {
        if (!ReadyToPlay())
            return;
        if (room.IsFull)
        {
            ShowToast($"{room.Name} is full ({room.MaxPlayers} players).", UiTheme.Danger);
            return;
        }
        SetBusy(true);
        ShowToast($"Joining {room.Name}…", UiTheme.Text);
        Net.Instance.Join(room.Host, room.Port, room.Code, room.Name);
    }

    private async Task JoinByCode()
    {
        var code = _codeEdit.Text.Trim().ToUpperInvariant();
        if (code.Length != 5)
        {
            ShowToast("Room codes are 5 characters, like K7QXP.", UiTheme.Danger);
            return;
        }
        if (!ReadyToPlay())
            return;

        SetBusy(true);
        var result = await LobbyApi.GetRoom(code);
        if (!IsInsideTree())
            return;
        SetBusy(false);
        if (!result.Ok)
        {
            ShowToast(result.Error!, UiTheme.Danger);
            return;
        }
        Join(result.Value!);
    }

    private async Task CreateRoom()
    {
        if (!ReadyToPlay())
            return;

        SetBusy(true);
        ShowToast("Creating your room…", UiTheme.Text);
        var mode = _createMode.Selected == 1 ? "chaos" : "duelpit";
        var created = await LobbyApi.CreateRoom(_createName.Text, mode, _createPrivate.ButtonPressed);
        if (!IsInsideTree())
            return;
        if (!created.Ok)
        {
            SetBusy(false);
            ShowToast(created.Error!, UiTheme.Danger);
            return;
        }

        // The room's game server takes a moment to boot. Wait for its first heartbeat so the
        // connection doesn't race it.
        var room = created.Value!;
        var waited = 0.0;
        while (room.State == "starting" && waited < NewRoomStartTimeoutSeconds)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            waited += 0.5;
            if (!IsInsideTree())
                return;
            var poll = await LobbyApi.GetRoom(room.Code);
            if (poll.Ok)
                room = poll.Value!;
        }

        SetBusy(false);
        if (room.IsPrivate)
            DisplayServer.ClipboardSet(room.Code);
        ShowToast(room.IsPrivate ? $"Room {room.Code} created. Code copied to your clipboard." : $"Room {room.Code} created.", UiTheme.Success);
        Join(room);
    }

    private void OnPractice()
    {
        if (!ReadyToPlay())
            return;
        SetBusy(true);
        Net.Instance.Practice();
    }

    // ------------------------------------------------------------------
    // Match history
    // ------------------------------------------------------------------

    private async Task LoadMatches(int page, int pageSize)
    {
        _historyLoaded = true;
        _matchPager.SetBusy(true);
        _matchEmpty.Text = "Loading matches…";
        _matchEmpty.Visible = true;
        var player = _onlyMine.ButtonPressed ? _nameEdit.Text.Trim() : null;
        var result = await LobbyApi.ListMatches(page, pageSize, player);
        if (!IsInsideTree())
            return;

        _matchTree.Clear();
        ShowDetailPlaceholder("Select a match to see its full scoreboard.");
        if (!result.Ok)
        {
            _matchEmpty.Text = $"Couldn't load match history.\n{result.Error}";
            _matchPager.SetPage(page, page, 0);
            return;
        }

        var data = result.Value!;
        _matchEmpty.Visible = data.Items.Count == 0;
        _matchEmpty.Text = player is null
            ? "No matches finished yet.\nPlay one: every finished match lands here."
            : $"No matches for \"{player}\" yet.";

        var me = _nameEdit.Text.Trim();
        foreach (var match in data.Items)
        {
            var winner = match.MvpName is null ? "—" : $"{match.MvpName}  ({match.MvpScore})";
            var item = AddRow(_matchTree, UiTheme.TimeAgo(match.EndedAt), match.RoomName, winner,
                UiTheme.Duration(match.DurationSeconds), match.PlayerCount.ToString());
            item.SetMetadata(0, match.Id);
            item.SetCustomColor(0, UiTheme.Muted);
            item.SetCustomColor(2, string.Equals(match.MvpName, me, StringComparison.OrdinalIgnoreCase) ? UiTheme.Gold : UiTheme.Text);
            item.SetTooltipText(1, $"{UiTheme.ModeLabel(match.Mode)}  ·  room {match.RoomCode}");
            item.SetTooltipText(0, match.EndedAt.ToLocalTime().ToString("ddd d MMM yyyy, HH:mm"));
        }
        _matchPager.SetPage(data.Page, data.TotalPages, data.TotalItems);

        // Open the newest match on the page, so the details panel is never just a placeholder.
        if (_matchTree.GetRoot()?.GetFirstChild() is { } first)
        {
            first.Select(0);
            _ = LoadMatchDetail(first.GetMetadata(0).AsInt64());
        }
    }

    private async Task LoadMatchDetail(long id)
    {
        var request = ++_detailRequest;
        ShowDetailPlaceholder("Loading…");
        var result = await LobbyApi.GetMatch(id);
        if (!IsInsideTree() || request != _detailRequest)
            return; // a newer selection won
        if (!result.Ok)
        {
            ShowDetailPlaceholder(result.Error!);
            return;
        }

        var match = result.Value!;
        ClearDetail();
        _detailBox.AddChild(UiTheme.Label(match.RoomName, "Heading"));
        _detailBox.AddChild(UiTheme.Label(
            $"{UiTheme.ModeLabel(match.Mode)}  ·  {UiTheme.Duration(match.DurationSeconds)}  ·  {match.EndedAt.ToLocalTime():d MMM, HH:mm}  ·  room {match.RoomCode}",
            "Muted", fontSize: 13));
        if (match.MvpName is not null)
            _detailBox.AddChild(UiTheme.Label($"MVP  {match.MvpName}  ·  {match.MvpScore} pts", fontSize: 17, color: UiTheme.Gold));

        var grid = new GridContainer { Columns = 6 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var heading in new[] { "#", "Player", "Char.", "Pts", "K", "D" })
            grid.AddChild(UiTheme.Label(heading, "Muted", fontSize: 12));

        var me = _nameEdit.Text.Trim();
        foreach (var p in match.Players)
        {
            var isMe = string.Equals(p.Name, me, StringComparison.OrdinalIgnoreCase);
            var color = p.Rank == 1 ? UiTheme.Gold : isMe ? UiTheme.AccentHover : UiTheme.Text;
            grid.AddChild(UiTheme.Label(p.Rank.ToString(), color: color));
            var name = UiTheme.Label(p.Name, color: color);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.ClipText = true;
            grid.AddChild(name);
            grid.AddChild(UiTheme.Label(UiTheme.CharacterLabel(p.Character), "Muted", fontSize: 13));
            grid.AddChild(UiTheme.Label(p.Score.ToString(), color: color));
            grid.AddChild(UiTheme.Label(p.Kills.ToString()));
            grid.AddChild(UiTheme.Label(p.Deaths.ToString()));
        }
        _detailBox.AddChild(grid);

        if (match.Awards.Count > 0)
        {
            _detailBox.AddChild(new HSeparator());
            _detailBox.AddChild(UiTheme.Label("AWARDS", "Muted", fontSize: 12));
            foreach (var award in match.Awards)
            {
                var line = UiTheme.Label(award, fontSize: 14);
                line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _detailBox.AddChild(line);
            }
        }
    }

    private void ClearDetail()
    {
        foreach (var child in _detailBox.GetChildren())
            child.QueueFree();
    }

    private void ShowDetailPlaceholder(string text)
    {
        ClearDetail();
        var label = UiTheme.Label(text, "Muted");
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _detailBox.AddChild(label);
    }

    // ------------------------------------------------------------------
    // Leaderboard
    // ------------------------------------------------------------------

    private async Task LoadBoard(int page, int pageSize)
    {
        _boardLoaded = true;
        _boardPager.SetBusy(true);
        _boardEmpty.Text = "Loading leaderboard…";
        _boardEmpty.Visible = true;
        var result = await LobbyApi.Leaderboard(page, pageSize);
        if (!IsInsideTree())
            return;

        _boardTree.Clear();
        if (!result.Ok)
        {
            _boardEmpty.Text = $"Couldn't load the leaderboard.\n{result.Error}";
            _boardPager.SetPage(page, page, 0);
            return;
        }

        var data = result.Value!;
        _boardEmpty.Visible = data.Items.Count == 0;
        _boardEmpty.Text = "Nobody on the board yet. Finish a match to get on it.";

        var me = _nameEdit.Text.Trim();
        foreach (var row in data.Items)
        {
            var winRate = row.Matches == 0 ? 0 : 100.0 * row.Wins / row.Matches;
            var kd = row.Deaths == 0 ? row.Kills : row.Kills / (double)row.Deaths;
            var item = AddRow(_boardTree, row.Rank.ToString(), row.Name, row.Matches.ToString(), row.Wins.ToString(),
                $"{winRate:0}%", $"{kd:0.00}", row.Kills.ToString(), row.BestScore.ToString());
            if (row.Rank <= 3)
                item.SetCustomColor(0, UiTheme.Gold);
            if (string.Equals(row.Name, me, StringComparison.OrdinalIgnoreCase))
            {
                for (var c = 0; c < _boardTree.Columns; c++)
                    item.SetCustomBgColor(c, new Color(UiTheme.Accent, 0.16f));
            }
        }
        _boardPager.SetPage(data.Page, data.TotalPages, data.TotalItems);
    }

    // ------------------------------------------------------------------
    // Settings, feedback
    // ------------------------------------------------------------------

    private string? SelectedCharacterId()
    {
        var ids = CharacterRegistry.All.Keys.ToList();
        return _characterPick.Selected >= 0 && _characterPick.Selected < ids.Count ? ids[_characterPick.Selected] : null;
    }

    private void LoadSettings()
    {
        var config = GameSettings.Load();
        _nameEdit.Text = config.GetValue("player", "name", Net.Instance.LocalPlayerName).AsString();
        var character = config.GetValue("player", "character", Net.Instance.ChosenCharacterId ?? "").AsString();
        var index = CharacterRegistry.All.Keys.ToList().IndexOf(character);
        _characterPick.Selected = Math.Max(0, index);
    }

    private void SaveSettings()
    {
        // Load first: the same file holds the display mode (GameSettings), which a fresh
        // ConfigFile would wipe.
        var config = GameSettings.Load();
        config.SetValue("player", "name", _nameEdit.Text.Trim());
        config.SetValue("player", "character", SelectedCharacterId() ?? "");
        GameSettings.Save(config);
    }

    private void ShowToast(string text, Color color)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color("#1d1d25"),
            BorderColor = color,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
            ShadowColor = new Color(0, 0, 0, 0.5f),
            ShadowSize = 12,
        };
        style.SetCornerRadiusAll(8);
        style.SetBorderWidthAll(1);
        style.BorderWidthLeft = 4;
        _toast.AddThemeStyleboxOverride("panel", style);
        _toastText.Text = text;
        _toast.Visible = true;
        _toastHideIn = color == UiTheme.Danger ? 12 : 5;
    }

    private void SetBusy(bool busy)
    {
        foreach (var button in _actionButtons)
            button.Disabled = busy;
        if (!busy)
            _joinSelected.Disabled = SelectedRoom() is not { IsFull: false };
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
