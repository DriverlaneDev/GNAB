namespace Gnab.Settings;

public class HotkeyCombo
{
    public HotkeyModifiers Modifiers { get; set; }
    public Keys Key { get; set; }

    public HotkeyCombo()
    {
    }

    public HotkeyCombo(HotkeyModifiers modifiers, Keys key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public bool IsAssigned => Key != Keys.None;

    public HotkeyCombo Clone() => new(Modifiers, Key);

    public override string ToString()
    {
        if (!IsAssigned) return "(none)";

        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(FriendlyKeyName(Key));
        return string.Join("+", parts);
    }

    // Keys.ToString() surfaces raw enum names like "Oem3" or "D5" that mean nothing to a user; map the
    // common ones to the character actually printed on the key, falling back to the enum name otherwise.
    private static string FriendlyKeyName(Keys key) => key switch
    {
        Keys.Oemtilde => "`",
        Keys.OemMinus => "-",
        Keys.Oemplus => "=",
        Keys.OemOpenBrackets => "[",
        Keys.OemCloseBrackets => "]",
        Keys.OemPipe => "\\",
        Keys.OemSemicolon => ";",
        Keys.OemQuotes => "'",
        Keys.Oemcomma => ",",
        Keys.OemPeriod => ".",
        Keys.OemQuestion => "/",
        Keys.Space => "Space",
        >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
        >= Keys.NumPad0 and <= Keys.NumPad9 => "Num " + (key - Keys.NumPad0),
        _ => key.ToString()
    };

    public override bool Equals(object? obj) =>
        obj is HotkeyCombo other && other.Modifiers == Modifiers && other.Key == Key;

    public override int GetHashCode() => HashCode.Combine(Modifiers, Key);
}
