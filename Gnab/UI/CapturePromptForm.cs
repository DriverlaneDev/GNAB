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
        ClientSize = new Size(400, 190);
        KeyPreview = true;

        _label = new Label
        {
            Text = "Screenshot captured. What would you like to do?",
            Size = new Size(360, 30),
            Location = new Point(20, 20),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Controls.Add(_label);

        _saveButton = AddButton("Save", CaptureAction.Save, new Point(20, 75));
        AddButton("Copy", CaptureAction.Copy, new Point(145, 75));
        AddButton("Save + Copy", CaptureAction.SaveAndCopy, new Point(270, 75));
        AddButton("Edit", CaptureAction.Edit, new Point(20, 125));
        AddButton("Markup", CaptureAction.Markup, new Point(145, 125));
        _cancelButton = AddButton("Cancel", CaptureAction.Cancel, new Point(270, 125));

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
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(110, 36),
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
