using System.Drawing.Drawing2D;

namespace Gnab.UI;

/// <summary>
/// A flat button with rounded corners. Paints its own background/border since WinForms' native
/// Button chrome is always square; fills the full client rect with the parent's BackColor first
/// so the corners outside the rounded path blend into whatever's behind it.
/// </summary>
public sealed class RoundedButton : Button
{
    public int CornerRadius { get; set; } = 4;
    public Color BorderColor { get; set; } = Color.Gray;
    public int BorderThickness { get; set; } = 1;

    /// <summary>When true, only the top corners are rounded and the bottom edge is square — used for
    /// the custom tab strip, so the selected tab reads as "attached" to the content pane below it.</summary>
    public bool RoundTopOnly { get; set; }

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? BackColor))
        {
            g.FillRectangle(parentBrush, ClientRectangle);
        }

        var rect = Rectangle.Inflate(ClientRectangle, -1, -1);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Clamp so the arc bounding boxes never exceed the rect itself — without this, a control
        // rendered smaller than 2x the corner radius (possible transiently during layout) produces a
        // visually broken or self-intersecting path.
        var radius = Math.Max(0, Math.Min(CornerRadius, Math.Min(rect.Width, rect.Height) / 2));
        using var path = RoundTopOnly ? RoundedTopRectPath(rect, radius) : RoundedRectPath(rect, radius);

        using (var fillBrush = new SolidBrush(BackColor))
        {
            g.FillPath(fillBrush, path);
        }

        if (BorderThickness > 0)
        {
            using var pen = new Pen(BorderColor, BorderThickness);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private static GraphicsPath RoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath RoundedTopRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        return path;
    }
}
