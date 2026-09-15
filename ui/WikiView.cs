using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheRoom.UI;

/// <summary>
/// The in-game wiki reader (ui/Wiki.cs does the loading, rendering and search). A sidebar with
/// the search box and one tab per page; the page on the right with a Back button and a breadcrumb.
/// Links go to other pages or to a section of one; external links open in the browser. Typing two
/// or more letters in the search box lists matching sections; picking one jumps there.
/// Used as the main menu's Wiki page and inside WikiDialog (the pause menu).
/// Scrolling: mouse wheel, the scrollbar, PageUp/PageDown, or the right stick on a controller.
/// </summary>
public partial class WikiView : HBoxContainer
{
    private const string Crumb = "#8d8c99";

    private readonly LineEdit _search = new() { PlaceholderText = "Search the wiki", ClearButtonEnabled = true };
    private readonly VBoxContainer _tabs = new();
    private readonly ButtonGroup _tabGroup = new();
    private readonly Dictionary<string, Button> _tabButtons = new();
    private readonly RichTextLabel _reader = new();
    private readonly Label _breadcrumb;
    private readonly Button _back;
    private readonly Stack<(string Page, string Anchor)> _history = new();

    private string _page = Wiki.HomeId;
    private string _anchor = "";
    private string? _pendingAnchor;

    public WikiView()
    {
        AddThemeConstantOverride("separation", 16);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var side = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
        side.AddThemeConstantOverride("separation", 10);
        _search.TextChanged += OnSearchChanged;
        side.AddChild(_search);
        var tabScroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _tabs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tabs.AddThemeConstantOverride("separation", 2);
        tabScroll.AddChild(_tabs);
        side.AddChild(tabScroll);
        AddChild(side);

        var main = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        main.AddThemeConstantOverride("separation", 8);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);
        _back = UiTheme.Button("‹ Back", "GhostButton", GoBack);
        bar.AddChild(_back);
        _breadcrumb = UiTheme.Label("", "Muted", fontSize: 13);
        _breadcrumb.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(_breadcrumb);
        main.AddChild(bar);

        _reader.BbcodeEnabled = true;
        _reader.ScrollActive = true;
        _reader.SelectionEnabled = true;
        _reader.ContextMenuEnabled = true;
        _reader.SizeFlagsVertical = SizeFlags.ExpandFill;
        _reader.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _reader.AddThemeFontSizeOverride("normal_font_size", 16);
        _reader.AddThemeFontSizeOverride("bold_font_size", 16);
        _reader.AddThemeFontSizeOverride("italics_font_size", 16);
        _reader.AddThemeConstantOverride("line_separation", 5);
        _reader.AddThemeColorOverride("default_color", UiTheme.Text);
        _reader.MetaUnderlined = true;
        _reader.MetaClicked += OnLink;
        main.AddChild(_reader);
        AddChild(main);

        foreach (var page in Wiki.Pages)
        {
            var id = page.Id;
            var button = UiTheme.Button(page.Title, "NavButton", () => Open(id, "", remember: true));
            button.ToggleMode = true;
            button.ButtonGroup = _tabGroup;
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;
            button.TooltipText = page.Title;
            _tabButtons[id] = button;
            _tabs.AddChild(button);
        }
    }

    public override void _Ready()
    {
        Open(_page, "", remember: false);
        Resized += () => Render(); // screenshots fit the new width
    }

    /// <summary>Opens a page (and a section of it). <paramref name="remember"/> puts the current
    /// place on the Back stack.</summary>
    public void Open(string pageId, string anchor, bool remember)
    {
        if (Wiki.Get(pageId) is null)
            pageId = Wiki.HomeId;
        if (remember && (pageId != _page || anchor != _anchor || _search.Text.Length > 0))
            _history.Push((_page, _anchor));
        _page = pageId;
        _anchor = anchor;
        if (_search.Text.Length > 0)
        {
            _search.TextChanged -= OnSearchChanged;
            _search.Text = "";
            _search.TextChanged += OnSearchChanged;
        }
        Render();
    }

    private void Render()
    {
        var page = Wiki.Get(_page);
        if (page is null)
        {
            _reader.Text = "[color=#8d8c99]The wiki pages are missing from this build.[/color]";
            return;
        }
        if (_search.Text.Trim().Length >= 2)
        {
            ShowResults(_search.Text.Trim());
            return;
        }
        var width = (int)Mathf.Clamp(_reader.Size.X - 40f, 280f, 760f);
        _reader.Text = Wiki.ToBbcode(page, width);
        _reader.ScrollToLine(0);
        _pendingAnchor = _anchor.Length > 0 ? _anchor : null;
        if (_tabButtons.TryGetValue(_page, out var tab))
            tab.ButtonPressed = true;
        var section = page.Sections.FirstOrDefault(s => s.Anchor == _anchor);
        _breadcrumb.Text = _page == Wiki.HomeId
            ? "Wiki"
            : section is { Heading.Length: > 0 } ? $"Wiki  ›  {page.Title}  ›  {section.Heading}" : $"Wiki  ›  {page.Title}";
        _back.Disabled = _history.Count == 0;
    }

    private void ShowResults(string query)
    {
        _reader.Text = Wiki.ResultsBbcode(query, Wiki.Search(query));
        _reader.ScrollToLine(0);
        _pendingAnchor = null;
        _breadcrumb.Text = "Wiki  ›  Search";
        _back.Disabled = _history.Count == 0;
        if (_tabGroup.GetPressedButton() is { } pressed)
            pressed.ButtonPressed = false;
    }

    private void OnSearchChanged(string text)
    {
        if (text.Trim().Length >= 2)
            ShowResults(text.Trim());
        else
            Render();
    }

    private void OnLink(Variant meta)
    {
        var target = meta.AsString();
        var (pageId, anchor) = Wiki.ParseTarget(target, _page);
        if (pageId is null)
        {
            OS.ShellOpen(target); // an external link: the browser, not the game
            return;
        }
        Open(pageId, anchor, remember: true);
    }

    private void GoBack()
    {
        if (_history.Count == 0)
            return;
        var (page, anchor) = _history.Pop();
        _page = page;
        _anchor = anchor;
        Open(page, anchor, remember: false);
    }

    public override void _Process(double delta)
    {
        // Jump to a section once the text is laid out: find the heading in what the reader
        // shows and scroll to its line.
        if (_pendingAnchor is { } anchor && _reader.IsFinished())
        {
            _pendingAnchor = null;
            var heading = Wiki.Get(_page)?.Sections.FirstOrDefault(s => s.Anchor == anchor)?.Heading;
            if (!string.IsNullOrEmpty(heading))
            {
                var index = _reader.GetParsedText().IndexOf(heading, System.StringComparison.Ordinal);
                if (index >= 0)
                    _reader.ScrollToLine(_reader.GetCharacterLine(index));
            }
        }

        if (!IsVisibleInTree())
            return;
        // A controller reads with the right stick (the D-pad and A pick tabs).
        var stick = Input.GetAxis("look_up", "look_down");
        if (Mathf.Abs(stick) > 0.2f)
            _reader.GetVScrollBar().Value += stick * 900f * delta;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventKey { Pressed: true } key)
            return;
        var page = _reader.Size.Y * 0.85f;
        if (key.Keycode == Key.Pagedown)
            _reader.GetVScrollBar().Value += page;
        else if (key.Keycode == Key.Pageup)
            _reader.GetVScrollBar().Value -= page;
        else
            return;
        GetViewport().SetInputAsHandled();
    }
}

/// <summary>The wiki in a popup, for the pause menu.</summary>
public partial class WikiDialog : ModalDialog
{
    public WikiDialog() : base("Wiki", 1040f)
    {
        var view = new WikiView { CustomMinimumSize = new Vector2(0, 500) };
        Body.AddChild(view);
    }
}
