using System.Buffers.Binary;
using System.IO.Compression;

namespace Glyphbar.Core.Helpers;

/// <summary>Pixel helpers for shell icons, which arrive as 32-bit premultiplied BGRA.</summary>
public static class IconImage
{
    /// <summary>Old icons carry no alpha at all, which would render them invisible.</summary>
    public static void FixMissingAlpha(Span<byte> bgra)
    {
        for (int i = 3; i < bgra.Length; i += 4)
            if (bgra[i] != 0) return;
        for (int i = 3; i < bgra.Length; i += 4)
            bgra[i] = 255;
    }

    public static void Unpremultiply(Span<byte> bgra)
    {
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            int a = bgra[i + 3];
            if (a is 0 or 255) continue;
            bgra[i] = (byte)Math.Min(255, bgra[i] * 255 / a);
            bgra[i + 1] = (byte)Math.Min(255, bgra[i + 1] * 255 / a);
            bgra[i + 2] = (byte)Math.Min(255, bgra[i + 2] * 255 / a);
        }
    }

    /// <summary>Greyscale, lifted so dark icons stay legible on the black bar. Alpha is preserved.</summary>
    public static void ToMonochrome(Span<byte> bgra)
    {
        for (int i = 0; i + 3 < bgra.Length; i += 4)
        {
            double luma = 0.114 * bgra[i] + 0.587 * bgra[i + 1] + 0.299 * bgra[i + 2];
            byte v = (byte)Math.Clamp(90 + luma * 165 / 255, 0, 255);
            bgra[i] = bgra[i + 1] = bgra[i + 2] = v;
        }
    }

    /// <summary>Minimal RGBA PNG encoder, which avoids pulling in an imaging dependency for icons.</summary>
    public static byte[] EncodePng(ReadOnlySpan<byte> bgra, int width, int height)
    {
        if (bgra.Length < width * height * 4) throw new ArgumentException("Buffer too small.", nameof(bgra));

        var raw = new byte[height * (width * 4 + 1)];
        int o = 0;
        for (int y = 0; y < height; y++)
        {
            raw[o++] = 0; // filter: none
            for (int x = 0; x < width; x++)
            {
                int p = (y * width + x) * 4;
                raw[o++] = bgra[p + 2];
                raw[o++] = bgra[p + 1];
                raw[o++] = bgra[p];
                raw[o++] = bgra[p + 3];
            }
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            z.Write(raw);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 6;  // RGBA
        WriteChunk(png, "IHDR"u8, ihdr);
        WriteChunk(png, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream s, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, data.Length);
        s.Write(len);
        s.Write(type);
        s.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFF, type), data) ^ 0xFFFFFFFF;
        BinaryPrimitives.WriteUInt32BigEndian(len, crc);
        s.Write(len);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
