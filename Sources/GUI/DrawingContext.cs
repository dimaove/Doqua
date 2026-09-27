using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Draws into a window's framebuffer. Coordinates are local to the control being rendered,
/// and drawing is clipped to that control's bounds.
/// </summary>
public sealed class DrawingContext
{
    private readonly Framebuffer _target;
    private int _offsetX;
    private int _offsetY;
    private Rect _clip;

    internal DrawingContext(Framebuffer target)
    {
        _target = target;
        _clip = new Rect(0, 0, target.Width, target.Height);
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

        var pixels = _target.Pixels;
        var stride = _target.Width;
        if (color.A == 255)
        {
            var pixel = color.ToPixel();
            for (var y = area.Y; y < area.Bottom; y++)
                pixels.AsSpan(y * stride + area.X, area.Width).Fill(pixel);
            return;
        }

        for (var y = area.Y; y < area.Bottom; y++)
        {
            var row = pixels.AsSpan(y * stride + area.X, area.Width);
            for (var i = 0; i < row.Length; i++)
                row[i] = Blend(row[i], color, color.A);
        }
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

    internal void Clear(Color color) => _target.Pixels.AsSpan().Fill(color.ToPixel());

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

    private void DrawGlyph(GlyphBitmap glyph, int x, int y, Color color)
    {
        var glyphRect = new Rect(x, y, glyph.Width, glyph.Height).Offset(_offsetX, _offsetY);
        var area = glyphRect.Intersect(_clip);
        if (area.IsEmpty)
            return;

        var pixels = _target.Pixels;
        var stride = _target.Width;
        for (var py = area.Y; py < area.Bottom; py++)
        {
            var coverage = glyph.Coverage.AsSpan((py - glyphRect.Y) * glyph.Width + (area.X - glyphRect.X), area.Width);
            var row = pixels.AsSpan(py * stride + area.X, area.Width);
            for (var i = 0; i < row.Length; i++)
            {
                if (coverage[i] != 0)
                    row[i] = Blend(row[i], color, coverage[i] * color.A / 255);
            }
        }
    }

    private static uint Blend(uint dst, Color src, int a)
    {
        var ia = 255 - a;
        var r = (src.R * a + (int)((dst >> 16) & 0xFF) * ia) / 255;
        var g = (src.G * a + (int)((dst >> 8) & 0xFF) * ia) / 255;
        var b = (src.B * a + (int)(dst & 0xFF) * ia) / 255;
        return (uint)(r << 16 | g << 8 | b);
    }

    internal readonly record struct State(int OffsetX, int OffsetY, Rect Clip);
}
