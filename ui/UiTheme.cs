using Godot;

namespace TheRoom.UI;

/// <summary>
/// The one theme every menu screen uses: near-black panels, the Room's red as the accent, gold
/// for winners. Built in code so it stays in one reviewable place. Theme type variations
/// (AccentButton, NavButton, GhostButton, Card, Muted, Heading, Title) are picked per control
/// with <c>ThemeTypeVariation</c>.
/// </summary>
public static class UiTheme
{
    public static readonly Color Background = new("#0f0f14");
    public static readonly Color Panel = new("#191920");
    public static readonly Color PanelRaised = new("#22222b");
    public static readonly Color Border = new("#2e2e39");
    public static readonly Color Accent = new("#c8463d");
    public static readonly Color AccentHover = new("#e0564c");
    public static readonly Color AccentPressed = new("#a8362f");
    public static readonly Color Gold = new("#f2c14e");
    public static readonly Color Text = new("#ecebe8");
    public static readonly Color Muted = new("#8d8c99");
    public static readonly Color Success = new("#62b872");
    public static readonly Color Danger = new("#e06a5f");

    private static Theme? _theme;

    public static Theme Get() => _theme ??= Build();

    /// <summary>Bold condensed display face for titles. Falls back through common system fonts,
    /// so it looks right on Windows and macOS without shipping a font file.</summary>
    public static Font DisplayFont { get; } = new SystemFont
    {
        FontNames = new[] { "Impact", "Haettenschweiler", "Arial Black", "Helvetica Neue", "sans-serif" },
        FontWeight = 800,
    };

    private static StyleBoxFlat Box(Color bg, int radius = 6, Color? border = null, int borderWidth = 0, int padX = 14, int padY = 8)
    {
        var box = new StyleBoxFlat
        {
            BgColor = bg,
            BorderColor = border ?? bg,
            ContentMarginLeft = padX,
            ContentMarginRight = padX,
            ContentMarginTop = padY,
            ContentMarginBottom = padY,
        };
        box.SetCornerRadiusAll(radius);
        box.SetBorderWidthAll(borderWidth);
        return box;
    }

    private static Theme Build()
    {
        var t = new Theme { DefaultFontSize = 16 };

        // Plain buttons: raised panel with a border.
        t.SetStylebox("normal", "Button", Box(PanelRaised, border: Border, borderWidth: 1));
        t.SetStylebox("hover", "Button", Box(new Color("#2b2b36"), border: Muted, borderWidth: 1));
        t.SetStylebox("pressed", "Button", Box(new Color("#1a1a21"), border: Accent, borderWidth: 1));
        t.SetStylebox("disabled", "Button", Box(new Color("#18181e"), border: new Color("#24242c"), borderWidth: 1));
        t.SetStylebox("focus", "Button", Box(Colors.Transparent, border: Accent, borderWidth: 1));
        t.SetColor("font_color", "Button", Text);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_pressed_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color("#55545f"));

        // Primary action: filled red.
        t.SetTypeVariation("AccentButton", "Button");
        t.SetStylebox("normal", "AccentButton", Box(Accent));
        t.SetStylebox("hover", "AccentButton", Box(AccentHover));
        t.SetStylebox("pressed", "AccentButton", Box(AccentPressed));
        t.SetStylebox("disabled", "AccentButton", Box(new Color("#4a2826")));
        t.SetColor("font_disabled_color", "AccentButton", new Color("#8a6b68"));

        // Left navigation: flat, left aligned, red bar when selected.
        t.SetTypeVariation("NavButton", "Button");
        var navNormal = Box(Colors.Transparent, radius: 4, padX: 16, padY: 11);
        var navSelected = Box(PanelRaised, radius: 4, padX: 16, padY: 11);
        navSelected.BorderColor = Accent;
        navSelected.BorderWidthLeft = 3;
        t.SetStylebox("normal", "NavButton", navNormal);
        t.SetStylebox("hover", "NavButton", Box(new Color("#1d1d25"), radius: 4, padX: 16, padY: 11));
        t.SetStylebox("pressed", "NavButton", navSelected);
        t.SetStylebox("hover_pressed", "NavButton", navSelected);
        t.SetStylebox("focus", "NavButton", new StyleBoxEmpty());
        t.SetColor("font_color", "NavButton", Muted);
        t.SetColor("font_pressed_color", "NavButton", Text);
        t.SetColor("font_hover_pressed_color", "NavButton", Text);
        t.SetConstant("h_separation", "NavButton", 10);
        t.SetFontSize("font_size", "NavButton", 17);

        // Low-emphasis (pagination arrows, refresh).
        t.SetTypeVariation("GhostButton", "Button");
        t.SetStylebox("normal", "GhostButton", Box(Colors.Transparent, padX: 10, padY: 6));
        t.SetStylebox("hover", "GhostButton", Box(PanelRaised, padX: 10, padY: 6));
        t.SetStylebox("pressed", "GhostButton", Box(new Color("#1a1a21"), padX: 10, padY: 6));
        t.SetStylebox("disabled", "GhostButton", Box(Colors.Transparent, padX: 10, padY: 6));

        // Tabs inside dialogs: flat, with a red underline on the selected one.
        t.SetTypeVariation("TabButton", "Button");
        var tabNormal = Box(Colors.Transparent, radius: 0, padX: 16, padY: 8);
        var tabSelected = Box(Colors.Transparent, radius: 0, padX: 16, padY: 8);
        tabSelected.BorderColor = Accent;
        tabSelected.BorderWidthBottom = 3;
        t.SetStylebox("normal", "TabButton", tabNormal);
        t.SetStylebox("hover", "TabButton", Box(new Color(1, 1, 1, 0.04f), radius: 0, padX: 16, padY: 8));
        t.SetStylebox("pressed", "TabButton", tabSelected);
        t.SetStylebox("hover_pressed", "TabButton", tabSelected);
        t.SetStylebox("focus", "TabButton", new StyleBoxEmpty());
        t.SetColor("font_color", "TabButton", Muted);
        t.SetColor("font_pressed_color", "TabButton", Text);
        t.SetColor("font_hover_pressed_color", "TabButton", Text);
        t.SetFontSize("font_size", "TabButton", 17);

        // A key on a keyboard, for the controls list.
        t.SetTypeVariation("KeyCap", "PanelContainer");
        var keyCap = Box(new Color("#121218"), radius: 5, border: new Color("#44444f"), borderWidth: 1, padX: 10, padY: 3);
        keyCap.BorderWidthBottom = 3;
        t.SetStylebox("panel", "KeyCap", keyCap);

        t.SetColor("font_color", "LinkButton", AccentHover);
        t.SetColor("font_hover_color", "LinkButton", Gold);
        t.SetColor("font_pressed_color", "LinkButton", Gold);

        // Panels.
        t.SetStylebox("panel", "PanelContainer", Box(Panel, radius: 10, border: Border, borderWidth: 1, padX: 20, padY: 18));
        t.SetTypeVariation("Card", "PanelContainer");
        t.SetStylebox("panel", "Card", Box(PanelRaised, radius: 8, border: Border, borderWidth: 1, padX: 16, padY: 14));

        // Text.
        t.SetColor("font_color", "Label", Text);
        t.SetTypeVariation("Muted", "Label");
        t.SetColor("font_color", "Muted", Muted);
        t.SetFontSize("font_size", "Muted", 14);
        t.SetTypeVariation("Heading", "Label");
        t.SetFontSize("font_size", "Heading", 22);
        t.SetFont("font", "Heading", DisplayFont);
        t.SetTypeVariation("Title", "Label");
        t.SetFont("font", "Title", DisplayFont);
        t.SetFontSize("font_size", "Title", 64);
        t.SetColor("font_color", "Title", Accent);

        // Inputs.
        t.SetStylebox("normal", "LineEdit", Box(new Color("#121218"), border: Border, borderWidth: 1, padX: 12, padY: 8));
        t.SetStylebox("focus", "LineEdit", Box(Colors.Transparent, border: Accent, borderWidth: 1, padX: 12, padY: 8));
        t.SetColor("font_color", "LineEdit", Text);
        t.SetColor("font_placeholder_color", "LineEdit", new Color("#5d5c68"));
        t.SetColor("caret_color", "LineEdit", Accent);
        t.SetColor("selection_color", "LineEdit", new Color(Accent, 0.4f));
        t.SetColor("font_color", "CheckBox", Text);
        t.SetColor("font_hover_color", "CheckBox", Colors.White);
        t.SetStylebox("normal", "CheckBox", new StyleBoxEmpty());
        t.SetStylebox("hover", "CheckBox", new StyleBoxEmpty());
        t.SetStylebox("pressed", "CheckBox", new StyleBoxEmpty());
        t.SetStylebox("hover_pressed", "CheckBox", new StyleBoxEmpty());
        t.SetStylebox("focus", "CheckBox", new StyleBoxEmpty());

        // Popups (OptionButton menus).
        t.SetStylebox("panel", "PopupMenu", Box(PanelRaised, radius: 6, border: Border, borderWidth: 1, padX: 6, padY: 6));
        t.SetStylebox("hover", "PopupMenu", Box(Accent, radius: 4, padX: 8, padY: 4));
        t.SetColor("font_color", "PopupMenu", Text);
        t.SetColor("font_hover_color", "PopupMenu", Colors.White);

        // Tables (Tree).
        t.SetStylebox("panel", "Tree", Box(new Color("#131319"), radius: 8, border: Border, borderWidth: 1, padX: 4, padY: 4));
        t.SetStylebox("focus", "Tree", new StyleBoxEmpty());
        t.SetStylebox("selected", "Tree", Box(new Color(Accent, 0.28f), radius: 4));
        t.SetStylebox("selected_focus", "Tree", Box(new Color(Accent, 0.36f), radius: 4));
        t.SetStylebox("hovered", "Tree", Box(new Color(1, 1, 1, 0.04f), radius: 4));
        t.SetStylebox("title_button_normal", "Tree", Box(new Color("#1b1b22"), radius: 0, padX: 10, padY: 8));
        t.SetStylebox("title_button_hover", "Tree", Box(new Color("#1b1b22"), radius: 0, padX: 10, padY: 8));
        t.SetStylebox("title_button_pressed", "Tree", Box(new Color("#1b1b22"), radius: 0, padX: 10, padY: 8));
        t.SetColor("title_button_color", "Tree", Muted);
        t.SetColor("font_color", "Tree", Text);
        t.SetColor("font_selected_color", "Tree", Colors.White);
        t.SetColor("guide_color", "Tree", new Color(1, 1, 1, 0.04f));
        t.SetConstant("v_separation", "Tree", 10);
        t.SetConstant("item_margin", "Tree", 0);
        t.SetConstant("inner_item_margin_left", "Tree", 8);
        t.SetConstant("inner_item_margin_right", "Tree", 8);
        t.SetConstant("draw_guides", "Tree", 1);

        // Thin scrollbars.
        var grabber = Box(new Color("#3a3a46"), radius: 4, padX: 0, padY: 0);
        t.SetStylebox("scroll", "VScrollBar", Box(Colors.Transparent, padX: 3, padY: 0));
        t.SetStylebox("grabber", "VScrollBar", grabber);
        t.SetStylebox("grabber_highlight", "VScrollBar", Box(Muted, radius: 4, padX: 0, padY: 0));
        t.SetStylebox("grabber_pressed", "VScrollBar", Box(Accent, radius: 4, padX: 0, padY: 0));

        t.SetStylebox("separator", "HSeparator", Box(Border, radius: 0, padX: 0, padY: 0));
        t.SetConstant("separation", "HSeparator", 18);

        return t;
    }

    public static Label Label(string text, string? variation = null, int? fontSize = null, Color? color = null)
    {
        var label = new Label { Text = text };
        if (variation is not null)
            label.ThemeTypeVariation = variation;
        if (fontSize is { } size)
            label.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c)
            label.AddThemeColorOverride("font_color", c);
        return label;
    }

    public static Button Button(string text, string? variation = null, System.Action? onPressed = null)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
        if (variation is not null)
            button.ThemeTypeVariation = variation;
        if (onPressed is not null)
            button.Pressed += onPressed;
        return button;
    }

    public static Control Spacer(bool horizontal = true) => new Control
    {
        SizeFlagsHorizontal = horizontal ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill,
        SizeFlagsVertical = horizontal ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
    };

    public static string ModeLabel(string mode) => mode == "chaos" ? "Chaos" : "Duel Pit";

    public static string CharacterLabel(string id) => id switch
    {
        "blink" => "Blink",
        "firepatch" => "Fire Patch",
        "" => "—",
        _ => id,
    };

    public static string Duration(double seconds)
    {
        var s = (int)System.Math.Round(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    public static string TimeAgo(System.DateTime utc)
    {
        var ago = System.DateTime.UtcNow - utc.ToUniversalTime();
        if (ago.TotalMinutes < 1) return "just now";
        if (ago.TotalHours < 1) return $"{(int)ago.TotalMinutes} min ago";
        if (ago.TotalDays < 1) return $"{(int)ago.TotalHours} h ago";
        if (ago.TotalDays < 2) return "yesterday";
        if (ago.TotalDays < 7) return $"{(int)ago.TotalDays} days ago";
        return utc.ToLocalTime().ToString("d MMM yyyy");
    }
}
