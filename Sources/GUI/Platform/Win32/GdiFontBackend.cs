using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.Win32;

[SupportedOSPlatform("windows")]
internal sealed unsafe class GdiFontBackend : IFontBackend
{
    public string DefaultFamily => "Segoe UI";

    /// <summary>Font families from EnumFontFamiliesExW; "@" names (vertical variants for East Asian text) are skipped.</summary>
    public IEnumerable<string> GetFamilies()
    {
        var families = new List<string>();
        var handle = GCHandle.Alloc(families);
        var dc = Gdi32.CreateCompatibleDC(0);
        try
        {
            var logFont = new LOGFONTW { lfCharSet = (byte)Gdi32.DEFAULT_CHARSET }; // Empty name: every family.
            Gdi32.EnumFontFamiliesExW(dc, &logFont, &OnFont, GCHandle.ToIntPtr(handle), 0);
        }
        finally
        {
            Gdi32.DeleteDC(dc);
            handle.Free();
        }
        return families;
    }

    // Note: an exception escaping an [UnmanagedCallersOnly] method terminates the process.
    [UnmanagedCallersOnly]
    private static int OnFont(LOGFONTW* logFont, void* metrics, uint fontType, nint lParam)
    {
        var name = new string(logFont->lfFaceName);
        if (!name.StartsWith('@'))
            ((List<string>)GCHandle.FromIntPtr(lParam).Target!).Add(name);
        return 1;
    }

    // GDI maps unknown family names to a similar installed font itself.
    public IFontFace CreateFace(string family, float size, FontStyle style) => new GdiFontFace(family, size, style);
}

[SupportedOSPlatform("windows")]
internal sealed unsafe class GdiFontFace : IFontFace
{
    private static readonly MAT2 s_identity = new() { eM11 = new FIXED { value = 1 }, eM22 = new FIXED { value = 1 } };

    // Memory DC with the font selected; faces are cached for the process lifetime.
    private readonly nint _dc;

    public GdiFontFace(string family, float size, FontStyle style)
    {
        _dc = Gdi32.CreateCompatibleDC(0);

        // Negative height selects by em size (character height) instead of cell height.
        var font = Gdi32.CreateFontW(
            -Math.Max(1, (int)MathF.Round(size)), 0, 0, 0,
            style.HasFlag(FontStyle.Bold) ? Gdi32.FW_BOLD : Gdi32.FW_NORMAL,
            style.HasFlag(FontStyle.Italic) ? 1u : 0u, 0, 0,
            Gdi32.DEFAULT_CHARSET, Gdi32.OUT_TT_PRECIS, Gdi32.CLIP_DEFAULT_PRECIS,
            Gdi32.ANTIALIASED_QUALITY, Gdi32.DEFAULT_PITCH, family);
        if (_dc == 0 || font == 0)
            throw new InvalidOperationException($"Cannot create GDI font '{family}'.");
        Gdi32.SelectObject(_dc, font);

        TEXTMETRICW metrics;
        Gdi32.GetTextMetricsW(_dc, &metrics);
        Ascent = metrics.tmAscent;
        Descent = metrics.tmDescent;
        LineHeight = metrics.tmHeight + metrics.tmExternalLeading;
    }

    public int Ascent { get; }
    public int Descent { get; }
    public int LineHeight { get; }

    public GlyphBitmap RenderGlyph(int codepoint)
    {
        // GetGlyphOutlineW takes a UTF-16 code unit: characters outside the BMP are not supported yet.
        var ch = codepoint <= 0xFFFF ? (uint)codepoint : 0xFFFD;
        var matrix = s_identity;
        GLYPHMETRICS gm;

        var size = Gdi32.GetGlyphOutlineW(_dc, ch, Gdi32.GGO_GRAY8_BITMAP, &gm, 0, null, &matrix);
        if (size == Gdi32.GDI_ERROR)
            return GlyphBitmap.Blank(0);
        if (size == 0)
            return GlyphBitmap.Blank(gm.gmCellIncX); // Whitespace.

        var buffer = new byte[size];
        fixed (byte* data = buffer)
        {
            if (Gdi32.GetGlyphOutlineW(_dc, ch, Gdi32.GGO_GRAY8_BITMAP, &gm, size, data, &matrix) == Gdi32.GDI_ERROR)
                return GlyphBitmap.Blank(gm.gmCellIncX);
        }

        // GGO_GRAY8_BITMAP: one byte per pixel with values 0..64, rows padded to 4 bytes.
        var width = (int)gm.gmBlackBoxX;
        var height = (int)gm.gmBlackBoxY;
        var pitch = (width + 3) & ~3;
        if (pitch * height > buffer.Length)
            return GlyphBitmap.Blank(gm.gmCellIncX);

        var coverage = new byte[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                coverage[y * width + x] = (byte)Math.Min(255, buffer[y * pitch + x] * 255 / 64);
        }
        return new GlyphBitmap(width, height, gm.gmptGlyphOrigin.x, gm.gmptGlyphOrigin.y, gm.gmCellIncX, coverage);
    }
}
