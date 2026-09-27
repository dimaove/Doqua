using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Drawing helpers for the classic (Windows 9x/2000) 3D look shared by the controls.</summary>
internal static class ClassicStyle
{
    /// <summary>Default "button face" color of the classic look.</summary>
    public static readonly Color Face = new(212, 208, 200);

    public static readonly Color Highlight = Color.White;
    public static readonly Color Shadow = new(128, 128, 128);
    public static readonly Color DarkShadow = new(64, 64, 64);

    /// <summary>
    /// Raised 2 px edge inside <paramref name="r"/>: highlight on the top and left; dark shadow
    /// (outer) and shadow (inner) on the bottom and right.
    /// </summary>
    public static void DrawRaisedEdge(DrawingContext dc, Rect r, Color highlight, Color shadow, Color darkShadow)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, highlight);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, highlight);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, darkShadow);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, darkShadow);
        dc.FillRectangle(r.X + 1, r.Bottom - 2, r.Width - 2, 1, shadow);
        dc.FillRectangle(r.Right - 2, r.Y + 1, 1, r.Height - 2, shadow);
    }

    /// <summary>
    /// Sunken 2 px edge inside <paramref name="r"/>: shadow (outer) and dark shadow (inner) on the top
    /// and left; highlight (outer) and face (inner) on the bottom and right.
    /// </summary>
    public static void DrawSunkenEdge(DrawingContext dc, Rect r, Color highlight, Color face, Color shadow, Color darkShadow)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, shadow);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, shadow);
        dc.FillRectangle(r.X + 1, r.Y + 1, r.Width - 3, 1, darkShadow);
        dc.FillRectangle(r.X + 1, r.Y + 1, 1, r.Height - 3, darkShadow);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, highlight);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, highlight);
        dc.FillRectangle(r.X + 1, r.Bottom - 2, r.Width - 2, 1, face);
        dc.FillRectangle(r.Right - 2, r.Y + 1, 1, r.Height - 2, face);
    }

    /// <summary>Focus indicator: a dotted rectangle (every other pixel).</summary>
    public static void DrawFocusRectangle(DrawingContext dc, Rect r, Color color)
    {
        for (var x = r.X; x < r.Right; x += 2)
        {
            dc.FillRectangle(x, r.Y, 1, 1, color);
            dc.FillRectangle(x, r.Bottom - 1, 1, 1, color);
        }
        for (var y = r.Y; y < r.Bottom; y += 2)
        {
            dc.FillRectangle(r.X, y, 1, 1, color);
            dc.FillRectangle(r.Right - 1, y, 1, 1, color);
        }
    }

    /// <summary>Disabled text: grey, with a highlight copy 1 px down and right so it looks engraved.</summary>
    public static void DrawEmbossedText(DrawingContext dc, string text, Font font, Color color, Color highlight, int x, int y)
    {
        dc.DrawText(text, font, highlight, x + 1, y + 1);
        dc.DrawText(text, font, color, x, y);
    }
}
