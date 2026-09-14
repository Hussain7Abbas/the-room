using Godot;
using TheRoom.Entities;

namespace TheRoom.UI;

/// <summary>
/// Top-left HUD for the local player (core/Main.tscn, clients only):
///   - your player name in the character's colour (character name beside it), and HP as numbers;
///   - red health bar with a trailing "chip" after each hit, pulsing when low;
///   - green stamina bar under it (sprint and dodge spend it), amber while exhausted;
///   - the ability tile: key, a cooldown shade that drains as it recharges, the seconds left,
///     and a gold border when it's ready.
/// Health and stamina come from the server's state sync (Player.ReceiveServerState); the
/// cooldown from the ability's tell.
/// </summary>
public partial class PlayerHud : CanvasLayer
{
    private static readonly Color HealthTop = new("#ea5a4c");
    private static readonly Color HealthBottom = new("#a92a24");
    private static readonly Color StaminaTop = new("#6fe08a");
    private static readonly Color StaminaBottom = new("#2f9e4f");
    private static readonly Color Exhausted = new("#d7962f");

    private Control _panel = null!;
    private Label _name = null!;
    private Label _character = null!;
    private Label _hp = null!;
    private HudBar _health = null!;
    private HudBar _stamina = null!;
    private AbilityTile _ability = null!;
    private Label _abilityName = null!;

    private Player? _player;
    private double _searchIn;
    private float _lastHealth = 1f;
    private float _ghostHoldIn;

    public override void _Ready()
    {
        if (TheRoom.Core.Net.Instance.IsServer || DisplayServer.GetName() == "headless")
        {
            QueueFree();
            return;
        }
        Layer = 5;
        Build();
    }

    private void Build()
    {
        var root = new Control { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Get() };
        AddChild(root);

        var panel = new PanelContainer { Position = new Vector2(16, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.07f, 0.72f),
            BorderColor = new Color(1, 1, 1, 0.08f),
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
            ShadowColor = new Color(0, 0, 0, 0.35f),
            ShadowSize = 8,
        };
        style.SetCornerRadiusAll(10);
        style.SetBorderWidthAll(1);
        panel.AddThemeStyleboxOverride("panel", style);
        root.AddChild(panel);
        _panel = panel;

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);

        var bars = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        bars.AddThemeConstantOverride("separation", 5);
        row.AddChild(bars);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        _name = UiTheme.Label("", "Heading", fontSize: 17);
        header.AddChild(_name);
        _character = UiTheme.Label("", "Muted", fontSize: 12);
        _character.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        header.AddChild(_character);
        header.AddChild(UiTheme.Spacer());
        _hp = UiTheme.Label("", fontSize: 13, color: new Color("#f1d9d6"));
        header.AddChild(_hp);
        bars.AddChild(header);

        _health = new HudBar { CustomMinimumSize = new Vector2(0, 18), Segments = 4, FillTop = HealthTop, FillBottom = HealthBottom, Radius = 5 };
        bars.AddChild(_health);
        _stamina = new HudBar
        {
            CustomMinimumSize = new Vector2(0, 8), FillTop = StaminaTop, FillBottom = StaminaBottom,
            GhostColor = new Color(0.4f, 0.9f, 0.5f, 0.25f), Track = new Color("#0f1a13"), Radius = 3,
        };
        bars.AddChild(_stamina);

        var abilityColumn = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        abilityColumn.AddThemeConstantOverride("separation", 3);
        _ability = new AbilityTile { CustomMinimumSize = new Vector2(58, 58) };
        abilityColumn.AddChild(_ability);
        _abilityName = UiTheme.Label("", "Muted", fontSize: 11);
        _abilityName.HorizontalAlignment = HorizontalAlignment.Center;
        _abilityName.CustomMinimumSize = new Vector2(58, 0);
        abilityColumn.AddChild(_abilityName);
        row.AddChild(abilityColumn);

        _panel.Visible = false;
    }

    public override void _Process(double delta)
    {
        if (_player is null || !IsInstanceValid(_player))
        {
            _player = null;
            _searchIn -= delta;
            if (_searchIn <= 0)
            {
                _player = FindLocalPlayer();
                _searchIn = 0.5;
            }
            _panel.Visible = _player is not null;
            if (_player is null)
                return;
        }

        var dt = (float)delta;
        var p = _player;
        var def = p.Character;
        var t = Time.GetTicksMsec() / 1000f;

        // Your name, big, in your character's colour; the character beside it, small.
        _name.Text = string.IsNullOrEmpty(p.DisplayName) ? Core.Net.Instance.LocalPlayerName : p.DisplayName;
        _character.Text = def?.DisplayName ?? "";
        if (def is not null)
            _name.AddThemeColorOverride("font_color", def.SilhouetteColor.Lightened(0.25f));

        // Health, with the chip: it holds for a moment after a hit, then slides down to the bar.
        var health = p.HealthFraction;
        if (health < _lastHealth - 0.001f)
            _ghostHoldIn = 0.45f;
        else if (health > _health.Ghost)
            _health.Ghost = health; // healing or respawn: no chip
        _lastHealth = health;
        if (_ghostHoldIn > 0f)
            _ghostHoldIn -= dt;
        else
            _health.Ghost = Mathf.MoveToward(_health.Ghost, health, dt * 0.8f);
        _health.Value = health;
        var low = health > 0f && health <= 0.3f ? 0.5f + 0.5f * Mathf.Sin(t * 8f) : 0f;
        _health.FillTop = HealthTop.Lerp(new Color("#ff8a7a"), low * 0.6f);
        _hp.Text = $"{Mathf.CeilToInt(p.Health)} / {Mathf.RoundToInt(Core.TuningService.Instance.MaxHealth)}";

        // Stamina eases (it's predicted, so small corrections shouldn't jump), amber when exhausted.
        _stamina.Value = Mathf.MoveToward(_stamina.Value, p.StaminaFraction, dt * 2.5f);
        var exhausted = p.IsExhausted && p.StaminaFraction < 0.99f;
        _stamina.FillTop = exhausted ? Exhausted.Lightened(0.15f) : StaminaTop;
        _stamina.FillBottom = exhausted ? Exhausted.Darkened(0.2f) : StaminaBottom;

        _ability.Remaining = p.AbilityCooldownRemaining;
        _ability.Total = p.AbilityCooldownTotal;
        _ability.Key = Keycap("ability");
        _abilityName.Text = def?.Ability?.DisplayName ?? "";
        _ability.Visible = def?.Ability is not null;
    }

    private static string Keycap(string action) =>
        SettingsDialog.Bindings(action) is { Count: > 0 } keys ? keys[0] : "?";

    private Player? FindLocalPlayer()
    {
        if (GetTree().CurrentScene?.GetNodeOrNull<Node>("PlayersContainer") is not { } container)
            return null;
        foreach (var child in container.GetChildren())
        {
            if (child is Player player && player.IsLocallyControlled)
                return player;
        }
        return null;
    }

    /// <summary>The ability square: dark shade over the part still recharging (draining downward),
    /// the key in the corner, seconds left in the middle, and a gold border when ready.</summary>
    private partial class AbilityTile : Control
    {
        public float Remaining { get; set; }
        public float Total { get; set; }
        public string Key { get; set; } = "E";

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var ready = Remaining <= 0.01f;
            var size = Size;
            var t = Time.GetTicksMsec() / 1000f;

            var bg = new StyleBoxFlat { BgColor = new Color("#1a1a22"), AntiAliasing = true };
            bg.SetCornerRadiusAll(8);
            DrawStyleBox(bg, new Rect2(Vector2.Zero, size));

            // A simple emblem: a boot-sole chevron, bright when ready.
            var c = size / 2f;
            var emblem = ready ? new Color("#f2c14e") : new Color("#5d5c68");
            DrawPolyline(new[] { c + new Vector2(-12, 6), c + new Vector2(0, -8), c + new Vector2(12, 6) }, emblem, 4f, true);
            DrawLine(c + new Vector2(-12, 12), c + new Vector2(12, 12), emblem, 3f, true);

            if (!ready && Total > 0.01f)
            {
                var fraction = Mathf.Clamp(Remaining / Total, 0f, 1f);
                var shade = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.62f) };
                shade.SetCornerRadiusAll(8);
                DrawStyleBox(shade, new Rect2(0, size.Y * (1f - fraction), size.X, size.Y * fraction));
                var text = Remaining >= 1f ? Mathf.CeilToInt(Remaining).ToString() : Remaining.ToString("0.0");
                var font = ThemeDB.FallbackFont;
                var textSize = font.GetStringSize(text, HorizontalAlignment.Left, -1, 22);
                DrawString(font, new Vector2((size.X - textSize.X) / 2f, size.Y / 2f + 8f), text, HorizontalAlignment.Left, -1, 22, Colors.White);
            }

            var border = new StyleBoxFlat { DrawCenter = false, AntiAliasing = true };
            border.SetCornerRadiusAll(8);
            border.SetBorderWidthAll(2);
            border.BorderColor = ready
                ? new Color("#f2c14e").Lerp(new Color("#fff1c4"), 0.5f + 0.5f * Mathf.Sin(t * 4f))
                : new Color("#34343f");
            DrawStyleBox(border, new Rect2(Vector2.Zero, size));

            // Key badge, bottom-right.
            var badgeSize = new Vector2(Mathf.Max(18f, 8f + Key.Length * 8f), 16f);
            var badgeRect = new Rect2(size - badgeSize - new Vector2(3, 3), badgeSize);
            var badge = new StyleBoxFlat { BgColor = new Color("#0f0f14"), BorderColor = new Color("#44444f") };
            badge.SetCornerRadiusAll(4);
            badge.SetBorderWidthAll(1);
            DrawStyleBox(badge, badgeRect);
            DrawString(ThemeDB.FallbackFont, badgeRect.Position + new Vector2(5, 12.5f), Key, HorizontalAlignment.Left, -1, 12, new Color("#ecebe8"));
        }
    }
}
