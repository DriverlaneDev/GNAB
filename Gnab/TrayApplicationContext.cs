using Gnab.Capture;
using Gnab.Editing;
using Gnab.Hotkeys;
using Gnab.Imaging;
using Gnab.Settings;
using Gnab.UI;

namespace Gnab;

public sealed class TrayApplicationContext : ApplicationContext
{
    private const int HotkeyIdFullScreen = 1;
    private const int HotkeyIdRegion = 2;
    private const int HotkeyIdActiveWindow = 3;

    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyManager _hotkeyManager = new();
    private AppSettings _settings;
    private bool _captureInProgress;

    public TrayApplicationContext()
    {
        _settings = SettingsManager.Load();
        ThemeManager.Refresh(_settings.Theme);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Capture Full Screen", null, (_, _) => Capture(CaptureMode.FullScreen));
        menu.Items.Add("Capture Region", null, (_, _) => Capture(CaptureMode.Region));
        menu.Items.Add("Capture Active Window", null, (_, _) => Capture(CaptureMode.ActiveWindow));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings...", null, (_, _) => OpenSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateTrayIcon(),
            Text = $"GNAB v{AppInfo.Version}",
            ContextMenuStrip = menu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => Capture(CaptureMode.Region);

        RegisterHotkeys();
    }

    private void RegisterHotkeys()
    {
        // Hotkeys now stay registered for the whole time Settings is open (see OpenSettings), so this
        // can run against combos that are already live — e.g. right after ShowDialog closes. Always
        // starting from a clean slate avoids re-registering an already-registered combo, which Windows
        // rejects as a conflict and would wrongly report as "unavailable".
        _hotkeyManager.UnregisterAll();

        var failed = new List<string>();
        TryRegister(HotkeyIdFullScreen, "Full screen", _settings.FullScreenHotkey, CaptureMode.FullScreen, failed);
        TryRegister(HotkeyIdRegion, "Region", _settings.RegionHotkey, CaptureMode.Region, failed);
        TryRegister(HotkeyIdActiveWindow, "Active window", _settings.ActiveWindowHotkey, CaptureMode.ActiveWindow, failed);

        if (failed.Count > 0)
        {
            _trayIcon.ShowBalloonTip(4000, "Hotkey unavailable",
                $"Already in use by another app, so it won't work in GNAB: {string.Join(", ", failed)}. " +
                "Pick a different combination in Settings > Hotkeys.",
                ToolTipIcon.Warning);
        }
    }

    private void TryRegister(int id, string label, HotkeyCombo combo, CaptureMode mode, List<string> failed)
    {
        if (!combo.IsAssigned) return;
        if (!_hotkeyManager.Register(id, combo, () => Capture(mode)))
        {
            failed.Add($"{label} ({combo})");
        }
    }

    private void Capture(CaptureMode mode)
    {
        // A capture already in flight means a modal dialog (region select, the ask prompt, or
        // markup) is open — without this guard, a second hotkey/tray trigger re-enters here,
        // captures that dialog itself, and stacks another one on top of it.
        if (_captureInProgress) return;
        _captureInProgress = true;
        try
        {
            ThemeManager.Refresh(_settings.Theme);

            // Loop so "Retry" on the prompt can re-run the same capture mode — e.g. redo a region drag
            // — without the user pressing the hotkey again. Each pass grabs a fresh capture.
            bool retry;
            do
            {
                using var bmp = mode switch
                {
                    CaptureMode.FullScreen => ScreenCapture.CaptureFullScreen(),
                    CaptureMode.ActiveWindow => ScreenCapture.CaptureActiveWindow(),
                    CaptureMode.Region => CaptureRegionInteractive(),
                    _ => null
                };

                if (bmp == null) return;
                retry = ProcessCapture(bmp);
            }
            while (retry);
        }
        finally
        {
            _captureInProgress = false;
        }
    }

    private Bitmap? CaptureRegionInteractive()
    {
        using var full = ScreenCapture.CaptureFullScreen();
        using var selector = new RegionSelectForm((Bitmap)full.Clone());
        selector.ShowDialog();
        return selector.Result;
    }

    /// <returns>True if the user chose Retry and the caller should re-capture; false when done.</returns>
    private bool ProcessCapture(Bitmap bmp)
    {
        var action = ResolveAction();
        if (action == CaptureAction.Retry) return true;
        if (action == CaptureAction.Cancel) return false;

        if (action == CaptureAction.Markup)
        {
            ProcessMarkup(bmp);
            return false;
        }

        if (action == CaptureAction.CopyText)
        {
            ProcessCopyText(bmp);
            return false;
        }

        string? savedPath = null;
        var shouldSave = action is CaptureAction.Save or CaptureAction.SaveAndCopy;
        var shouldCopy = action is CaptureAction.Copy or CaptureAction.SaveAndCopy or CaptureAction.Edit;
        var copied = false;

        if (shouldSave)
        {
            savedPath = SaveCapture(bmp);
        }

        if (shouldCopy)
        {
            copied = ClipboardHelper.TrySetImage(bmp);
        }

        if (action == CaptureAction.Edit)
        {
            EditorLauncher.OpenBlank(_settings);
        }

        if (_settings.ShowNotifications)
        {
            var message = savedPath
                ?? (shouldCopy ? (copied ? "Copied to clipboard" : "Could not copy to clipboard") : "No changes made");
            var icon = shouldCopy && !copied ? ToolTipIcon.Warning : ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(1500, "Screenshot captured", message, icon);
        }

        return false;
    }

    private CaptureAction ResolveAction()
    {
        if (_settings.DefaultAction != DefaultAction.Ask)
        {
            return _settings.DefaultAction switch
            {
                DefaultAction.Save => CaptureAction.Save,
                DefaultAction.Copy => CaptureAction.Copy,
                DefaultAction.SaveAndCopy => CaptureAction.SaveAndCopy,
                DefaultAction.Edit => CaptureAction.Edit,
                DefaultAction.Markup => CaptureAction.Markup,
                _ => CaptureAction.Cancel
            };
        }

        using var prompt = new CapturePromptForm();
        prompt.ShowDialog();
        return prompt.ChosenAction;
    }

    private void ProcessCopyText(Bitmap bmp)
    {
        OcrService.OcrResult result;
        try
        {
            // RecognizeAsync runs off the UI thread, so blocking here can't deadlock on a sync context.
            result = Task.Run(() => OcrService.RecognizeAsync(bmp)).GetAwaiter().GetResult();
        }
        catch
        {
            // OCR is best-effort; a failure must never take down the capture pipeline.
            _trayIcon.ShowBalloonTip(2500, "Text capture failed",
                "Couldn't read text from that screenshot.", ToolTipIcon.Warning);
            return;
        }

        // The no-result cases below always notify, even when per-capture notifications are off: the user
        // explicitly asked for text, so silence would just read as "it's broken".
        if (!result.EngineAvailable)
        {
            _trayIcon.ShowBalloonTip(6000, "Text capture unavailable",
                "No OCR language is installed. Add one under Windows Settings > Time & language > Language & region.",
                ToolTipIcon.Warning);
            return;
        }

        var text = result.Text.Trim();
        if (text.Length == 0)
        {
            _trayIcon.ShowBalloonTip(2500, "No text found",
                "GNAB didn't find any readable text in that screenshot.", ToolTipIcon.Warning);
            return;
        }

        if (!ClipboardHelper.TrySetText(result.Text))
        {
            _trayIcon.ShowBalloonTip(2500, "Clipboard busy",
                "Found text, but couldn't copy it — try again.", ToolTipIcon.Warning);
            return;
        }

        if (_settings.ShowNotifications)
        {
            var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            var message = words == 1 ? "Copied 1 word to the clipboard" : $"Copied {words} words to the clipboard";
            _trayIcon.ShowBalloonTip(1500, "Text copied", message, ToolTipIcon.Info);
        }
    }

    private void ProcessMarkup(Bitmap bmp)
    {
        using var markupForm = new MarkupForm(bmp);
        markupForm.ShowDialog();

        using var edited = markupForm.EditedImage;
        if (edited == null)
        {
            if (_settings.ShowNotifications)
            {
                _trayIcon.ShowBalloonTip(1500, "Screenshot captured", "Markup discarded", ToolTipIcon.Info);
            }

            return;
        }

        if (markupForm.Outcome == MarkupForm.MarkupOutcome.Save)
        {
            var savedPath = SaveCapture(edited);
            if (_settings.ShowNotifications)
            {
                _trayIcon.ShowBalloonTip(1500, "Screenshot captured", savedPath ?? "No changes made", ToolTipIcon.Info);
            }

            return;
        }

        var copied = ClipboardHelper.TrySetImage(edited);
        if (_settings.ShowNotifications)
        {
            var message = copied ? "Copied to clipboard" : "Could not copy to clipboard";
            _trayIcon.ShowBalloonTip(1500, "Screenshot captured", message, copied ? ToolTipIcon.Info : ToolTipIcon.Warning);
        }
    }

    private string? SaveCapture(Bitmap bmp)
    {
        string? savedPath;
        if (_settings.SaveMode == SaveMode.AutoSave)
        {
            Directory.CreateDirectory(_settings.AutoSaveFolder);
            var fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.{ImageSaver.Extension(_settings.Format)}";
            savedPath = Path.Combine(_settings.AutoSaveFolder, fileName);
            ImageSaver.Save(bmp, savedPath, _settings.Format, _settings.JpegQuality);
        }
        else
        {
            using var sfd = new SaveFileDialog
            {
                FileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.{ImageSaver.Extension(_settings.Format)}",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Filter = ImageSaver.SaveDialogFilter(_settings.Format)
            };
            savedPath = sfd.ShowDialog() == DialogResult.OK ? sfd.FileName : null;
            if (savedPath != null)
            {
                ImageSaver.Save(bmp, savedPath, _settings.Format, _settings.JpegQuality);
            }
        }

        return savedPath;
    }

    private void OpenSettings()
    {
        ThemeManager.Refresh(_settings.Theme);

        using var form = new SettingsForm(_settings);
        // Hotkeys stay live while Settings is open so capture still works — they're only paused for
        // the brief moment a HotkeyField is actively recording a new combination, so that pressing
        // e.g. Ctrl+Shift+F to record doesn't also trigger a live capture.
        form.HotkeyCaptureStarted += _hotkeyManager.UnregisterAll;
        form.HotkeyCaptureEnded += RegisterHotkeys;

        if (form.ShowDialog() == DialogResult.OK)
        {
            _settings = form.Result;
            SettingsManager.Save(_settings);
            ThemeManager.Refresh(_settings.Theme);
        }

        RegisterHotkeys();
    }

    private void ExitApp()
    {
        _hotkeyManager.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }
}
