using System.Runtime.InteropServices;

namespace Gnab.UI;

internal static class BrandColors
{
    public static readonly Color Accent = ColorTranslator.FromHtml("#C9F24E");
    public static readonly Color Dark = ColorTranslator.FromHtml("#0E1116");

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public static void ApplyBrandTitleBar(Form form)
    {
        if (!form.IsHandleCreated) return;
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        SetAttributeColor(form, DwmwaCaptionColor, Accent);
        SetAttributeColor(form, DwmwaTextColor, Dark);
    }

    private static void SetAttributeColor(Form form, int attribute, Color color)
    {
        var colorRef = color.R | (color.G << 8) | (color.B << 16);
        DwmSetWindowAttribute(form.Handle, attribute, ref colorRef, sizeof(int));
    }
}
