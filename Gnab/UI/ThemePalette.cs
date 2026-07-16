namespace Gnab.UI;

public sealed class ThemePalette
{
    public required Color Canvas { get; init; }
    public required Color Surface { get; init; }
    public required Color InputFill { get; init; }
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required bool IsDark { get; init; }

    public static readonly ThemePalette Light = new()
    {
        // Canvas is a step darker than Surface (unlike before) so the custom tab strip can tell
        // the selected tab (Surface) apart from the strip/unselected tabs (Canvas) — the old native
        // TabControl didn't need this distinction since light mode never used owner-draw.
        Canvas = Color.FromArgb(0xE4, 0xE4, 0xE4),
        Surface = Color.FromArgb(0xF0, 0xF0, 0xF0),
        InputFill = Color.White,
        Border = Color.FromArgb(0xC7, 0xC7, 0xC7),
        BorderStrong = Color.FromArgb(0x00, 0x67, 0xC0),
        TextPrimary = Color.FromArgb(0x1A, 0x1A, 0x1A),
        TextSecondary = Color.FromArgb(0x6B, 0x6B, 0x6B),
        IsDark = false
    };

    public static readonly ThemePalette Dark = new()
    {
        Canvas = Color.FromArgb(0x0E, 0x11, 0x16),
        Surface = Color.FromArgb(0x17, 0x1B, 0x22),
        InputFill = Color.FromArgb(0x1C, 0x21, 0x2A),
        Border = Color.FromArgb(0x2E, 0x35, 0x41),
        BorderStrong = Color.FromArgb(0x41, 0x4A, 0x59),
        TextPrimary = Color.FromArgb(0xE7, 0xE9, 0xED),
        TextSecondary = Color.FromArgb(0x83, 0x8B, 0x99),
        IsDark = true
    };
}
