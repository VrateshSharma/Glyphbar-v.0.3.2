namespace Glyphbar.Core.Services;

/// <summary>
/// An original 5x7 dot-matrix alphabet, drawn for Glyphbar, so no font has to be licensed or installed.
/// Each glyph is seven rows of five bits, most significant bit on the left.
/// </summary>
public static class DotMatrixFont
{
    public const int Width = 5;
    public const int Height = 7;

    private static readonly Dictionary<char, byte[]> Glyphs = new()
    {
        ['0'] = [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110],
        ['1'] = [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['2'] = [0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111],
        ['3'] = [0b11110, 0b00001, 0b00001, 0b01110, 0b00001, 0b00001, 0b11110],
        ['4'] = [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010],
        ['5'] = [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110],
        ['6'] = [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110],
        ['7'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000],
        ['8'] = [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110],
        ['9'] = [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100],
        ['A'] = [0b01110, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['B'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10001, 0b10001, 0b11110],
        ['C'] = [0b01110, 0b10001, 0b10000, 0b10000, 0b10000, 0b10001, 0b01110],
        ['D'] = [0b11100, 0b10010, 0b10001, 0b10001, 0b10001, 0b10010, 0b11100],
        ['E'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b11111],
        ['F'] = [0b11111, 0b10000, 0b10000, 0b11110, 0b10000, 0b10000, 0b10000],
        ['G'] = [0b01110, 0b10001, 0b10000, 0b10111, 0b10001, 0b10001, 0b01111],
        ['H'] = [0b10001, 0b10001, 0b10001, 0b11111, 0b10001, 0b10001, 0b10001],
        ['I'] = [0b01110, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110],
        ['J'] = [0b00111, 0b00010, 0b00010, 0b00010, 0b00010, 0b10010, 0b01100],
        ['K'] = [0b10001, 0b10010, 0b10100, 0b11000, 0b10100, 0b10010, 0b10001],
        ['L'] = [0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b10000, 0b11111],
        ['M'] = [0b10001, 0b11011, 0b10101, 0b10101, 0b10001, 0b10001, 0b10001],
        ['N'] = [0b10001, 0b11001, 0b10101, 0b10011, 0b10001, 0b10001, 0b10001],
        ['O'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['P'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10000, 0b10000, 0b10000],
        ['Q'] = [0b01110, 0b10001, 0b10001, 0b10001, 0b10101, 0b10010, 0b01101],
        ['R'] = [0b11110, 0b10001, 0b10001, 0b11110, 0b10100, 0b10010, 0b10001],
        ['S'] = [0b01111, 0b10000, 0b10000, 0b01110, 0b00001, 0b00001, 0b11110],
        ['T'] = [0b11111, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100, 0b00100],
        ['U'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110],
        ['V'] = [0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01010, 0b00100],
        ['W'] = [0b10001, 0b10001, 0b10001, 0b10101, 0b10101, 0b11011, 0b10001],
        ['X'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b01010, 0b10001, 0b10001],
        ['Y'] = [0b10001, 0b10001, 0b01010, 0b00100, 0b00100, 0b00100, 0b00100],
        ['Z'] = [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b11111],
        [':'] = [0b00000, 0b00100, 0b00100, 0b00000, 0b00100, 0b00100, 0b00000],
        ['.'] = [0b00000, 0b00000, 0b00000, 0b00000, 0b00000, 0b00110, 0b00110],
        ['-'] = [0b00000, 0b00000, 0b00000, 0b11111, 0b00000, 0b00000, 0b00000],
        ['/'] = [0b00001, 0b00001, 0b00010, 0b00100, 0b01000, 0b10000, 0b10000],
        ['%'] = [0b11001, 0b11001, 0b00010, 0b00100, 0b01000, 0b10011, 0b10011],
        [' '] = [0, 0, 0, 0, 0, 0, 0],
    };

    public static bool Supports(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c));

    /// <summary>Lit dots for a string as (column, row), with one blank column between glyphs.</summary>
    public static (List<(int X, int Y)> Dots, int Columns) Layout(string text)
    {
        var dots = new List<(int, int)>();
        int cursor = 0;
        foreach (char raw in text)
        {
            if (!Glyphs.TryGetValue(char.ToUpperInvariant(raw), out var rows)) rows = Glyphs[' '];
            // Punctuation like ':' uses only the middle columns, so it is drawn narrow.
            bool narrow = raw is ':' or '.';
            int advance = narrow ? 3 : Width;
            int skip = narrow ? 1 : 0;
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < advance; x++)
                    if ((rows[y] >> (Width - 1 - (x + skip)) & 1) != 0)
                        dots.Add((cursor + x, y));
            cursor += advance + 1;
        }
        return (dots, Math.Max(0, cursor - 1));
    }
}
