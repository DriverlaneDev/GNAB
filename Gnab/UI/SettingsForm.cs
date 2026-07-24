using Gnab.Settings;
using Gnab.Startup;

namespace Gnab.UI;

public sealed class SettingsForm : Form
{
    private sealed record ActionItem(DefaultAction Value, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly ActionItem[] DefaultActionOptions =
    {
        new(DefaultAction.Save, "Save to file"),
        new(DefaultAction.Copy, "Copy to clipboard"),
        new(DefaultAction.SaveAndCopy, "Save + copy to clipboard"),
        new(DefaultAction.Edit, "Copy + open editor to paste"),
        new(DefaultAction.Markup, "Open markup editor"),
        new(DefaultAction.Ask, "Ask each time")
    };

    private sealed record FormatItem(ImageFormatOption Value, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly FormatItem[] FormatOptions =
    {
        new(ImageFormatOption.Png, ".png"),
        new(ImageFormatOption.Jpeg, ".jpg"),
        new(ImageFormatOption.Bmp, ".bmp"),
        new(ImageFormatOption.Gif, ".gif"),
        new(ImageFormatOption.Tiff, ".tif")
    };

    private sealed record ThemeItem(AppTheme Value, string Label)
    {
        public override string ToString() => Label;
    }

    private static readonly ThemeItem[] ThemeOptions =
    {
        new(AppTheme.Light, "Light"),
        new(AppTheme.Dark, "Dark"),
        new(AppTheme.System, "Match Windows")
    };

    public AppSettings Result { get; }

    // Custom tab strip instead of a native TabControl — the native control always drew a themed
    // border/background around its content area that ignored BackColor and couldn't be fully
    // suppressed in dark mode, even with owner-draw and SetWindowTheme tricks.
    private readonly Panel _tabStrip = new() { Dock = DockStyle.Top, Height = 46 };
    private readonly Panel _tabContent = new() { Location = new Point(0, 46), Size = new Size(810, 250) };
    private readonly List<RoundedButton> _tabButtons = new();
    private readonly List<Panel> _tabPages = new();
    private int _selectedTabIndex;
    private ThemePalette _currentPalette = ThemePalette.Light;

    private readonly List<Panel> _dividers = new();
    private readonly List<Label> _labels = new();
    private readonly List<Label> _mutedLabels = new();

    private readonly Label _lblDefaultAction = new() { Text = "Default action after capture:", AutoSize = true };
    private readonly DarkComboBox _cmbDefaultAction = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };

    private readonly Label _lblTheme = new() { Text = "Appearance:", AutoSize = true };
    private readonly DarkComboBox _cmbTheme = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    private readonly AccentCheckBox _chkStartWithWindows = new() { Text = "Start GNAB with Windows", AutoSize = true };
    private readonly AccentCheckBox _chkShowNotifications = new() { Text = "Show a notification after each capture", AutoSize = true };

    private readonly RadioButton _rbPrompt = new() { Text = "Ask where to save each time", AutoSize = true };
    private readonly RadioButton _rbAutoSave = new() { Text = "Auto-save to a folder", AutoSize = true };
    private readonly TextBox _txtFolder = new() { ReadOnly = true, Width = 420 };

    private readonly RoundedButton _btnBrowseFolder = new()
    {
        Text = "Browse...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(100, 34),
        UseMnemonic = false
    };

    private readonly DarkComboBox _cmbFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 170 };
    private readonly NumericUpDown _numJpegQuality = new() { Minimum = 1, Maximum = 100, Width = 80 };
    private readonly Label _lblJpegQuality = new() { Text = "JPEG quality:", AutoSize = true };

    private readonly DarkComboBox _cmbEditor = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly TextBox _txtCustomEditor = new() { ReadOnly = true, Width = 440 };

    private readonly RoundedButton _btnBrowseEditor = new()
    {
        Text = "Browse...", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(100, 34),
        UseMnemonic = false
    };

    private readonly HotkeyField _fullScreenHotkey = new("Full screen:");
    private readonly HotkeyField _regionHotkey = new("Region:");
    private readonly HotkeyField _activeWindowHotkey = new("Active window:");

    private readonly RoundedButton _btnOk = new()
    {
        Text = "OK", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(120, 40),
        UseMnemonic = false
    };

    private readonly RoundedButton _btnCancel = new()
    {
        Text = "Cancel", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly, MinimumSize = new Size(120, 40),
        DialogResult = DialogResult.Cancel, UseMnemonic = false
    };

    private readonly Label _lblVersion = new() { Text = $"v{AppInfo.Version}", AutoSize = true };

    /// <summary>
    /// Raised while a HotkeyField is actively waiting for a new key combination, so the tray context
    /// can pause global hotkey registration just for that moment instead of the whole dialog's
    /// lifetime — otherwise capture would never trigger while Settings is open.
    /// </summary>
    public event Action? HotkeyCaptureStarted;
    public event Action? HotkeyCaptureEnded;

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();

        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = ThemeManager.SettingsBodyFont;
        Text = "GNAB - Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(810, 350);
        CancelButton = _btnCancel;

        AddTab("General", BuildGeneralTab());
        AddTab("Saving", BuildSavingTab());
        AddTab("Editor", BuildEditorTab());
        AddTab("Hotkeys", BuildHotkeysTab());
        LayoutTabButtons();
        Controls.Add(_tabContent);
        Controls.Add(_tabStrip);
        SelectTab(0);

        _btnCancel.Location = new Point(ClientSize.Width - 26 - _btnCancel.Width, 306);
        _btnOk.Location = new Point(_btnCancel.Location.X - 10 - _btnOk.Width, 306);
        Controls.Add(_btnOk);
        Controls.Add(_btnCancel);
        AcceptButton = _btnOk;

        // Bottom-left of the footer, vertically centered against the OK/Cancel row.
        _lblVersion.Location = new Point(26, 306 + (_btnOk.Height - _lblVersion.Height) / 2);
        _mutedLabels.Add(_lblVersion);
        Controls.Add(_lblVersion);

        LoadFromSettings(current);
        ApplyTheme(ThemeManager.Current);

        foreach (var field in new[] { _fullScreenHotkey, _regionHotkey, _activeWindowHotkey })
        {
            field.CaptureStarted += () => HotkeyCaptureStarted?.Invoke();
            field.CaptureEnded += () => HotkeyCaptureEnded?.Invoke();
        }

        _rbAutoSave.CheckedChanged += (_, _) => UpdateSaveModeState();
        _btnBrowseFolder.Click += (_, _) => BrowseFolder();
        _cmbFormat.SelectedIndexChanged += (_, _) => UpdateFormatState();
        _cmbEditor.SelectedIndexChanged += (_, _) => UpdateEditorState();
        _btnBrowseEditor.Click += (_, _) => BrowseEditor();
        _cmbTheme.SelectedIndexChanged += (_, _) => PreviewTheme();
        _btnOk.Click += (_, _) => OnOk();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        BrandColors.ApplyBrandTitleBar(this);
    }

    private void AddTab(string label, Panel page)
    {
        var index = _tabButtons.Count;
        var button = new RoundedButton
        {
            Text = label, RoundTopOnly = true, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowOnly,
            MinimumSize = new Size(95, 40), UseMnemonic = false
        };
        button.Click += (_, _) => SelectTab(index);

        page.Size = _tabContent.Size;
        page.Visible = false;

        _tabButtons.Add(button);
        _tabPages.Add(page);
        _tabStrip.Controls.Add(button);
        _tabContent.Controls.Add(page);
    }

    private void LayoutTabButtons()
    {
        var x = 26;
        foreach (var button in _tabButtons)
        {
            button.Location = new Point(x, 6);
            x += button.Width + 3;
        }
    }

    private void SelectTab(int index)
    {
        _selectedTabIndex = index;
        for (var i = 0; i < _tabPages.Count; i++)
        {
            _tabPages[i].Visible = i == index;
        }

        // Use the form's own actively-previewed palette, not the global ThemeManager.Current — the
        // Appearance dropdown previews a theme by calling ApplyTheme directly without touching that
        // global, so using it here would revert tab-button colors to the pre-preview theme the moment
        // any tab is clicked after switching Appearance.
        RestyleTabButtons(_currentPalette);
    }

    private void RestyleTabButtons(ThemePalette palette)
    {
        for (var i = 0; i < _tabButtons.Count; i++)
        {
            ThemeManager.StyleTabButton(_tabButtons[i], palette, isSelected: i == _selectedTabIndex);
        }
    }

    private Panel BuildGeneralTab()
    {
        var page = new Panel();
        _lblDefaultAction.Location = new Point(26, 26);
        _cmbDefaultAction.Location = new Point(26, 54);
        _cmbDefaultAction.Items.AddRange(DefaultActionOptions);

        var divider1 = new Panel { Location = new Point(26, 102), Size = new Size(758, 1) };
        _dividers.Add(divider1);

        _chkStartWithWindows.Location = new Point(26, 118);
        _chkShowNotifications.Location = new Point(26, 152);

        var divider2 = new Panel { Location = new Point(26, 194), Size = new Size(758, 1) };
        _dividers.Add(divider2);

        _lblTheme.Location = new Point(26, 212);
        _cmbTheme.Location = new Point(150, 208);
        _cmbTheme.Items.AddRange(ThemeOptions);

        _labels.Add(_lblDefaultAction);
        _labels.Add(_lblTheme);

        page.Controls.Add(_lblDefaultAction);
        page.Controls.Add(_cmbDefaultAction);
        page.Controls.Add(divider1);
        page.Controls.Add(_chkStartWithWindows);
        page.Controls.Add(_chkShowNotifications);
        page.Controls.Add(divider2);
        page.Controls.Add(_lblTheme);
        page.Controls.Add(_cmbTheme);
        return page;
    }

    private Panel BuildSavingTab()
    {
        var page = new Panel();
        _rbPrompt.Location = new Point(26, 24);
        _rbAutoSave.Location = new Point(26, 58);
        _txtFolder.Location = new Point(50, 92);
        _txtFolder.Width = 600;
        _btnBrowseFolder.Location = new Point(662, 90);

        var divider = new Panel { Location = new Point(26, 138), Size = new Size(758, 1) };
        _dividers.Add(divider);

        var lblFormat = new Label { Text = "Save format:", AutoSize = true, Location = new Point(26, 158) };
        _labels.Add(lblFormat);
        _cmbFormat.Location = new Point(150, 154);
        _cmbFormat.Items.AddRange(FormatOptions);

        _lblJpegQuality.Location = new Point(26, 200);
        _labels.Add(_lblJpegQuality);
        _numJpegQuality.Location = new Point(150, 197);

        page.Controls.Add(_rbPrompt);
        page.Controls.Add(_rbAutoSave);
        page.Controls.Add(_txtFolder);
        page.Controls.Add(_btnBrowseFolder);
        page.Controls.Add(divider);
        page.Controls.Add(lblFormat);
        page.Controls.Add(_cmbFormat);
        page.Controls.Add(_lblJpegQuality);
        page.Controls.Add(_numJpegQuality);
        return page;
    }

    private Panel BuildEditorTab()
    {
        var page = new Panel();
        var lblEditor = new Label { Text = "Open capture in:", AutoSize = true, Location = new Point(26, 28) };
        _labels.Add(lblEditor);
        _cmbEditor.Location = new Point(180, 24);
        _cmbEditor.Items.AddRange(Enum.GetNames<EditorChoice>());

        _txtCustomEditor.Location = new Point(26, 72);
        _txtCustomEditor.Width = 626;
        _btnBrowseEditor.Location = new Point(662, 70);

        page.Controls.Add(lblEditor);
        page.Controls.Add(_cmbEditor);
        page.Controls.Add(_txtCustomEditor);
        page.Controls.Add(_btnBrowseEditor);
        return page;
    }

    private Panel BuildHotkeysTab()
    {
        var page = new Panel();
        _fullScreenHotkey.Location = new Point(26, 26);
        _regionHotkey.Location = new Point(26, 74);
        _activeWindowHotkey.Location = new Point(26, 122);

        page.Controls.Add(_fullScreenHotkey);
        page.Controls.Add(_regionHotkey);
        page.Controls.Add(_activeWindowHotkey);

        var hint = new Label
        {
            Text = "Click a hotkey box, then press the key combination you want.",
            AutoSize = true,
            Location = new Point(26, 186)
        };
        _mutedLabels.Add(hint);
        page.Controls.Add(hint);
        return page;
    }

    private void LoadFromSettings(AppSettings settings)
    {
        _chkStartWithWindows.Checked = StartupManager.IsEnabled();
        _chkShowNotifications.Checked = settings.ShowNotifications;
        _cmbDefaultAction.SelectedItem = DefaultActionOptions.First(o => o.Value == settings.DefaultAction);
        _cmbTheme.SelectedItem = ThemeOptions.First(o => o.Value == settings.Theme);

        _rbPrompt.Checked = settings.SaveMode == SaveMode.Prompt;
        _rbAutoSave.Checked = settings.SaveMode == SaveMode.AutoSave;
        _txtFolder.Text = settings.AutoSaveFolder;

        _cmbFormat.SelectedItem = FormatOptions.First(o => o.Value == settings.Format);
        _numJpegQuality.Value = settings.JpegQuality;

        _cmbEditor.SelectedItem = settings.Editor.ToString();
        _txtCustomEditor.Text = settings.CustomEditorPath;

        _fullScreenHotkey.Combo = settings.FullScreenHotkey.Clone();
        _regionHotkey.Combo = settings.RegionHotkey.Clone();
        _activeWindowHotkey.Combo = settings.ActiveWindowHotkey.Clone();

        UpdateSaveModeState();
        UpdateFormatState();
        UpdateEditorState();
    }

    private void PreviewTheme()
    {
        var chosen = ((ThemeItem)_cmbTheme.SelectedItem!).Value;
        var palette = chosen switch
        {
            AppTheme.Dark => ThemePalette.Dark,
            AppTheme.Light => ThemePalette.Light,
            _ => ThemeManager.Current
        };
        ApplyTheme(palette);
    }

    private void ApplyTheme(ThemePalette palette)
    {
        _currentPalette = palette;
        BackColor = palette.Surface;
        _tabStrip.BackColor = palette.Canvas;
        _tabContent.BackColor = palette.Canvas;
        RestyleTabButtons(palette);

        foreach (var page in _tabPages)
        {
            page.BackColor = palette.Surface;
        }

        foreach (var divider in _dividers)
        {
            divider.BackColor = palette.Border;
        }

        foreach (var label in _labels)
        {
            label.ForeColor = palette.TextPrimary;
        }

        foreach (var label in _mutedLabels)
        {
            label.ForeColor = palette.TextSecondary;
        }

        ThemeManager.StyleCheckBox(_chkStartWithWindows, palette);
        ThemeManager.StyleCheckBox(_chkShowNotifications, palette);
        _rbPrompt.ForeColor = palette.TextPrimary;
        _rbAutoSave.ForeColor = palette.TextPrimary;
        _lblJpegQuality.ForeColor = palette.TextPrimary;

        ThemeManager.StyleComboBox(_cmbDefaultAction, palette);
        ThemeManager.StyleComboBox(_cmbTheme, palette);
        ThemeManager.StyleComboBox(_cmbFormat, palette);
        ThemeManager.StyleComboBox(_cmbEditor, palette);

        ThemeManager.StyleTextBox(_txtFolder, palette);
        ThemeManager.StyleTextBox(_txtCustomEditor, palette);
        _numJpegQuality.BackColor = palette.InputFill;
        _numJpegQuality.ForeColor = palette.TextPrimary;

        ThemeManager.StyleButton(_btnBrowseFolder, palette);
        ThemeManager.StyleButton(_btnBrowseEditor, palette);
        ThemeManager.StyleButton(_btnOk, palette, emphasized: true);
        ThemeManager.StyleButton(_btnCancel, palette);

        _fullScreenHotkey.ApplyTheme(palette);
        _regionHotkey.ApplyTheme(palette);
        _activeWindowHotkey.ApplyTheme(palette);

        // StyleButton/StyleTabButton force the small (9.5pt) emphasis font; re-apply the scaled Settings
        // font afterwards so buttons and tabs match the rest of this deliberately larger dialog.
        foreach (var button in _tabButtons)
        {
            button.Font = ThemeManager.SettingsEmphasisFont;
        }

        _btnBrowseFolder.Font = ThemeManager.SettingsEmphasisFont;
        _btnBrowseEditor.Font = ThemeManager.SettingsEmphasisFont;
        _btnOk.Font = ThemeManager.SettingsEmphasisFont;
        _btnCancel.Font = ThemeManager.SettingsEmphasisFont;
    }

    private void UpdateSaveModeState()
    {
        var isAuto = _rbAutoSave.Checked;
        _txtFolder.Enabled = isAuto;
        _btnBrowseFolder.Enabled = isAuto;
    }

    private void UpdateFormatState()
    {
        var isJpeg = (_cmbFormat.SelectedItem as FormatItem)?.Value == ImageFormatOption.Jpeg;
        _numJpegQuality.Enabled = isJpeg;
        _lblJpegQuality.Enabled = isJpeg;
    }

    private void UpdateEditorState()
    {
        var isCustom = _cmbEditor.SelectedItem as string == nameof(EditorChoice.Custom);
        _txtCustomEditor.Enabled = isCustom;
        _btnBrowseEditor.Enabled = isCustom;
    }

    private void BrowseFolder()
    {
        using var dialog = new FolderBrowserDialog { SelectedPath = _txtFolder.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtFolder.Text = dialog.SelectedPath;
        }
    }

    private void BrowseEditor()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
            Title = "Choose the editor to open captures in"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _txtCustomEditor.Text = dialog.FileName;
        }
    }

    private void OnOk()
    {
        var combos = new[] { _fullScreenHotkey.Combo, _regionHotkey.Combo, _activeWindowHotkey.Combo };
        var assigned = combos.Where(c => c.IsAssigned).ToList();
        if (assigned.Count != assigned.Distinct().Count())
        {
            MessageBox.Show(this, "Each capture mode needs a different hotkey.", "Duplicate hotkey",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_rbAutoSave.Checked && string.IsNullOrWhiteSpace(_txtFolder.Text))
        {
            MessageBox.Show(this, "Choose a folder to auto-save screenshots to.", "Missing folder",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var chosenAction = ((ActionItem)_cmbDefaultAction.SelectedItem!).Value;
        var chosenEditor = Enum.Parse<EditorChoice>((string)_cmbEditor.SelectedItem!);
        if (chosenAction == DefaultAction.Edit && chosenEditor == EditorChoice.None)
        {
            MessageBox.Show(this,
                "Default action is set to \"Copy + open editor to paste\" but no editor is chosen on the Editor tab. " +
                "Screenshots will still be copied to the clipboard, but no editor will open until you pick one.",
                "No editor configured", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        Result.DefaultAction = chosenAction;
        Result.ShowNotifications = _chkShowNotifications.Checked;
        Result.Theme = ((ThemeItem)_cmbTheme.SelectedItem!).Value;
        Result.SaveMode = _rbAutoSave.Checked ? SaveMode.AutoSave : SaveMode.Prompt;
        Result.AutoSaveFolder = _txtFolder.Text;
        Result.Format = ((FormatItem)_cmbFormat.SelectedItem!).Value;
        Result.JpegQuality = (int)_numJpegQuality.Value;
        Result.Editor = chosenEditor;
        Result.CustomEditorPath = _txtCustomEditor.Text;
        Result.FullScreenHotkey = _fullScreenHotkey.Combo;
        Result.RegionHotkey = _regionHotkey.Combo;
        Result.ActiveWindowHotkey = _activeWindowHotkey.Combo;

        try
        {
            StartupManager.SetEnabled(_chkStartWithWindows.Checked);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not update the Windows startup setting:\n{ex.Message}",
                "Startup setting", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
