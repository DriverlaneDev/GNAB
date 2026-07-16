using System.Reflection;

namespace Gnab.UI;

public static class IconFactory
{
    public static Icon CreateTrayIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("gnab.ico");
        if (stream == null)
        {
            return SystemIcons.Application;
        }

        return new Icon(stream);
    }
}
