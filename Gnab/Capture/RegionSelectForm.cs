using Gnab.UI;

namespace Gnab.Capture;

public sealed class RegionSelectForm : Form
{
    private const int CrosshairArmLength = 14;

    private static readonly SolidBrush DimBrush = new(Color.FromArgb(120, 0, 0, 0));
    private static readonly Pen SelectionPen = new(BrandColors.Accent, 2);
    private static readonly Pen CrosshairPen = new(BrandColors.Accent, 1);

    private readonly Bitmap _fullImage;
    private Point _startPoint;
    private Rectangle _selection;
    private bool _selecting;
    private Point _mousePosition;
    private bool _hasMousePosition;

    public Bitmap? Result { get; private set; }

    public RegionSelectForm(Bitmap fullImage)
    {
        _fullImage = fullImage;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ForegroundHelper.ForceForeground(this);
        // The system crosshair cursor can't be recolored, so it's hidden in favor of a custom
        // crosshair drawn in OnPaint using the same blue as the selection rectangle.
        Cursor.Hide();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(_fullImage, Point.Empty);

        using var overlayRegion = new Region(new Rectangle(Point.Empty, Size));
        if (_selection is { Width: > 0, Height: > 0 })
        {
            overlayRegion.Exclude(_selection);
        }

        e.Graphics.Clip = overlayRegion;
        e.Graphics.FillRectangle(DimBrush, new Rectangle(Point.Empty, Size));
        e.Graphics.ResetClip();

        if (_selection is { Width: > 0, Height: > 0 })
        {
            e.Graphics.DrawRectangle(SelectionPen, _selection);
        }

        if (_hasMousePosition)
        {
            e.Graphics.DrawLine(CrosshairPen,
                _mousePosition.X - CrosshairArmLength, _mousePosition.Y,
                _mousePosition.X + CrosshairArmLength, _mousePosition.Y);
            e.Graphics.DrawLine(CrosshairPen,
                _mousePosition.X, _mousePosition.Y - CrosshairArmLength,
                _mousePosition.X, _mousePosition.Y + CrosshairArmLength);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _selecting = true;
        _startPoint = e.Location;
        _selection = new Rectangle(e.Location, Size.Empty);
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _hasMousePosition = true;
        _mousePosition = e.Location;

        if (_selecting)
        {
            _selection = NormalizeRectangle(_startPoint, e.Location);
        }

        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_selecting) return;
        _selecting = false;
        _selection = NormalizeRectangle(_startPoint, e.Location);

        if (_selection.Width > 2 && _selection.Height > 2)
        {
            Result = _fullImage.Clone(_selection, _fullImage.PixelFormat);
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Cursor.Show();
        _fullImage.Dispose();
    }

    private static Rectangle NormalizeRectangle(Point a, Point b)
    {
        return new Rectangle(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(a.X - b.X),
            Math.Abs(a.Y - b.Y));
    }
}
