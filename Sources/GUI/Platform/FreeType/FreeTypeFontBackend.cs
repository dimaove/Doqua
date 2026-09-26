using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.FreeType;

/// <summary>Fonts on Linux: fontconfig finds the font file, FreeType rasterizes glyphs.</summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class FreeTypeFontBackend : IFontBackend
{
    private readonly nint _library;

    public FreeTypeFontBackend()
    {
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("The FreeType backend supports only 64-bit processes.");

        nint library;
        var error = FT.FT_Init_FreeType(&library);
        if (error != 0)
            throw new InvalidOperationException($"FT_Init_FreeType failed with error {error}.");
        _library = library;

        if (Fc.FcInit() == 0)
            throw new InvalidOperationException("Cannot initialize fontconfig.");
    }

    // fontconfig alias resolved to the desktop's default sans-serif font.
    public string DefaultFamily => "sans-serif";

    public IFontFace CreateFace(string family, float size, FontStyle style)
    {
        var (path, index) = FindFontFile(family, style);
        return new FreeTypeFontFace(_library, path, index, size);
    }

    /// <summary>Asks fontconfig for the best match; it falls back to a similar font if the family is missing.</summary>
    private static (string Path, int Index) FindFontFile(string family, FontStyle style)
    {
        var pattern = Fc.FcPatternCreate();
        try
        {
            Fc.FcPatternAddString(pattern, "family", family);
            Fc.FcPatternAddInteger(pattern, "weight", style.HasFlag(FontStyle.Bold) ? Fc.WEIGHT_BOLD : Fc.WEIGHT_REGULAR);
            Fc.FcPatternAddInteger(pattern, "slant", style.HasFlag(FontStyle.Italic) ? Fc.SLANT_ITALIC : Fc.SLANT_ROMAN);
            Fc.FcConfigSubstitute(0, pattern, Fc.MatchPattern);
            Fc.FcDefaultSubstitute(pattern);

            int result;
            var match = Fc.FcFontMatch(0, pattern, &result);
            if (match == 0)
                throw new InvalidOperationException($"No font found for '{family}'.");
            try
            {
                byte* file;
                if (Fc.FcPatternGetString(match, "file", 0, &file) != Fc.ResultMatch)
                    throw new InvalidOperationException($"No font file found for '{family}'.");
                int index;
                if (Fc.FcPatternGetInteger(match, "index", 0, &index) != Fc.ResultMatch)
                    index = 0;
                return (Marshal.PtrToStringUTF8((nint)file)!, index);
            }
            finally
            {
                Fc.FcPatternDestroy(match);
            }
        }
        finally
        {
            Fc.FcPatternDestroy(pattern);
        }
    }
}

[SupportedOSPlatform("linux")]
internal sealed unsafe class FreeTypeFontFace : IFontFace
{
    // Faces are cached for the process lifetime and never freed.
    private readonly FT_FaceRec* _face;

    public FreeTypeFontFace(nint library, string path, int index, float size)
    {
        FT_FaceRec* face;
        var error = FT.FT_New_Face(library, path, index, &face);
        if (error != 0)
            throw new InvalidOperationException($"Cannot open font '{path}' (FreeType error {error}).");

        // Char size in 26.6 points at 72 dpi equals the size in pixels.
        error = FT.FT_Set_Char_Size(face, 0, (nint)MathF.Round(size * 64), 72, 72);
        if (error != 0)
            throw new InvalidOperationException($"Cannot set size {size} for font '{path}' (FreeType error {error}).");
        _face = face;

        var metrics = face->size;
        Ascent = (int)((metrics->ascender + 63) >> 6);
        Descent = (int)((-metrics->descender + 63) >> 6);
        LineHeight = (int)((metrics->height + 32) >> 6);
    }

    public int Ascent { get; }
    public int Descent { get; }
    public int LineHeight { get; }

    public GlyphBitmap RenderGlyph(int codepoint)
    {
        if (FT.FT_Load_Char(_face, (nuint)codepoint, FT.LOAD_RENDER | FT.LOAD_TARGET_LIGHT) != 0)
            return GlyphBitmap.Blank(0);

        var slot = _face->glyph;
        var advance = slot->advanceX / 64f;
        var width = (int)slot->bitmapWidth;
        var height = (int)slot->bitmapRows;
        if (width == 0 || height == 0 || slot->bitmapBuffer == null)
            return GlyphBitmap.Blank(advance);

        // Pitch is the offset between rows; when negative, the top row is last in memory.
        var pitch = slot->bitmapPitch;
        var top = pitch >= 0 ? slot->bitmapBuffer : slot->bitmapBuffer - pitch * (height - 1);

        var coverage = new byte[width * height];
        switch (slot->bitmapPixelMode)
        {
            case FT.PIXEL_MODE_GRAY:
                for (var y = 0; y < height; y++)
                    new ReadOnlySpan<byte>(top + y * pitch, width).CopyTo(coverage.AsSpan(y * width));
                break;

            case FT.PIXEL_MODE_MONO: // Bitmap fonts: one bit per pixel, most significant bit first.
                for (var y = 0; y < height; y++)
                {
                    var row = top + y * pitch;
                    for (var x = 0; x < width; x++)
                        coverage[y * width + x] = (row[x >> 3] & (0x80 >> (x & 7))) != 0 ? (byte)255 : (byte)0;
                }
                break;

            default: // Color (emoji) and LCD modes are not supported yet.
                return GlyphBitmap.Blank(advance);
        }
        return new GlyphBitmap(width, height, slot->bitmapLeft, slot->bitmapTop, advance, coverage);
    }
}
