using System.Buffers.Binary;
using System.IO.Compression;

namespace Doqua.GUI;

/// <summary>
/// PNG decoder: all color types and bit depths, palette and tRNS transparency, Adam7 interlacing.
/// Gamma and color profile chunks are ignored; CRCs are not checked.
/// </summary>
internal static class PngDecoder
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    // Adam7 passes: first column, first row, column step, row step.
    private static readonly (int X, int Y, int StepX, int StepY)[] s_adam7 =
    [
        (0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2),
    ];

    public static Bitmap Decode(Stream stream)
    {
        try
        {
            return DecodeCore(stream);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException(Localization.Get("Doqua.Error.PngTruncated"), exception);
        }
    }

    private static Bitmap DecodeCore(Stream stream)
    {
        Span<byte> buffer = stackalloc byte[8];
        if (stream.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false) < buffer.Length || !buffer.SequenceEqual(Signature))
            throw new InvalidDataException(Localization.Get("Doqua.Error.NotPng"));

        Header? header = null;
        byte[]? palette = null;
        byte[]? transparency = null;
        var compressed = new MemoryStream();
        while (true)
        {
            stream.ReadExactly(buffer); // Chunk length and type.
            var length = BinaryPrimitives.ReadInt32BigEndian(buffer);
            if (length < 0)
                throw new InvalidDataException(Localization.Get("Doqua.Error.PngChunkLength"));
            var type = BinaryPrimitives.ReadUInt32BigEndian(buffer[4..]);
            var data = new byte[length];
            stream.ReadExactly(data);
            stream.ReadExactly(buffer[..4]); // CRC.

            switch (type)
            {
                case 0x49484452: // IHDR
                    header = Header.Parse(data);
                    break;
                case 0x504C5445: // PLTE
                    palette = data;
                    break;
                case 0x74524E53: // tRNS
                    transparency = data;
                    break;
                case 0x49444154: // IDAT: image data may be split over several chunks.
                    compressed.Write(data);
                    break;
                case 0x49454E44: // IEND
                    if (header is not { } h)
                        throw new InvalidDataException(Localization.Get("Doqua.Error.PngNoHeader"));
                    compressed.Position = 0;
                    return DecodeImage(h, compressed, palette, transparency);
            }
        }
    }

    private static Bitmap DecodeImage(Header header, Stream compressed, byte[]? palette, byte[]? transparency)
    {
        if (header.ColorType == 3 && palette == null)
            throw new InvalidDataException(Localization.Get("Doqua.Error.PngNoPalette"));

        var bitmap = new Bitmap(header.Width, header.Height);
        var converter = new PixelConverter(header, palette, transparency);
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);

        if (!header.Interlaced)
        {
            DecodePass(zlib, bitmap, header, converter, 0, 0, 1, 1);
        }
        else
        {
            foreach (var (x, y, stepX, stepY) in s_adam7)
                DecodePass(zlib, bitmap, header, converter, x, y, stepX, stepY);
        }
        return bitmap;
    }

    /// <summary>Decodes one sub-image (the whole image, or one Adam7 pass) into its pixel positions.</summary>
    private static void DecodePass(Stream zlib, Bitmap bitmap, Header header, PixelConverter converter,
        int startX, int startY, int stepX, int stepY)
    {
        var width = (header.Width - startX + stepX - 1) / stepX;
        var height = (header.Height - startY + stepY - 1) / stepY;
        if (width <= 0 || height <= 0)
            return;

        var bitsPerPixel = header.Channels * header.BitDepth;
        var bytesPerPixel = Math.Max(1, bitsPerPixel / 8);
        var rowBytes = (int)(((long)width * bitsPerPixel + 7) / 8);
        var previous = new byte[rowBytes]; // The row above the first row counts as zeros.
        var current = new byte[rowBytes];
        var pixels = bitmap.Pixels;

        for (var y = 0; y < height; y++)
        {
            var filter = zlib.ReadByte();
            if (filter < 0)
                throw new EndOfStreamException();
            zlib.ReadExactly(current);
            Unfilter(filter, current, previous, bytesPerPixel);

            var row = (startY + y * stepY) * header.Width;
            for (var x = 0; x < width; x++)
                pixels[row + startX + x * stepX] = converter.ToArgb(current, x);

            (previous, current) = (current, previous);
        }
    }

    /// <summary>Reverses the per-row prediction filter (PNG spec, section 9).</summary>
    private static void Unfilter(int filter, Span<byte> row, ReadOnlySpan<byte> previous, int bpp)
    {
        switch (filter)
        {
            case 0: // None
                break;
            case 1: // Sub
                for (var i = bpp; i < row.Length; i++)
                    row[i] += row[i - bpp];
                break;
            case 2: // Up
                for (var i = 0; i < row.Length; i++)
                    row[i] += previous[i];
                break;
            case 3: // Average
                for (var i = 0; i < row.Length; i++)
                    row[i] += (byte)(((i >= bpp ? row[i - bpp] : 0) + previous[i]) >> 1);
                break;
            case 4: // Paeth
                for (var i = 0; i < row.Length; i++)
                    row[i] += Paeth(i >= bpp ? row[i - bpp] : (byte)0, previous[i], i >= bpp ? previous[i - bpp] : (byte)0);
                break;
            default:
                throw new InvalidDataException(Localization.Format("Doqua.Error.PngFilter", filter));
        }
    }

    internal static byte Paeth(byte a, byte b, byte c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private readonly record struct Header(int Width, int Height, int BitDepth, int ColorType, bool Interlaced)
    {
        public int Channels => ColorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };

        public static Header Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length < 13)
                throw new InvalidDataException(Localization.Get("Doqua.Error.PngHeader"));
            var width = BinaryPrimitives.ReadInt32BigEndian(data);
            var height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
            int bitDepth = data[8], colorType = data[9], interlace = data[12];

            if (width <= 0 || height <= 0 || (long)width * height > Bitmap.MaxPixels)
                throw new InvalidDataException(Localization.Format("Doqua.Error.PngSize", width, height));
            var validDepth = colorType switch
            {
                0 => bitDepth is 1 or 2 or 4 or 8 or 16,
                3 => bitDepth is 1 or 2 or 4 or 8,
                2 or 4 or 6 => bitDepth is 8 or 16,
                _ => false,
            };
            if (!validDepth)
                throw new InvalidDataException(Localization.Format("Doqua.Error.PngColorType", colorType, bitDepth));
            if (data[10] != 0 || data[11] != 0 || interlace > 1)
                throw new InvalidDataException(Localization.Get("Doqua.Error.PngMethod"));
            return new Header(width, height, bitDepth, colorType, interlace == 1);
        }
    }

    /// <summary>Turns the samples of one pixel in an unfiltered row into 0xAARRGGBB.</summary>
    private sealed class PixelConverter
    {
        private readonly int _colorType;
        private readonly int _depth;
        private readonly int _channels;
        private readonly byte[]? _palette;
        private readonly byte[]? _paletteAlpha;
        private readonly int _transparentGray = -1; // Gray or RGB value marked transparent by tRNS.
        private readonly (int R, int G, int B) _transparentRgb = (-1, -1, -1);

        public PixelConverter(Header header, byte[]? palette, byte[]? transparency)
        {
            _colorType = header.ColorType;
            _depth = header.BitDepth;
            _channels = header.Channels;
            _palette = palette;
            if (transparency == null)
                return;

            switch (_colorType)
            {
                case 3:
                    _paletteAlpha = transparency;
                    break;
                case 0 when transparency.Length >= 2:
                    _transparentGray = BinaryPrimitives.ReadUInt16BigEndian(transparency);
                    break;
                case 2 when transparency.Length >= 6:
                    _transparentRgb = (BinaryPrimitives.ReadUInt16BigEndian(transparency),
                        BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2)),
                        BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4)));
                    break;
            }
        }

        public uint ToArgb(ReadOnlySpan<byte> row, int x)
        {
            switch (_colorType)
            {
                case 0: // Gray
                {
                    var gray = Sample(row, x, 0);
                    var value = To8Bit(gray);
                    return Argb(gray == _transparentGray ? 0 : 255, value, value, value);
                }
                case 2: // RGB
                {
                    int r = Sample(row, x, 0), g = Sample(row, x, 1), b = Sample(row, x, 2);
                    var alpha = (r, g, b) == _transparentRgb ? 0 : 255;
                    return Argb(alpha, To8Bit(r), To8Bit(g), To8Bit(b));
                }
                case 3: // Palette
                {
                    var index = Sample(row, x, 0);
                    if (index * 3 + 2 >= _palette!.Length)
                        throw new InvalidDataException(Localization.Get("Doqua.Error.PngPaletteIndex"));
                    var alpha = _paletteAlpha != null && index < _paletteAlpha.Length ? _paletteAlpha[index] : 255;
                    return Argb(alpha, _palette[index * 3], _palette[index * 3 + 1], _palette[index * 3 + 2]);
                }
                case 4: // Gray + alpha
                {
                    var value = To8Bit(Sample(row, x, 0));
                    return Argb(To8Bit(Sample(row, x, 1)), value, value, value);
                }
                default: // RGBA
                    return Argb(To8Bit(Sample(row, x, 3)), To8Bit(Sample(row, x, 0)), To8Bit(Sample(row, x, 1)), To8Bit(Sample(row, x, 2)));
            }
        }

        /// <summary>Raw value of one channel at the image's bit depth.</summary>
        private int Sample(ReadOnlySpan<byte> row, int x, int channel)
        {
            switch (_depth)
            {
                case 8:
                    return row[x * _channels + channel];
                case 16:
                    var offset = (x * _channels + channel) * 2;
                    return row[offset] << 8 | row[offset + 1];
                default: // 1, 2 or 4 bits, one channel, packed from the most significant bit.
                    var bit = x * _depth;
                    return (row[bit >> 3] >> (8 - _depth - (bit & 7))) & ((1 << _depth) - 1);
            }
        }

        private int To8Bit(int value) => _depth switch
        {
            8 => value,
            16 => value >> 8,
            _ => value * 255 / ((1 << _depth) - 1),
        };

        private static uint Argb(int a, int r, int g, int b) => (uint)(a << 24 | r << 16 | g << 8 | b);
    }
}
