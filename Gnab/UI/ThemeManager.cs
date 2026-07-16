using Gnab.Settings;

namespace Gnab.UI;

public static class ThemeManager
{
    public static ThemePalette Current { get; private set; } = ThemePalette.Light;

    // "Segoe UI Variable" ships with Windows 10 1809+/11 and reads rounder and friendlier than
    // classic Segoe UI; Semibold is used for buttons/tab labels for a slightly heavier, chunkier look.
    public static Font BodyFont { get; } = ResolveFont("Segoe UI Variable Text", 9.5f, FontStyle.Regular);
    public static Font EmphasisFont { get; } = ResolveFont("Segoe UI Variable Text Semibold", 9.5f, FontStyle.Regular);

    // Larger variants used only by the Settings dialog, which is deliberately scaled up to match the
    // roomier proportions of the design mockup. Kept separate so the Ask prompt and Markup toolbar —
    // whose fixed layouts are tuned for the smaller size — are unaffected.
    public static Font SettingsBodyFont { get; } = ResolveFont("Segoe UI Variable Text", 12f, FontStyle.Regular);
    public static Font SettingsEmphasisFont { get; } = ResolveFont("Segoe UI Variable Text Semibold", 12f, FontStyle.Regular);

    private static Font ResolveFont(string familyName, float size, FontStyle style)
    {
        try
        {
            using var collection = new System.Drawing.Text.InstalledFontCollection();
            if (collection.Families.Any(f => f.Name == familyName))
            {
                return new Font(familyName, size, style);
            }
        }
        catch
        {
            // fall through to the safe default below
        }

        return new Font("Segoe UI", size, style);
    }

    public static void Refresh(AppTheme theme)
    {
        Current = theme switch
        {
            AppTheme.Dark => ThemePalette.Dark,
            AppTheme.Light => ThemePalette.Light,
            AppTheme.System => IsSystemDarkMode() ? ThemePalette.Dark : ThemePalette.Light,
            _ => ThemePalette.Light
        };
    }

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Styles one button in a custom tab strip (see SettingsForm) — the selected tab uses Surface to
    /// read as "attached" to the content pane below it, unselected tabs blend into the strip's Canvas
    /// background. This replaced a native TabControl, which always drew a themed border/background
    /// around the content area that ignored BackColor and couldn't be fully suppressed.
    /// </summary>
    public static void StyleTabButton(RoundedButton button, ThemePalette palette, bool isSelected)
    {
        button.Font = EmphasisFont;
        button.BackColor = isSelected ? palette.Surface : palette.Canvas;
        button.ForeColor = isSelected ? palette.TextPrimary : palette.TextSecondary;
        button.BorderColor = palette.Canvas;
        button.BorderThickness = 0;
        button.Invalidate();
    }

    /// <summary>
    /// Applies theme colors to a button. Backgrounds always stay neutral (no accent fills) — an
    /// emphasized button (e.g. a dialog's default action) only gets a slightly bolder border, matching
    /// how native Windows highlights a default button rather than filling it with color.
    /// </summary>
    public static void StyleButton(Button button, ThemePalette palette, bool emphasized = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.BackColor = palette.InputFill;
        button.ForeColor = palette.TextPrimary;
        button.Font = EmphasisFont;
        button.FlatAppearance.BorderColor = emphasized ? palette.BorderStrong : palette.Border;
        button.FlatAppearance.BorderSize = 1;

        if (button is RoundedButton rounded)
        {
            rounded.BorderColor = emphasized ? palette.BorderStrong : palette.Border;
            rounded.BorderThickness = 1;
            rounded.Invalidate();
        }
    }

    public static void StyleTextBox(TextBox textBox, ThemePalette palette)
    {
        textBox.BackColor = palette.InputFill;
        textBox.ForeColor = palette.TextPrimary;
        textBox.BorderStyle = BorderStyle.FixedSingle;
    }

    /// <summary>
    /// The native checkmark glyph is drawn by Windows using the system accent color (blue) with no
    /// property to recolor it, so AccentCheckBox owner-draws its own box/checkmark instead.
    /// </summary>
    public static void StyleCheckBox(AccentCheckBox checkBox, ThemePalette palette)
    {
        checkBox.ForeColor = palette.TextPrimary;
        checkBox.AccentColor = BrandColors.Accent;
        checkBox.CheckGlyphColor = BrandColors.Dark;
        checkBox.BoxBorderColor = palette.Border;
        checkBox.BoxFillColor = palette.InputFill;
        checkBox.Invalidate();
    }

    /// <summary>
    /// ComboBox's BackColor/ForeColor only theme the closed control, not the dropdown popup list —
    /// that list is always drawn white by Windows unless owner-drawn, so dark mode needs owner-draw
    /// for the items. The dropdown arrow button itself is separate native chrome with no owner-draw
    /// hook at all; DarkComboBox instances get a small painted overlay for that region instead.
    /// </summary>
    public static void StyleComboBox(ComboBox comboBox, ThemePalette palette)
    {
        comboBox.BackColor = palette.InputFill;
        comboBox.ForeColor = palette.TextPrimary;
        comboBox.DrawItem -= ComboBoxOnDrawItem;

        if (comboBox is DarkComboBox darkCombo)
        {
            darkCombo.DarkArrow = palette.IsDark;
            darkCombo.ArrowBackColor = palette.InputFill;
            darkCombo.ArrowColor = BrandColors.Accent;
            darkCombo.Invalidate();
        }

        if (!palette.IsDark)
        {
            comboBox.DrawMode = DrawMode.Normal;
            return;
        }

        comboBox.Tag = palette;
        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
        // OwnerDrawFixed uses a single ItemHeight for every dropdown row; the default is tuned for the
        // default font and clips taller text, so track the control's actual font height.
        comboBox.ItemHeight = comboBox.Font.Height + 2;
        comboBox.DrawItem += ComboBoxOnDrawItem;
    }

    private static void ComboBoxOnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.Tag is not ThemePalette palette) return;
        if (e.Index < 0) return;

        var isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var background = isSelected ? palette.BorderStrong : palette.InputFill;

        using var brush = new SolidBrush(background);
        e.Graphics.FillRectangle(brush, e.Bounds);

        var text = comboBox.GetItemText(comboBox.Items[e.Index]);
        TextRenderer.DrawText(e.Graphics, text, comboBox.Font, e.Bounds, palette.TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
    }
}
