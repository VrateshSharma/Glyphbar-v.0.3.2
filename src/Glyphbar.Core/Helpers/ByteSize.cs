using System.Globalization;

namespace Glyphbar.Core.Helpers;

public static class ByteSize
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB", "PB"];

    public static string Format(long bytes, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (bytes < 0) bytes = 0;
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        string format = unit == 0 || value >= 100 ? "0" : "0.#";
        return $"{value.ToString(format, culture)} {Units[unit]}";
    }
}
