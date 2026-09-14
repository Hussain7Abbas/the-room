using System;
using Godot;

namespace TheRoom.UI;

/// <summary>« ‹ Page X of Y › » plus a rows-per-page picker, for any paged list. Raises
/// PageRequested(page, pageSize); the owner fetches and then calls SetPage() with what came back,
/// so the bar always reflects the server's page, not the one that was asked for.</summary>
public partial class PaginationBar : HBoxContainer
{
    private static readonly int[] PageSizes = { 10, 20, 50 };

    public event Action<int, int>? PageRequested;

    private readonly Button _first = UiTheme.Button("«", "GhostButton");
    private readonly Button _prev = UiTheme.Button("‹  Prev", "GhostButton");
    private readonly Button _next = UiTheme.Button("Next  ›", "GhostButton");
    private readonly Button _last = UiTheme.Button("»", "GhostButton");
    private readonly Label _status = UiTheme.Label("", "Muted");
    private readonly OptionButton _size = new() { FocusMode = FocusModeEnum.None };
    private readonly string _itemNoun;

    private int _page = 1;
    private int _totalPages = 1;

    public int PageSize => PageSizes[Mathf.Max(0, _size.Selected)];

    public PaginationBar() : this("items") { }

    public PaginationBar(string itemNoun)
    {
        _itemNoun = itemNoun;
        AddThemeConstantOverride("separation", 6);

        foreach (var n in PageSizes)
            _size.AddItem($"{n} per page");
        _size.Selected = 0;
        _size.ItemSelected += _ => PageRequested?.Invoke(1, PageSize);

        _first.Pressed += () => PageRequested?.Invoke(1, PageSize);
        _prev.Pressed += () => PageRequested?.Invoke(Math.Max(1, _page - 1), PageSize);
        _next.Pressed += () => PageRequested?.Invoke(Math.Min(_totalPages, _page + 1), PageSize);
        _last.Pressed += () => PageRequested?.Invoke(_totalPages, PageSize);

        _first.TooltipText = "First page";
        _last.TooltipText = "Last page";

        AddChild(_size);
        AddChild(UiTheme.Spacer());
        AddChild(_status);
        AddChild(UiTheme.Spacer());
        AddChild(_first);
        AddChild(_prev);
        AddChild(_next);
        AddChild(_last);
        SetPage(1, 1, 0);
    }

    public void SetPage(int page, int totalPages, int totalItems)
    {
        _page = page;
        _totalPages = Math.Max(1, totalPages);
        _status.Text = totalItems == 0
            ? $"No {_itemNoun} yet"
            : $"Page {page} of {_totalPages}  ·  {totalItems} {_itemNoun}";
        SetBusy(false);
    }

    /// <summary>Disables every control while a page is loading, so double clicks can't race.</summary>
    public void SetBusy(bool busy)
    {
        _first.Disabled = busy || _page <= 1;
        _prev.Disabled = busy || _page <= 1;
        _next.Disabled = busy || _page >= _totalPages;
        _last.Disabled = busy || _page >= _totalPages;
        _size.Disabled = busy;
    }
}
