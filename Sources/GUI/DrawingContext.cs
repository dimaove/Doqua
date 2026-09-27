using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Draws into a window's framebuffer (in <see cref="Control"/> rendering) or into a <see cref="Bitmap"/>
/// (<see cref="Bitmap.CreateDrawingContext"/>). While rendering a control, coordinates are local to
/// the control and drawing is clipped to its bounds.
/// </summary>
public sealed class DrawingContext
{
    // Pixels are 0xAARRGGBB. A window framebuffer is always opaque, so its alpha byte is ignored;
    // a bitmap keeps real alpha and is blended with the "over" operator.
    private readonly uint[] _pixels;
    private readonly int _stride;
    private readonly bool _hasAlpha;
    private int _offsetX;
    private int _offsetY;
    private Rect _clip;

    internal DrawingContext(Framebuffer target)
        : this(target.Pixels, target.Width, target.Height, hasAlpha: false)
    {
    }

    internal DrawingContext(Bitmap target)
        : this(target.Pixels, target.Width, target.Height, hasAlpha: true)
    {
    }

    private DrawingContext(uint[] pixels, int width, int height, bool hasAlpha)
    {
        _pixels = pixels;
        _stride = width;
        _hasAlpha = hasAlpha;
        _clip = new Rect(0, 0, width, height);
    }

    internal bool IsClipEmpty => _clip.IsEmpty;

    public void FillRectangle(int x, int y, int width, int height, Color color) =>
        FillRectangle(new Rect(x, y, width, height), color);

    public void FillRectangle(Rect rect, Color color)
    {
        if (color.A == 0)
            return;
        var area = rect.Offset(_offsetX, _offsetY).Intersect(_clip);
        if (area.IsEmpty)
            return;

        var pixels = _pixels;
        var stride = _stride;
        if (color.A == 255)
        {
            var pixel = ToOpaquePixel(color);
            for (var y = area.Y; y < area.Bottom; y++)
                pixels.AsSpan(y * stride + area.X, area.Width).Fill(pixel);
            return;
        }

        for (var y = area.Y; y < area.Bottom; y++)
        {
            var row = pixels.AsSpan(y * stride + area.X, area.Width);
            for (var i = 0; i < row.Length; i++)
                row[i] = Blend(row[i], color.R, color.G, color.B, color.A);
        }
    }

    /// <summary>
    /// Draws <paramref name="source"/> of <paramref name="bitmap"/> (the whole bitmap if null) at its
    /// natural size, with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>).
    /// </summary>
    public void DrawBitmap(Bitmap bitmap, int x, int y, Rect? source = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        var sourceRect = ValidateSource(bitmap, source);
        DrawBitmapCore(bitmap, new Rect(x, y, sourceRect.Width, sourceRect.Height), sourceRect);
    }

    /// <summary>
    /// Draws <paramref name="source"/> of <paramref name="bitmap"/> (the whole bitmap if null) scaled to
    /// fill <paramref name="destination"/>, using nearest-neighbor sampling.
    /// </summary>
    public void DrawBitmap(Bitmap bitmap, Rect destination, Rect? source = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        DrawBitmapCore(bitmap, destination, ValidateSource(bitmap, source));
    }

    /// <summary>Draws the outline of <paramref name="rect"/>, inside its bounds.</summary>
    public void DrawRectangle(Rect rect, Color color, int thickness = 1)
    {
        var t = Math.Min(thickness, Math.Min(rect.Width, rect.Height) / 2 + 1);
        if (t <= 0 || rect.IsEmpty)
            return;
        FillRectangle(rect.X, rect.Y, rect.Width, t, color);
        FillRectangle(rect.X, rect.Bottom - t, rect.Width, t, color);
        FillRectangle(rect.X, rect.Y + t, t, rect.Height - 2 * t, color);
        FillRectangle(rect.Right - t, rect.Y + t, t, rect.Height - 2 * t, color);
    }

    /// <summary>
    /// Draws <paramref name="text"/> with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>).
    /// Lines are split by '\n'; see <see cref="Font.MeasureText"/> for the size of the result.
    /// </summary>
    public void DrawText(string text, Font font, Color color, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(font);
        if (color.A == 0 || text.Length == 0)
            return;

        var face = font.Face;
        var baseline = y + face.Ascent;
        float penX = x;
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '\n':
                    baseline += face.LineHeight;
                    penX = x;
                    break;
                case '\r':
                    break;
                default:
                    var glyph = face.GetGlyph(rune.Value);
                    DrawGlyph(glyph, (int)MathF.Round(penX) + glyph.Left, baseline - glyph.Top, color);
                    penX += glyph.Advance;
                    break;
            }
        }
    }

    /// <summary>
    /// Restricts drawing to <paramref name="rect"/> (in local coordinates) until the returned scope is disposed:
    /// <c>using (dc.PushClip(rect)) { ... }</c>
    /// </summary>
    public ClipScope PushClip(Rect rect)
    {
        var saved = new State(_offsetX, _offsetY, _clip);
        _clip = _clip.Intersect(rect.Offset(_offsetX, _offsetY));
        return new ClipScope(this, saved);
    }

    /// <summary>
    /// Sets every pixel inside the current clip to <paramref name="color"/>, without blending.
    /// On a bitmap, <c>Clear(Color.Transparent)</c> erases to transparent.
    /// </summary>
    public void Clear(Color color)
    {
        var area = _clip;
        if (area.IsEmpty)
            return;
        var pixel = _hasAlpha ? (uint)(color.A << 24) | color.ToPixel() : color.ToPixel();
        for (var y = area.Y; y < area.Bottom; y++)
            _pixels.AsSpan(y * _stride + area.X, area.Width).Fill(pixel);
    }

    /// <summary>Moves the origin to <paramref name="bounds"/> and narrows the clip to it.</summary>
    internal State PushBounds(Rect bounds)
    {
        var saved = new State(_offsetX, _offsetY, _clip);
        var absolute = bounds.Offset(_offsetX, _offsetY);
        _offsetX = absolute.X;
        _offsetY = absolute.Y;
        _clip = _clip.Intersect(absolute);
        return saved;
    }

    internal void Restore(State state)
    {
        _offsetX = state.OffsetX;
        _offsetY = state.OffsetY;
        _clip = state.Clip;
    }

    private static Rect ValidateSource(Bitmap bitmap, Rect? source)
    {
        var whole = new Rect(0, 0, bitmap.Width, bitmap.Height);
        if (source is not { } rect)
            return whole;
        if (rect.IsEmpty || rect.Intersect(whole) != rect)
            throw new ArgumentOutOfRangeException(nameof(source), rect, "The source rectangle must be non-empty and inside the bitmap.");
        return rect;
    }

    private void DrawBitmapCore(Bitmap bitmap, Rect destination, Rect source)
    {
        var target = destination.Offset(_offsetX, _offsetY);
        var area = target.Intersect(_clip);
        if (area.IsEmpty)
            return;

        var scaled = target.Width != source.Width || target.Height != source.Height;
        var sourcePixels = bitmap.Pixels;
        var pixels = _pixels;
        var stride = _stride;
        for (var py = area.Y; py < area.Bottom; py++)
        {
            // Nearest neighbor: the source pixel under the center of the destination pixel.
            var dy = py - target.Y;
            var sy = source.Y + (scaled ? (int)((2L * dy + 1) * source.Height / (2L * target.Height)) : dy);
            var sourceRow = sy * bitmap.Width;
            var row = pixels.AsSpan(py * stride, stride);
            for (var px = area.X; px < area.Right; px++)
            {
                var dx = px - target.X;
                var sx = source.X + (scaled ? (int)((2L * dx + 1) * source.Width / (2L * target.Width)) : dx);
                var pixel = sourcePixels[sourceRow + sx];
                var alpha = (int)(pixel >> 24);
                if (alpha == 255)
                    row[px] = _hasAlpha ? pixel : pixel & 0xFFFFFF;
                else if (alpha != 0)
                    row[px] = Blend(row[px], (int)(pixel >> 16) & 0xFF, (int)(pixel >> 8) & 0xFF, (int)pixel & 0xFF, alpha);
            }
        }
    }

    private void DrawGlyph(GlyphBitmap glyph, int x, int y, Color color)
    {
        var glyphRect = new Rect(x, y, glyph.Width, glyph.Height).Offset(_offsetX, _offsetY);
        var area = glyphRect.Intersect(_clip);
        if (area.IsEmpty)
            return;

        var pixels = _pixels;
        var stride = _stride;
        for (var py = area.Y; py < area.Bottom; py++)
        {
            var coverage = glyph.Coverage.AsSpan((py - glyphRect.Y) * glyph.Width + (area.X - glyphRect.X), area.Width);
            var row = pixels.AsSpan(py * stride + area.X, area.Width);
            for (var i = 0; i < row.Length; i++)
            {
                if (coverage[i] != 0)
                    row[i] = Blend(row[i], color.R, color.G, color.B, coverage[i] * color.A / 255);
            }
        }
    }

    private uint ToOpaquePixel(Color color) => _hasAlpha ? 0xFF000000 | color.ToPixel() : color.ToPixel();

    /// <summary>Draws color (r, g, b) with opacity <paramref name="a"/> (0-255) over the pixel <paramref name="dst"/>.</summary>
    private uint Blend(uint dst, int r, int g, int b, int a)
    {
        int dr = (int)(dst >> 16) & 0xFF, dg = (int)(dst >> 8) & 0xFF, db = (int)dst & 0xFF;
        var da = _hasAlpha ? (int)(dst >> 24) : 255;
        var ia = 255 - a;

        if (da == 255) // Opaque destination: the result stays opaque.
        {
            var rgb = (uint)((r * a + dr * ia) / 255 << 16 | (g * a + dg * ia) / 255 << 8 | (b * a + db * ia) / 255);
            return _hasAlpha ? 0xFF000000 | rgb : rgb;
        }
        if (da == 0)
            return (uint)(a << 24 | r << 16 | g << 8 | b);

        // Porter-Duff "over" with non-premultiplied colors: weight each color by its contribution.
        var outA = a + da * ia / 255;
        int sourceWeight = a * 255, destinationWeight = da * ia, total = outA * 255;
        return (uint)(outA << 24
            | (r * sourceWeight + dr * destinationWeight) / total << 16
            | (g * sourceWeight + dg * destinationWeight) / total << 8
            | (b * sourceWeight + db * destinationWeight) / total);
    }

    internal readonly record struct State(int OffsetX, int OffsetY, Rect Clip);

    public readonly struct ClipScope : IDisposable
    {
        private readonly DrawingContext? _context;
        private readonly State _saved;

        internal ClipScope(DrawingContext context, State saved)
        {
            _context = context;
            _saved = saved;
        }

        public void Dispose() => _context?.Restore(_saved);
    }
}
