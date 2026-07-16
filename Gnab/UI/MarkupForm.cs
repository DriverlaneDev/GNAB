using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Gnab.UI;

public sealed class MarkupForm : Form
{
    public enum MarkupOutcome
    {
        Copy,
        Save,
        Discard
    }

    private enum Tool
    {
        None,
        Crop,
        Pen,
        Pencil
    }

    private const int PenWidth = 6;
    private const int PencilWidth = 2;

    private static readonly Pen CropSelectionPen = new(Color.DeepSkyBlue, 2);

    public MarkupOutcome Outcome { get; private set; } = MarkupOutcome.Discard;
    public Bitmap? EditedImage { get; private set; }

    private Bitmap _canvas;
    private readonly Stack<Bitmap> _undoStack = new();
    private Tool _activeTool = Tool.None;
    private Color _drawColor = Color.Red;
    private readonly Pen _drawPen = new(Color.Red) { StartCap = LineCap.Round, EndCap = LineCap.Round };
    private readonly SolidBrush _drawBrush = new(Color.Red);
    private float _scale = 1f;

    private bool _dragging;
    private Point _lastImagePoint;
    private Rectangle _cropSelection;

    private readonly Panel _canvasPanel;
    private Panel _toolbar = null!;
    private readonly RoundedButton _btnCrop;
    private readonly RoundedButton _btnPen;
    private readonly RoundedButton _btnPencil;
    private readonly Panel _colorSwatch;
    private readonly RoundedButton _btnUndo;
    private readonly RoundedButton _btnCopy;
    private readonly RoundedButton _btnSave;
    private readonly RoundedButton _btnDiscard;

    public MarkupForm(Bitmap capture)
    {
        _canvas = (Bitmap)capture.Clone();

        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = ThemeManager.BodyFont;
        Text = "GNAB - Markup";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var workArea = Screen.PrimaryScreen!.WorkingArea;
        var maxCanvasWidth = (int)(workArea.Width * 0.85);
        var maxCanvasHeight = (int)(workArea.Height * 0.85) - 60;
        _scale = Math.Min(1f, Math.Min((float)maxCanvasWidth / _canvas.Width, (float)maxCanvasHeight / _canvas.Height));

        var canvasWidth = Math.Max(1, (int)(_canvas.Width * _scale));
        var canvasHeight = Math.Max(1, (int)(_canvas.Height * _scale));
        ClientSize = new Size(Math.Max(780, canvasWidth), canvasHeight + 56);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 46 };
        Controls.Add(toolbar);

        _btnCrop = new RoundedButton { Text = "Crop", Location = new Point(10, 8), Size = new Size(70, 30), UseMnemonic = false };
        _btnPen = new RoundedButton { Text = "Pen", Location = new Point(90, 8), Size = new Size(70, 30), UseMnemonic = false };
        _btnPencil = new RoundedButton { Text = "Pencil", Location = new Point(170, 8), Size = new Size(70, 30), UseMnemonic = false };
        _colorSwatch = new Panel
        {
            Location = new Point(250, 8),
            Size = new Size(36, 30),
            BackColor = _drawColor,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        var colorTip = new ToolTip();
        colorTip.SetToolTip(_colorSwatch, "Click to change color");
        _btnUndo = new RoundedButton { Text = "Undo", Location = new Point(296, 8), Size = new Size(70, 30), UseMnemonic = false };

        toolbar.Controls.Add(_btnCrop);
        toolbar.Controls.Add(_btnPen);
        toolbar.Controls.Add(_btnPencil);
        toolbar.Controls.Add(_colorSwatch);
        toolbar.Controls.Add(_btnUndo);

        // toolbar.Width now reflects its real Dock=Top width (already parented to the Form),
        // so these Locations and Anchors are computed against the correct size, not a provisional one.
        _btnDiscard = new RoundedButton
        {
            Text = "Discard", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(80, 30),
            UseMnemonic = false
        };
        _btnCopy = new RoundedButton
        {
            Text = "Copy", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(80, 30),
            UseMnemonic = false
        };
        _btnSave = new RoundedButton
        {
            Text = "Save", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(80, 30),
            UseMnemonic = false
        };
        _btnDiscard.Location = new Point(toolbar.Width - 15 - _btnDiscard.Width, 8);
        _btnCopy.Location = new Point(_btnDiscard.Location.X - 10 - _btnCopy.Width, 8);
        _btnSave.Location = new Point(_btnCopy.Location.X - 10 - _btnSave.Width, 8);

        toolbar.Controls.Add(_btnSave);
        toolbar.Controls.Add(_btnCopy);
        toolbar.Controls.Add(_btnDiscard);

        _btnDiscard.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnCopy.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnSave.Anchor = AnchorStyles.Top | AnchorStyles.Right;

        _canvasPanel = new CanvasPanel(this) { Dock = DockStyle.Fill, BackColor = SystemColors.ControlDarkDark };
        Controls.Add(_canvasPanel);
        _canvasPanel.BringToFront();

        _btnCrop.Click += (_, _) => SetTool(Tool.Crop);
        _btnPen.Click += (_, _) => SetTool(Tool.Pen);
        _btnPencil.Click += (_, _) => SetTool(Tool.Pencil);
        _colorSwatch.Click += (_, _) => PickColor();
        _btnUndo.Click += (_, _) => Undo();
        _btnCopy.Click += (_, _) => Finish(MarkupOutcome.Copy);
        _btnSave.Click += (_, _) => Finish(MarkupOutcome.Save);
        _btnDiscard.Click += (_, _) => Finish(MarkupOutcome.Discard);

        _toolbar = toolbar;
        ApplyTheme(ThemeManager.Current);
        UpdateToolButtons();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        ForegroundHelper.ForceForeground(this);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BrandColors.ApplyBrandTitleBar(this);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Finish(MarkupOutcome.Discard);
        }
    }

    private void SetTool(Tool tool)
    {
        _activeTool = _activeTool == tool ? Tool.None : tool;
        UpdateToolButtons();
    }

    private void UpdateToolButtons()
    {
        var palette = ThemeManager.Current;
        SetToolButtonState(_btnCrop, _activeTool == Tool.Crop, palette);
        SetToolButtonState(_btnPen, _activeTool == Tool.Pen, palette);
        SetToolButtonState(_btnPencil, _activeTool == Tool.Pencil, palette);
    }

    private static void SetToolButtonState(Button button, bool active, ThemePalette palette)
    {
        ThemeManager.StyleButton(button, palette, emphasized: active);
    }

    private void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Surface;
        _toolbar.BackColor = palette.Surface;

        ThemeManager.StyleButton(_btnUndo, palette);
        ThemeManager.StyleButton(_btnCopy, palette);
        ThemeManager.StyleButton(_btnSave, palette, emphasized: true);
        ThemeManager.StyleButton(_btnDiscard, palette);
    }

    private void PickColor()
    {
        using var dialog = new ColorDialog { Color = _drawColor };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _drawColor = dialog.Color;
            _colorSwatch.BackColor = _drawColor;
            _drawPen.Color = _drawColor;
            _drawBrush.Color = _drawColor;
        }
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push((Bitmap)_canvas.Clone());
    }

    private void Undo()
    {
        if (_undoStack.Count == 0) return;
        var previous = _undoStack.Pop();
        _canvas.Dispose();
        _canvas = previous;
        _canvasPanel.Invalidate();
    }

    private void Finish(MarkupOutcome outcome)
    {
        Outcome = outcome;
        EditedImage = outcome is MarkupOutcome.Copy or MarkupOutcome.Save ? (Bitmap)_canvas.Clone() : null;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        while (_undoStack.Count > 0)
        {
            _undoStack.Pop().Dispose();
        }

        _canvas.Dispose();
        _drawPen.Dispose();
        _drawBrush.Dispose();
    }

    private sealed class CanvasPanel : Panel
    {
        private readonly MarkupForm _owner;

        public CanvasPanel(MarkupForm owner)
        {
            _owner = owner;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            var (offsetX, offsetY, drawWidth, drawHeight) = GetCanvasLayout();

            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_owner._canvas, new Rectangle(offsetX, offsetY, drawWidth, drawHeight));

            if (_owner._activeTool == Tool.Crop && _owner._cropSelection is { Width: > 0, Height: > 0 })
            {
                var screenRect = ImageToScreenRect(_owner._cropSelection, offsetX, offsetY, _owner._scale);
                e.Graphics.DrawRectangle(CropSelectionPen, screenRect);
            }
        }

        private (int OffsetX, int OffsetY, int DrawWidth, int DrawHeight) GetCanvasLayout()
        {
            var drawWidth = (int)(_owner._canvas.Width * _owner._scale);
            var drawHeight = (int)(_owner._canvas.Height * _owner._scale);
            var offsetX = Math.Max(0, (Width - drawWidth) / 2);
            var offsetY = Math.Max(0, (Height - drawHeight) / 2);
            return (offsetX, offsetY, drawWidth, drawHeight);
        }

        private static Rectangle ImageToScreenRect(Rectangle imageRect, int offsetX, int offsetY, float scale)
        {
            return new Rectangle(
                offsetX + (int)(imageRect.X * scale),
                offsetY + (int)(imageRect.Y * scale),
                (int)(imageRect.Width * scale),
                (int)(imageRect.Height * scale));
        }

        private Point ToImagePoint(Point screenPoint)
        {
            var (offsetX, offsetY, _, _) = GetCanvasLayout();

            var x = (int)((screenPoint.X - offsetX) / _owner._scale);
            var y = (int)((screenPoint.Y - offsetY) / _owner._scale);
            return new Point(
                Math.Clamp(x, 0, _owner._canvas.Width - 1),
                Math.Clamp(y, 0, _owner._canvas.Height - 1));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _owner._activeTool == Tool.None) return;

            var imagePoint = ToImagePoint(e.Location);
            _owner._dragging = true;
            _owner._lastImagePoint = imagePoint;

            if (_owner._activeTool == Tool.Crop)
            {
                _owner._cropSelection = new Rectangle(imagePoint, Size.Empty);
            }
            else
            {
                _owner.PushUndoSnapshot();
                DrawDot(imagePoint);
            }

            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_owner._dragging) return;

            var imagePoint = ToImagePoint(e.Location);

            if (_owner._activeTool == Tool.Crop)
            {
                _owner._cropSelection = NormalizeRectangle(_owner._lastImagePoint, imagePoint);
            }
            else if (_owner._activeTool is Tool.Pen or Tool.Pencil)
            {
                DrawLine(_owner._lastImagePoint, imagePoint);
                _owner._lastImagePoint = imagePoint;
            }

            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_owner._dragging) return;
            _owner._dragging = false;

            if (_owner._activeTool == Tool.Crop)
            {
                var selection = _owner._cropSelection;
                if (selection.Width > 2 && selection.Height > 2)
                {
                    _owner.PushUndoSnapshot();
                    var cropped = _owner._canvas.Clone(selection, _owner._canvas.PixelFormat);
                    _owner._canvas.Dispose();
                    _owner._canvas = cropped;
                }

                _owner._cropSelection = Rectangle.Empty;
            }

            Invalidate();
        }

        private void DrawDot(Point imagePoint)
        {
            var width = _owner._activeTool == Tool.Pen ? PenWidth : PencilWidth;
            using var g = Graphics.FromImage(_owner._canvas);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(_owner._drawBrush, imagePoint.X - width / 2f, imagePoint.Y - width / 2f, width, width);
        }

        private void DrawLine(Point from, Point to)
        {
            var width = _owner._activeTool == Tool.Pen ? PenWidth : PencilWidth;
            using var g = Graphics.FromImage(_owner._canvas);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            _owner._drawPen.Width = width;
            g.DrawLine(_owner._drawPen, from, to);
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
}
