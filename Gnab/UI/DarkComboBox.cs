namespace Gnab.UI;

/// <summary>
/// A ComboBox that can recolor its dropdown arrow button for dark mode. DrawItem/OwnerDraw only
/// covers the item list and the closed-box text — the arrow button itself is native chrome painted
/// by Windows using fixed system colors, with no theming hook available. This intercepts WM_PAINT
/// and paints a small dark rectangle + arrow glyph over that region immediately after the native
/// paint completes, leaving everything else (popup behavior, keyboard nav, positioning) untouched.
/// </summary>
public sealed class DarkComboBox : ComboBox
{
    private const int WmPaint = 0x000F;

    public bool DarkArrow { get; set; }
    public Color ArrowBackColor { get; set; } = Color.Black;
    public Color ArrowColor { get; set; } = Color.White;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WmPaint && DarkArrow)
        {
            PaintArrowButton();
        }
    }

    private void PaintArrowButton()
    {
        var buttonWidth = SystemInformation.VerticalScrollBarWidth;
        if (buttonWidth <= 0 || buttonWidth >= Width) return;

        var rect = new Rectangle(Width - buttonWidth, 0, buttonWidth, Height);

        using var g = Graphics.FromHwnd(Handle);
        using var backBrush = new SolidBrush(ArrowBackColor);
        g.FillRectangle(backBrush, rect);

        var cx = rect.X + rect.Width / 2;
        var cy = rect.Y + rect.Height / 2;
        Point[] triangle =
        [
            new Point(cx - 4, cy - 2),
            new Point(cx + 4, cy - 2),
            new Point(cx, cy + 3)
        ];
        using var arrowBrush = new SolidBrush(ArrowColor);
        g.FillPolygon(arrowBrush, triangle);
    }
}
