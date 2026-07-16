using Gnab.Settings;

namespace Gnab.UI;

public sealed class HotkeyField : Panel
{
    private const string CapturePrompt = "Press keys...";

    private readonly Label _label;
    private readonly TextBox _textBox;
    private readonly RoundedButton _captureButton;
    private readonly RoundedButton _clearButton;
    private HotkeyCombo _combo = new();
    private bool _isCapturing;

    /// <summary>
    /// Raised while this field is waiting for a new key combination — the owning dialog uses this to
    /// pause global hotkey registration just for that moment, so pressing e.g. Ctrl+Shift+F to record
    /// a combo doesn't also trigger a live capture.
    /// </summary>
    public event Action? CaptureStarted;
    public event Action? CaptureEnded;

    public HotkeyCombo Combo
    {
        get => _combo;
        set
        {
            _combo = value;
            _textBox.Text = _combo.ToString();
        }
    }

    public HotkeyField(string labelText)
    {
        Size = new Size(760, 40);

        _label = new Label
        {
            Text = labelText,
            AutoSize = false,
            Location = new Point(0, 8),
            Size = new Size(150, 24),
            TextAlign = ContentAlignment.MiddleLeft
        };
        _textBox = new TextBox { ReadOnly = true, Location = new Point(158, 5), Width = 210, Text = "(none)" };
        _captureButton = new RoundedButton
        {
            Text = "Capture", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(100, 34),
            UseMnemonic = false
        };
        _clearButton = new RoundedButton
        {
            Text = "Clear", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(80, 34),
            UseMnemonic = false
        };
        _captureButton.Location = new Point(386, 2);
        _clearButton.Location = new Point(_captureButton.Right + 8, 2);

        _textBox.KeyDown += TextBoxOnKeyDown;
        _textBox.Leave += (_, _) =>
        {
            if (_textBox.Text == CapturePrompt)
            {
                _textBox.Text = _combo.ToString();
                EndCapture();
            }
        };
        _captureButton.Click += (_, _) =>
        {
            _textBox.Text = CapturePrompt;
            _textBox.Focus();
            StartCapture();
        };
        _clearButton.Click += (_, _) => Combo = new HotkeyCombo();

        Controls.Add(_label);
        Controls.Add(_textBox);
        Controls.Add(_captureButton);
        Controls.Add(_clearButton);
    }

    public void ApplyTheme(ThemePalette palette)
    {
        BackColor = palette.Surface;
        _label.ForeColor = palette.TextPrimary;
        ThemeManager.StyleTextBox(_textBox, palette);
        ThemeManager.StyleButton(_captureButton, palette);
        ThemeManager.StyleButton(_clearButton, palette);
        // StyleButton forces the small emphasis font; HotkeyField is Settings-only, so bump it back up
        // to the scaled Settings font to match the rest of the dialog.
        _captureButton.Font = ThemeManager.SettingsEmphasisFont;
        _clearButton.Font = ThemeManager.SettingsEmphasisFont;
    }

    private void StartCapture()
    {
        if (_isCapturing) return;
        _isCapturing = true;
        CaptureStarted?.Invoke();
    }

    private void EndCapture()
    {
        if (!_isCapturing) return;
        _isCapturing = false;
        CaptureEnded?.Invoke();
    }

    private void TextBoxOnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Tab)
        {
            return;
        }

        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            Combo = new HotkeyCombo();
            EndCapture();
            return;
        }

        var modifiers = HotkeyModifiers.None;
        if (e.Control) modifiers |= HotkeyModifiers.Control;
        if (e.Alt) modifiers |= HotkeyModifiers.Alt;
        if (e.Shift) modifiers |= HotkeyModifiers.Shift;

        if (modifiers == HotkeyModifiers.None)
        {
            MessageBox.Show(FindForm(), "Hotkeys must include at least one modifier (Ctrl, Alt, or Shift).",
                "Modifier required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Combo = new HotkeyCombo(modifiers, e.KeyCode);
        EndCapture();
    }
}
