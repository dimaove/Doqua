using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>Native face for a <see cref="Font"/> with a cache of rendered glyphs. Shared by equal fonts.</summary>
internal sealed class FontFace
{
    private static readonly Dictionary<Font, FontFace> s_faces = new();

    private readonly IFontFace _native;
    private readonly Dictionary<int, GlyphBitmap> _glyphs = new();

    private FontFace(IFontFace native) => _native = native;

    public int Ascent => _native.Ascent;
    public int Descent => _native.Descent;
    public int LineHeight => _native.LineHeight;

    public static FontFace Get(Font font)
    {
        if (!s_faces.TryGetValue(font, out var face))
        {
            face = new FontFace(Application.Platform.Fonts.CreateFace(font.Family, font.Size, font.Style));
            s_faces[font] = face;
        }
        return face;
    }

    public GlyphBitmap GetGlyph(int codepoint)
    {
        if (!_glyphs.TryGetValue(codepoint, out var glyph))
        {
            glyph = _native.RenderGlyph(codepoint);
            _glyphs[codepoint] = glyph;
        }
        return glyph;
    }
}
