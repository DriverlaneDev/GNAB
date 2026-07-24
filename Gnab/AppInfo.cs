using System.Reflection;

namespace Gnab;

internal static class AppInfo
{
    /// <summary>
    /// Version string for display, e.g. "1.0.2". Read from the assembly rather than hardcoded so it
    /// always matches the &lt;Version&gt; in the csproj and can't silently drift from the release tag.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informational = typeof(AppInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip any build metadata the SDK appends (e.g. "1.0.2+9a3f2c1").
            return informational.Split('+')[0];
        }

        var version = typeof(AppInfo).Assembly.GetName().Version;
        return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
