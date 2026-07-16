using System.Drawing.Drawing2D;

namespace Gnab.UI;

/// <summary>
/// A checkbox drawn entirely from scratch on a plain Control. It does not derive from WinForms'
/// CheckBox because ButtonBase insists on painting a native focus rectangle (and other chrome) that
/// bleeds through even a full owner-draw and can't be reliably suppressed. Building on Control gives a
/// clean surface: we draw the box, checkmark, and label ourselves and toggle Checked on click.
/// </summary>
public sealed class AccentCheckBox : Control
{
    // Track the font so the box reads proportionally at either the standard or the larger Settings size.
    private int BoxSize => Math.Max(14, Font.Height + 1);
    private bool _checked;

    public Color AccentColor { get; set; } = Color.LimeGreen;
    public Color CheckGlyphColor { get; set; } = Color.Black;
    public Color BoxBorderColor { get; set; } = Color.Gray;
    public Color BoxFillColor { get; set; } = Color.White;

    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public AccentCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                  ControlStyles.SupportsTransparentBackColor, true);
        Cursor = Cursors.Hand;
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var textSize = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
        return new Size(BoxSize + 8 + textSize.Width + 2, Math.Max(BoxSize, textSize.Height) + 2);
    }

    // A bare Control doesn't auto-apply GetPreferredSize the way ButtonBase does, so drive it from the
    // inputs that affect measurement (text, font) and once the handle exists.
    private void ResizeToContent()
    {
        if (AutoSize) Size = GetPreferredSize(Size.Empty);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ResizeToContent();
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        ResizeToContent();
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        ResizeToContent();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;

        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var backBrush = new SolidBrush(Parent?.BackColor ?? BackColor))
        {
            g.FillRectangle(backBrush, ClientRectangle);
        }

        var boxRect = new Rectangle(0, Math.Max(0, (Height - BoxSize) / 2), BoxSize, BoxSize);

        using (var fillBrush = new SolidBrush(Checked ? AccentColor : BoxFillColor))
        {
            g.FillRectangle(fillBrush, boxRect);
        }

        using (var borderPen = new Pen(Checked ? AccentColor : BoxBorderColor))
        {
            g.DrawRectangle(borderPen, boxRect);
        }

        if (Checked)
        {
            using var checkPen = new Pen(CheckGlyphColor, 2) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            var s = boxRect.Width;
            PointF[] check =
            [
                new PointF(boxRect.X + s * 0.22f, boxRect.Y + s * 0.52f),
                new PointF(boxRect.X + s * 0.42f, boxRect.Y + s * 0.72f),
                new PointF(boxRect.X + s * 0.78f, boxRect.Y + s * 0.28f)
            ];
            g.DrawLines(checkPen, check);
        }

        var textRect = new Rectangle(BoxSize + 8, 0, Math.Max(0, Width - BoxSize - 8), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
