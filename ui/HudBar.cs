using Godot;

namespace TheRoom.UI;

/// <summary>
/// A horizontal meter drawn in code: a rounded track, a two-tone fill, optional segment marks, a
/// thin highlight along the top, and a "ghost" chip that trails behind the fill after it drops,
/// so a hit reads as a chunk falling away rather than the bar just jumping.
/// </summary>
public partial class HudBar : Control
{
    /// <summary>0..1, the current value.</summary>
    public float Value { get; set; } = 1f;
    /// <summary>0..1, the trailing chip; PlayerHud eases it down towards Value.</summary>
    public float Ghost { get; set; } = 1f;
    public Color FillTop { get; set; } = new("#e25548");
    public Color FillBottom { get; set; } = new("#a92a24");
    public Color GhostColor { get; set; } = new("#f4d98a");
    public Color Track { get; set; } = new("#1d1116");
    public int Segments { get; set; }
    public int Radius { get; set; } = 4;

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        var size = Size;
        DrawBox(new Rect2(Vector2.Zero, size), Track, borderColor: new Color(0, 0, 0, 0.55f));

        var inner = new Rect2(new Vector2(1, 1), size - new Vector2(2, 2));
        if (Ghost > Value)
            DrawBox(new Rect2(inner.Position, new Vector2(inner.Size.X * Mathf.Clamp(Ghost, 0f, 1f), inner.Size.Y)), GhostColor);

        var fillWidth = inner.Size.X * Mathf.Clamp(Value, 0f, 1f);
        if (fillWidth > 0.5f)
        {
            // Two-tone fill: the lighter top half gives the bar some depth without a texture.
            var fill = new Rect2(inner.Position, new Vector2(fillWidth, inner.Size.Y));
            DrawBox(fill, FillBottom);
            DrawBox(new Rect2(fill.Position, new Vector2(fillWidth, fill.Size.Y * 0.55f)), FillTop, bottomCorners: false);
            DrawLine(fill.Position + new Vector2(Radius, 1), fill.Position + new Vector2(Mathf.Max(Radius, fillWidth - Radius), 1),
                new Color(1, 1, 1, 0.28f), 1f);
        }

        for (var i = 1; i < Segments; i++)
        {
            var x = inner.Position.X + inner.Size.X * i / Segments;
            DrawLine(new Vector2(x, inner.Position.Y + 1), new Vector2(x, inner.End.Y - 1), new Color(0, 0, 0, 0.45f), 1.5f);
        }
    }

    private void DrawBox(Rect2 rect, Color color, Color? borderColor = null, bool bottomCorners = true)
    {
        var box = new StyleBoxFlat { BgColor = color, AntiAliasing = true };
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = Radius;
        box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = bottomCorners ? Radius : 0;
        if (borderColor is { } border)
        {
            box.BorderColor = border;
            box.SetBorderWidthAll(1);
        }
        DrawStyleBox(box, rect);
    }
}
