using Gnab.Capture;

namespace Gnab.UI;

public sealed class CapturePromptForm : Form
{
    public CaptureAction ChosenAction { get; private set; } = CaptureAction.Cancel;

    private readonly Label _label;
    private readonly List<RoundedButton> _buttons = new();
    private readonly RoundedButton _saveButton;
    private readonly RoundedButton _cancelButton;

    public CapturePromptForm()
    {
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = ThemeManager.BodyFont;
        Text = "GNAB";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(400, 235);
        KeyPreview = true;

        _label = new Label
        {
            Text = "Screenshot captured. What would you like to do?",
            Size = new Size(360, 30),
            Location = new Point(20, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_label);

        // Grouped by column: save actions (col 1), copy actions (col 2), editors (col 3); Cancel on
        // its own row. Copy Text sits directly under Copy since both copy the capture — one as an
        // image, one as recognized text.
        _saveButton = AddButton("Save", CaptureAction.Save, new Point(20, 72));
        AddButton("Copy", CaptureAction.Copy, new Point(145, 72));
        AddButton("Edit", CaptureAction.Edit, new Point(270, 72));
        AddButton("Save + Copy", CaptureAction.SaveAndCopy, new Point(20, 120));
        AddButton("Copy Text", CaptureAction.CopyText, new Point(145, 120));
        AddButton("Markup", CaptureAction.Markup, new Point(270, 120));
        _cancelButton = AddButton("Cancel", CaptureAction.Cancel, new Point(270, 178));

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        ApplyTheme(ThemeManager.Current);
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

    private void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Surface;
        _label.ForeColor = palette.TextPrimary;

        foreach (var button in _buttons)
        {
            ThemeManager.StyleButton(button, palette, emphasized: button == _saveButton);
        }
    }

    private RoundedButton AddButton(string text, CaptureAction action, Point location)
    {
        var button = new RoundedButton
        {
            Text = text,
            Location = location,
            // Fixed size, not AutoSize. AutoSize controls that aren't in a layout container re-measure
            // inconsistently during the DPI-scaling pass a PerMonitorV2 window gets while it's shown,
            // which made these buttons land at the wrong width most of the time. A fixed size scales
            // deterministically under AutoScaleMode.Dpi, so every button is always identical. The width
            // comfortably fits the longest label ("Save + Copy") at the emphasis font at any scaling.
            Size = new Size(110, 40),
            UseMnemonic = false
        };
        button.Click += (_, _) =>
        {
            ChosenAction = action;
            DialogResult = DialogResult.OK;
            Close();
        };
        Controls.Add(button);
        _buttons.Add(button);
        return button;
    }
}
