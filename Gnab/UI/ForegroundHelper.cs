using System.Runtime.InteropServices;

namespace Gnab.UI;

internal static class ForegroundHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public static void ForceForeground(Form form)
    {
        form.Activate();
        SetForegroundWindow(form.Handle);
    }
}
