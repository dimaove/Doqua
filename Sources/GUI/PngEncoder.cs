using System.Buffers.Binary;
using System.IO.Compression;

namespace Doqua.GUI;

/// <summary>Writes 8-bit RGB or RGBA PNGs, choosing the prediction filter per row.</summary>
internal static class PngEncoder
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    private static readonly uint[] s_crcTable = CreateCrcTable();

    public static void Encode(Bitmap bitmap, Stream stream)
    {
        int width = bitmap.Width, height = bitmap.Height;
        var pixels = bitmap.Pixels;
        var opaque = Array.TrueForAll(pixels, pixel => pixel >> 24 == 255);
        var channels = opaque ? 3 : 4;

        stream.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;                       // Bit depth.
        header[9] = (byte)(opaque ? 2 : 6);  // Color type: RGB or RGBA.
        // Compression, filter and interlace methods are all 0.
        WriteChunk(stream, "IHDR"u8, header);

        var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var rowBytes = width * channels;
            var previous = new byte[rowBytes];
            var current = new byte[rowBytes];
            var candidate = new byte[rowBytes];
            var best = new byte[rowBytes + 1];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = pixels[y * width + x];
                    var offset = x * channels;
                    current[offset] = (byte)(pixel >> 16);
                    current[offset + 1] = (byte)(pixel >> 8);
                    current[offset + 2] = (byte)pixel;
                    if (!opaque)
                        current[offset + 3] = (byte)(pixel >> 24);
                }

                // Pick the filter whose output has the smallest sum of absolute (signed) values:
                // the usual heuristic, as those rows compress best.
                var bestScore = long.MaxValue;
                for (var filter = 0; filter <= 4; filter++)
                {
                    var score = Filter(filter, current, previous, channels, candidate);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best[0] = (byte)filter;
                        candidate.CopyTo(best, 1);
                    }
                }
                zlib.Write(best);
                (previous, current) = (current, previous);
            }
        }
        WriteChunk(stream, "IDAT"u8, compressed.GetBuffer().AsSpan(0, (int)compressed.Length));
        WriteChunk(stream, "IEND"u8, []);
    }

    private static long Filter(int filter, ReadOnlySpan<byte> row, ReadOnlySpan<byte> previous, int bpp, Span<byte> output)
    {
        long score = 0;
        for (var i = 0; i < row.Length; i++)
        {
            byte a = i >= bpp ? row[i - bpp] : (byte)0, b = previous[i], c = i >= bpp ? previous[i - bpp] : (byte)0;
            var prediction = filter switch
            {
                0 => 0,
                1 => a,
                2 => b,
                3 => (a + b) >> 1,
                _ => PngDecoder.Paeth(a, b, c),
            };
            var value = (byte)(row[i] - prediction);
            output[i] = value;
            score += Math.Abs((int)(sbyte)value); // Widen first: Math.Abs((sbyte)-128) overflows.
        }
        return score;
    }

    private static void WriteChunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, data.Length);
        stream.Write(buffer);
        stream.Write(type);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(buffer, ~UpdateCrc(UpdateCrc(0xFFFFFFFF, type), data));
        stream.Write(buffer);
    }

    // CRC-32 (ISO 3309) over the chunk type and data, as required by PNG.
    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
            crc = s_crcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] CreateCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
