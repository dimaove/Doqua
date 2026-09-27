namespace Doqua.GUI.Platform;

/// <summary>Native font engine: finds installed fonts and rasterizes glyphs.</summary>
internal interface IFontBackend
{
    /// <summary>Family used by <see cref="Font.Default"/>.</summary>
    string DefaultFamily { get; }

    /// <summary>Names of the installed font families, distinct, in any order.</summary>
    IEnumerable<string> GetFamilies();

    /// <summary>Opens the closest installed match; unknown families fall back to a system default.</summary>
    IFontFace CreateFace(string family, float size, FontStyle style);
}

/// <summary>A font at a specific pixel size.</summary>
internal interface IFontFace
{
    /// <summary>Pixels from the top of a line to the baseline.</summary>
    int Ascent { get; }

    /// <summary>Pixels from the baseline to the bottom of a line (positive).</summary>
    int Descent { get; }

    /// <summary>Distance between baselines of consecutive lines.</summary>
    int LineHeight { get; }

    GlyphBitmap RenderGlyph(int codepoint);
}

/// <summary>Antialiased glyph image: one coverage byte (0-255) per pixel, rows top to bottom.</summary>
internal sealed class GlyphBitmap
{
    public GlyphBitmap(int width, int height, int left, int top, float advance, byte[] coverage)
    {
        Width = width;
        Height = height;
        Left = left;
        Top = top;
        Advance = advance;
        Coverage = coverage;
    }

    public int Width { get; }
    public int Height { get; }

    /// <summary>Horizontal offset from the pen position to the left edge of the bitmap.</summary>
    public int Left { get; }

    /// <summary>Distance from the baseline up to the top row of the bitmap.</summary>
    public int Top { get; }

    /// <summary>How far the pen moves after this glyph.</summary>
    public float Advance { get; }

    public byte[] Coverage { get; }

    /// <summary>Glyph with nothing to draw, such as a space.</summary>
    public static GlyphBitmap Blank(float advance) => new(0, 0, 0, 0, advance, []);
}
