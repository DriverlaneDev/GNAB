using System.Diagnostics;
using System.Runtime.InteropServices;
using Gnab.Settings;

namespace Gnab.Editing;

public static class EditorLauncher
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void OpenBlank(AppSettings settings)
    {
        Process? process;
        try
        {
            process = settings.Editor switch
            {
                EditorChoice.Paint => Process.Start(new ProcessStartInfo("mspaint.exe") { UseShellExecute = true }),
                EditorChoice.Photos => Process.Start(new ProcessStartInfo("ms-photos:") { UseShellExecute = true }),
                EditorChoice.Custom when !string.IsNullOrWhiteSpace(settings.CustomEditorPath) =>
                    Process.Start(new ProcessStartInfo(settings.CustomEditorPath) { UseShellExecute = true }),
                _ => null
            };
        }
        catch
        {
            return;
        }

        if (process == null) return;

        var thread = new Thread(() => PasteIntoWindow(process))
        {
            IsBackground = true
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    private static void PasteIntoWindow(Process process)
    {
        using (process)
        {
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(10);
                var handle = IntPtr.Zero;
                while (DateTime.UtcNow < deadline)
                {
                    process.Refresh();
                    if (process.HasExited) return;

                    handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero) break;

                    Thread.Sleep(150);
                }

                if (handle == IntPtr.Zero) return;

                Thread.Sleep(400);
                SetForegroundWindow(handle);
                Thread.Sleep(150);
                SendKeys.SendWait("^v");
            }
            catch
            {
                // Best-effort: if the target app never gets focus or ignores the paste, the user still has
                // the image on the clipboard to paste manually.
            }
        }
    }
}
