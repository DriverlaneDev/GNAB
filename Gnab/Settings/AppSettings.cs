namespace Gnab.Settings;

public class AppSettings
{
    public DefaultAction DefaultAction { get; set; } = DefaultAction.Ask;
    public bool ShowNotifications { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.System;

    public SaveMode SaveMode { get; set; } = SaveMode.Prompt;

    public string AutoSaveFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

    public ImageFormatOption Format { get; set; } = ImageFormatOption.Png;
    public int JpegQuality { get; set; } = 90;

    public EditorChoice Editor { get; set; } = EditorChoice.None;
    public string CustomEditorPath { get; set; } = "";

    public HotkeyCombo FullScreenHotkey { get; set; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.F);
    public HotkeyCombo RegionHotkey { get; set; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.R);
    public HotkeyCombo ActiveWindowHotkey { get; set; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, Keys.W);

    public AppSettings Clone()
    {
        return new AppSettings
        {
            DefaultAction = DefaultAction,
            ShowNotifications = ShowNotifications,
            Theme = Theme,
            SaveMode = SaveMode,
            AutoSaveFolder = AutoSaveFolder,
            Format = Format,
            JpegQuality = JpegQuality,
            Editor = Editor,
            CustomEditorPath = CustomEditorPath,
            FullScreenHotkey = FullScreenHotkey.Clone(),
            RegionHotkey = RegionHotkey.Clone(),
            ActiveWindowHotkey = ActiveWindowHotkey.Clone()
        };
    }
}
