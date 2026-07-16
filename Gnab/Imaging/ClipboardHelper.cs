using System.Runtime.InteropServices;

namespace Gnab.Imaging;

public static class ClipboardHelper
{
    public static bool TrySetImage(Image image)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetImage(image);
                return true;
            }
            catch (ExternalException)
            {
                Thread.Sleep(75);
            }
        }

        return false;
    }
}
