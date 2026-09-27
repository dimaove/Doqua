namespace Doqua.GUI;

/// <summary>Image in memory: 32-bit pixels with an alpha channel. Loads PNG files.</summary>
public sealed class Bitmap
{
    /// <summary>Creates a transparent bitmap.</summary>
    public Bitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if ((long)width * height > MaxPixels)
            throw new ArgumentOutOfRangeException(nameof(width), Localization.Get("Doqua.Error.BitmapTooLarge"));
        Width = width;
        Height = height;
        Pixels = new uint[width * height];
    }

    /// <summary>Upper limit for width * height (256 megapixels, 1 GB of pixels).</summary>
    internal const long MaxPixels = 1L << 28;

    public int Width { get; }
    public int Height { get; }

    /// <summary>Pixels as 0xAARRGGBB (not premultiplied), rows from top to bottom.</summary>
    internal uint[] Pixels { get; }

    /// <summary>Loads a PNG file.</summary>
    /// <exception cref="InvalidDataException">The file is not a valid or supported PNG.</exception>
    public static Bitmap Load(string path)
    {
        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    /// <summary>Loads a PNG image from a stream.</summary>
    /// <exception cref="InvalidDataException">The data is not a valid or supported PNG.</exception>
    public static Bitmap Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return PngDecoder.Decode(stream);
    }

    /// <summary>Saves the bitmap as a PNG file (RGB if every pixel is opaque, otherwise RGBA).</summary>
    public void Save(string path)
    {
        using var stream = File.Create(path);
        Save(stream);
    }

    /// <summary>Writes the bitmap to a stream in PNG format.</summary>
    public void Save(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        PngEncoder.Encode(this, stream);
    }

    /// <summary>
    /// Creates a <see cref="DrawingContext"/> that draws into this bitmap: (0, 0) is its top-left pixel
    /// and drawing is clipped to its size. Translucent drawing blends with the bitmap's own alpha.
    /// </summary>
    public DrawingContext CreateDrawingContext() => new(this);

    public Color GetPixel(int x, int y)
    {
        var pixel = Pixels[IndexOf(x, y)];
        return new Color((byte)(pixel >> 16), (byte)(pixel >> 8), (byte)pixel, (byte)(pixel >> 24));
    }

    public void SetPixel(int x, int y, Color color) =>
        Pixels[IndexOf(x, y)] = (uint)(color.A << 24 | color.R << 16 | color.G << 8 | color.B);

    /// <summary>
    /// Draws <paramref name="source"/> (the whole bitmap if null) at its natural size, with its
    /// top-left corner at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public void Draw(DrawingContext dc, int x, int y, Rect? source = null) => dc.DrawBitmap(this, x, y, source);

    /// <summary>Draws <paramref name="source"/> (the whole bitmap if null) scaled to fill <paramref name="destination"/>.</summary>
    public void Draw(DrawingContext dc, Rect destination, Rect? source = null) => dc.DrawBitmap(this, destination, source);

    private int IndexOf(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(x < 0 || x >= Width ? nameof(x) : nameof(y));
        return y * Width + x;
    }
}
