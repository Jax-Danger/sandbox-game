using System.IO.Compression;

namespace Sandbox;

/// <summary>Minimal RGB8 PNG writer/reader-of-size (replaces Magick.NET, which the editor's script assembly cannot reference).</summary>
public static class PngWriter
{
    /// <summary>Writes RGBA8 pixels as an opaque RGB PNG. bottomUp = GL row order (flipped while writing).</summary>
    public static void Write(string path, byte[] rgba, int w, int h, bool bottomUp, bool alpha = false)
    {
        int bpp = alpha ? 4 : 3;
        using var raw = new MemoryStream();
        using (var z = new ZLibStream(raw, CompressionLevel.Fastest, leaveOpen: true))
        {
            byte[] row = new byte[1 + w * bpp];
            for (int y = 0; y < h; y++)
            {
                int sy = bottomUp ? h - 1 - y : y;
                for (int x = 0; x < w; x++)
                {
                    int s = (sy * w + x) * 4;
                    row[1 + x * bpp] = rgba[s]; row[2 + x * bpp] = rgba[s + 1]; row[3 + x * bpp] = rgba[s + 2];
                    if (alpha) row[4 + x * bpp] = rgba[s + 3];
                }
                z.Write(row);
            }
        }
        using var f = File.Create(path);
        f.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        byte[] ihdr = new byte[13];
        BE(ihdr, 0, (uint)w); BE(ihdr, 4, (uint)h); ihdr[8] = 8; ihdr[9] = (byte)(alpha ? 6 : 2);
        Chunk(f, "IHDR", ihdr); Chunk(f, "IDAT", raw.ToArray()); Chunk(f, "IEND", []);
    }

    /// <summary>Reads width/height from a PNG header, or null when the file is not a PNG.</summary>
    public static (int W, int H)? Size(string path)
    {
        byte[] b = new byte[24];
        using var f = File.OpenRead(path);
        if (f.Read(b) < 24 || b[0] != 137 || b[1] != 80) return null;
        return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
    }

    private static void BE(byte[] b, int o, uint v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        byte[] hdr = new byte[8]; BE(hdr, 0, (uint)data.Length);
        for (int i = 0; i < 4; i++) hdr[4 + i] = (byte)type[i];
        s.Write(hdr);
        s.Write(data);
        uint c = Crc(hdr.AsSpan(4), 0xFFFFFFFFu); c = Crc(data, c) ^ 0xFFFFFFFFu;
        byte[] crc = new byte[4]; BE(crc, 0, c); s.Write(crc);
    }

    private static uint[]? _table;
    private static uint Crc(ReadOnlySpan<byte> d, uint c)
    {
        _table ??= Enumerable.Range(0, 256).Select(n => { uint x = (uint)n; for (int k = 0; k < 8; k++) x = (x & 1) != 0 ? 0xEDB88320u ^ (x >> 1) : x >> 1; return x; }).ToArray();
        foreach (byte b in d) c = _table[(c ^ b) & 0xFF] ^ (c >> 8);
        return c;
    }
}
