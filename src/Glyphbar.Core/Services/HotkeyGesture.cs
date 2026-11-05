namespace Glyphbar.Core.Services;

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

/// <summary>A parsed gesture such as "Win+Shift+G". Values match RegisterHotKey's MOD_* and VK_* codes.</summary>
public readonly record struct HotkeyGesture(HotkeyModifiers Modifiers, uint VirtualKey)
{
    private static readonly Dictionary<string, uint> NamedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = 0x20, ["Tab"] = 0x09, ["Enter"] = 0x0D, ["Escape"] = 0x1B, ["Esc"] = 0x1B,
        ["Home"] = 0x24, ["End"] = 0x23, ["PageUp"] = 0x21, ["PageDown"] = 0x22,
        ["Insert"] = 0x2D, ["Delete"] = 0x2E, ["Left"] = 0x25, ["Up"] = 0x26, ["Right"] = 0x27, ["Down"] = 0x28,
    };

    /// <summary>Gestures Windows owns; registering these would break something the user relies on.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Alt+Tab", "Alt+F4", "Alt+Escape", "Control+Escape", "Control+Shift+Escape",
        "Win+L", "Win+D", "Win+E", "Win+R", "Win+I", "Win+X", "Win+Tab", "Win+V", "Win+H", "Win+S", "Win+A", "Win+N", "Win+P", "Win+Space",
        "Win+Shift+S",
    };

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var mods = HotkeyModifiers.None;
        uint? key = null;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToUpperInvariant())
            {
                case "ALT": mods |= HotkeyModifiers.Alt; continue;
                case "CTRL" or "CONTROL": mods |= HotkeyModifiers.Control; continue;
                case "SHIFT": mods |= HotkeyModifiers.Shift; continue;
                case "WIN" or "WINDOWS": mods |= HotkeyModifiers.Win; continue;
            }
            if (key is not null) return false;
            key = ParseKey(raw);
            if (key is null) return false;
        }
        if (key is null || mods == HotkeyModifiers.None) return false;
        gesture = new HotkeyGesture(mods, key.Value);
        return true;
    }

    private static uint? ParseKey(string name)
    {
        if (NamedKeys.TryGetValue(name, out var vk)) return vk;
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0])) return char.ToUpperInvariant(name[0]);
        if (name.Length is 2 or 3 && name[0] is 'F' or 'f' && int.TryParse(name[1..], out int f) && f is >= 1 and <= 24)
            return (uint)(0x70 + f - 1);
        return null;
    }

    public bool IsReserved => Reserved.Contains(ToString());

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Control");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        parts.Add(KeyName());
        return string.Join('+', parts);
    }

    private string KeyName()
    {
        foreach (var (name, vk) in NamedKeys)
            if (vk == VirtualKey && name != "Esc") return name;
        if (VirtualKey is >= 0x70 and <= 0x87) return $"F{VirtualKey - 0x70 + 1}";
        return ((char)VirtualKey).ToString();
    }
}
