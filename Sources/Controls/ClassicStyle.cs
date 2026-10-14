using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// The classic (Windows 9x/2000) 3D look, Doqua's default <see cref="Style"/>: grey faces with raised and sunken
/// 2-pixel edges, dotted focus rectangles, embossed disabled text and navy selections. The edge colors are
/// <see cref="Highlight"/>, <see cref="Shadow"/> and <see cref="DarkShadow"/>.
/// </summary>
public class ClassicStyle : Style
{
    /// <summary>Creates the classic style with its standard colors.</summary>
    public ClassicStyle()
    {
        Face = new Color(212, 208, 200);
        ButtonFace = Face;
        MenuBackground = Face;
        WindowBackground = Face;
        Text = Color.Black;
        DisabledText = new Color(128, 128, 128);
        FieldBackground = Color.White;
        DisabledFieldBackground = Face;
        SelectionBackground = new Color(0, 0, 128);
        SelectionText = Color.White;
        InactiveSelectionBackground = new Color(204, 204, 204);
        TextSelectionBackground = new Color(0, 120, 215);
        TextSelectionText = Color.White;
        MenuSelectionBackground = new Color(0, 0, 128);
        MenuSelectionText = Color.White;
        PlaceholderText = new Color(128, 128, 128);
        GridLine = new Color(192, 192, 192);
        TreeLine = new Color(128, 128, 128);
        PressedOffset = 1;
    }

    /// <inheritdoc/>
    public override string Name => "Classic";

    /// <summary>Light edge: top and left of raised parts, bottom and right of sunken ones.</summary>
    public Color Highlight { get; init; } = Color.White;

    /// <summary>Inner dark edge.</summary>
    public Color Shadow { get; init; } = new(128, 128, 128);

    /// <summary>Outer dark edge.</summary>
    public Color DarkShadow { get; init; } = new(64, 64, 64);

    /// <summary>Frame of the focused (default) button and of drop-down lists.</summary>
    public Color Frame { get; init; } = Color.Black;

    // Classic 7 x 7 check mark, drawn 3 px inside the box.
    private static readonly string[] s_checkMark =
    [
        "......X",
        ".....XX",
        "X...XXX",
        "XX.XXX.",
        "XXXXX..",
        ".XXX...",
        "..X....",
    ];

    /// <inheritdoc/>
    public override void DrawButton(DrawingContext dc, Rect bounds, string text, Font font, Color face, Color textColor, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        var pressed = state.HasFlag(PartState.Pressed);
        var focused = state.HasFlag(PartState.Focused);
        var fill = !enabled ? Face : state.HasFlag(PartState.Hot) && !pressed ? Color.Lerp(face, Color.White, 0.25f) : face;

        var edge = bounds;
        if (focused)
        {
            dc.DrawRectangle(edge, Frame); // Classic focused / default button: a black frame.
            edge = Shrink(edge, 1);
        }
        dc.FillRectangle(edge, fill);
        if (pressed)
        {
            dc.DrawRectangle(edge, DarkShadow);
            dc.DrawRectangle(Shrink(edge, 1), Shadow);
        }
        else
        {
            DrawRaisedEdge(dc, edge);
        }

        if (text.Length > 0)
        {
            var textWidth = font.MeasureText(text).Width;
            var shift = pressed ? 1 : 0;
            var x = bounds.X + (bounds.Width - textWidth) / 2 + shift;
            var y = bounds.Y + (bounds.Height - (font.Ascent + font.Descent)) / 2 + shift;
            DrawText(dc, text, font, textColor, x, y, state);
        }
        if (focused)
            DrawFocusRectangle(dc, Shrink(edge, 3), enabled ? textColor : DisabledText);
    }

    /// <inheritdoc/>
    public override void DrawToolButton(DrawingContext dc, Rect bounds, PartState state)
    {
        dc.FillRectangle(bounds, Face);
        if (state.HasFlag(PartState.Pressed))
            dc.DrawRectangle(bounds, Shadow);
        else
            DrawRaisedEdge(dc, bounds);
    }

    /// <summary>7 x 4 pixel triangles, 1 px apart when there are several, centred together along the pointing direction.</summary>
    public override void DrawArrow(DrawingContext dc, Rect bounds, ArrowDirection direction, Color color, int count = 1)
    {
        var vertical = direction is ArrowDirection.Up or ArrowDirection.Down;
        for (var arrow = 0; arrow < count; arrow++)
        {
            var along = (arrow - (count - 1) / 2.0) * 5;
            var centerX = bounds.X + bounds.Width / 2 + (vertical ? 0 : (int)along);
            var centerY = bounds.Y + bounds.Height / 2 + (vertical ? (int)along : 0);
            for (var i = 0; i < 4; i++)
            {
                // Row (or column) i of the triangle, from its tip: 1, 3, 5, 7 pixels.
                var half = direction is ArrowDirection.Up or ArrowDirection.Left ? i : 3 - i;
                if (vertical)
                    dc.FillRectangle(centerX - half, centerY - 2 + i, 2 * half + 1, 1, color);
                else
                    dc.FillRectangle(centerX - 2 + i, centerY - half, 1, 2 * half + 1, color);
            }
        }
    }

    /// <inheritdoc/>
    public override void DrawField(DrawingContext dc, Rect bounds, Color background, PartState state)
    {
        dc.FillRectangle(bounds, background);
        DrawSunkenEdge(dc, bounds);
    }

    /// <inheritdoc/>
    public override void DrawCheckBox(DrawingContext dc, Rect box, Color background, Color mark, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        DrawSunkenEdge(dc, box);
        dc.FillRectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 4, enabled && !state.HasFlag(PartState.Pressed) ? background : Face);
        if (!state.HasFlag(PartState.Checked))
            return;
        for (var row = 0; row < s_checkMark.Length; row++)
        {
            for (var column = 0; column < s_checkMark[row].Length; column++)
            {
                if (s_checkMark[row][column] == 'X')
                    dc.FillRectangle(box.X + 3 + column, box.Y + 3 + row, 1, 1, mark);
            }
        }
    }

    /// <inheritdoc/>
    public override void DrawRadioButton(DrawingContext dc, Rect box, Color background, Color mark, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        float r = box.Width / 2f, cx = box.X + r, cy = box.Y + r;
        dc.FillEllipse(cx, cy, r, r, Highlight);                              // Outer ring, bottom-right.
        dc.FillEllipse(cx - 0.5f, cy - 0.5f, r - 0.5f, r - 0.5f, Shadow);     // Outer ring, top-left.
        dc.FillEllipse(cx, cy, r - 1, r - 1, Face);                           // Inner ring, bottom-right.
        dc.FillEllipse(cx - 0.5f, cy - 0.5f, r - 1.5f, r - 1.5f, DarkShadow); // Inner ring, top-left.
        dc.FillEllipse(cx, cy, r - 2, r - 2, enabled && !state.HasFlag(PartState.Pressed) ? background : Face);
        if (state.HasFlag(PartState.Checked))
            dc.FillEllipse(cx, cy, 2, 2, mark);
    }

    /// <summary>Enabled text plainly; disabled text engraved: grey with a highlight copy 1 px down and right.</summary>
    public override void DrawText(DrawingContext dc, string text, Font font, Color color, int x, int y, PartState state)
    {
        if (state.HasFlag(PartState.Disabled))
            dc.DrawText(text, font, Highlight, x + 1, y + 1);
        dc.DrawText(text, font, color, x, y);
    }

    /// <summary>A dotted rectangle (every other pixel).</summary>
    public override void DrawFocusRectangle(DrawingContext dc, Rect bounds, Color color)
    {
        for (var x = bounds.X; x < bounds.Right; x += 2)
        {
            dc.FillRectangle(x, bounds.Y, 1, 1, color);
            dc.FillRectangle(x, bounds.Bottom - 1, 1, 1, color);
        }
        for (var y = bounds.Y; y < bounds.Bottom; y += 2)
        {
            dc.FillRectangle(bounds.X, y, 1, 1, color);
            dc.FillRectangle(bounds.Right - 1, y, 1, 1, color);
        }
    }

    /// <inheritdoc/>
    public override void DrawTabPanel(DrawingContext dc, Rect bounds, Rect selectedTab, Color face)
    {
        dc.FillRectangle(bounds, face);
        DrawRaisedEdge(dc, bounds);
    }

    /// <summary>Raised tab with rounded top corners (the corner pixels are cut and replaced by one diagonal pixel).</summary>
    public override void DrawTab(DrawingContext dc, Rect r, Color face, PartState state)
    {
        dc.FillRectangle(r.X + 1, r.Y + 1, r.Width - 2, r.Height - 1, face);
        dc.FillRectangle(r.X + 2, r.Y, r.Width - 4, 1, Highlight);           // Top edge.
        dc.FillRectangle(r.X + 1, r.Y + 1, 1, 1, Highlight);                 // Top-left corner.
        dc.FillRectangle(r.X, r.Y + 2, 1, r.Height - 2, Highlight);          // Left edge.
        dc.FillRectangle(r.Right - 2, r.Y + 1, 1, 1, DarkShadow);            // Top-right corner.
        dc.FillRectangle(r.Right - 1, r.Y + 2, 1, r.Height - 2, DarkShadow); // Right edge, outer.
        dc.FillRectangle(r.Right - 2, r.Y + 2, 1, r.Height - 2, Shadow);     // Right edge, inner.
    }

    /// <summary>An etched frame: a shadow outline and a highlight outline 1 px below and right of it.</summary>
    public override void DrawGroupBox(DrawingContext dc, Rect r, int gapLeft, int gapRight)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        foreach (var (offset, color) in new[] { (0, Shadow), (1, Highlight) })
        {
            int left = r.X + offset, top = r.Y + offset, right = r.Right - 2 + offset, bottom = r.Bottom - 2 + offset;
            if (gapLeft < 0)
            {
                dc.FillRectangle(left, top, right - left + 1, 1, color);
            }
            else
            {
                dc.FillRectangle(left, top, Math.Max(0, gapLeft - left), 1, color);
                dc.FillRectangle(gapRight, top, Math.Max(0, right - gapRight + 1), 1, color);
            }
            dc.FillRectangle(left, top, 1, bottom - top + 1, color);    // Left.
            dc.FillRectangle(right, top, 1, bottom - top + 1, color);   // Right.
            dc.FillRectangle(left, bottom, right - left + 1, 1, color); // Bottom.
        }
    }

    /// <summary>Dotted track (face with every other pixel highlighted), a darker held part, a raised thumb.</summary>
    public override void DrawScrollBar(DrawingContext dc, Rect track, Rect thumb, bool horizontal, Rect? pressedTrack, PartState thumbState)
    {
        dc.FillRectangle(track, Face);
        for (var y = track.Y; y < track.Bottom; y++)
        {
            for (var x = track.X + (y & 1); x < track.Right; x += 2)
                dc.FillRectangle(x, y, 1, 1, Highlight);
        }
        if (pressedTrack is { } held && !thumbState.HasFlag(PartState.Disabled))
            dc.FillRectangle(held, DarkShadow);
        dc.FillRectangle(thumb, Face);
        DrawRaisedEdge(dc, thumb);
    }

    /// <inheritdoc/>
    public override void DrawHeaderCell(DrawingContext dc, Rect bounds, PartState state)
    {
        dc.FillRectangle(bounds, Face);
        DrawRaisedEdge(dc, bounds);
    }

    /// <inheritdoc/>
    public override void DrawSplitter(DrawingContext dc, Rect bounds, bool vertical, PartState state) => dc.FillRectangle(bounds, Face);

    /// <summary>The 9 x 9 [+] / [-] box: framed in the tree line color.</summary>
    public override void DrawTreeExpander(DrawingContext dc, Rect box, bool expanded, Color background, PartState state)
    {
        int centerX = box.X + box.Width / 2, centerY = box.Y + box.Height / 2;
        dc.FillRectangle(box, background);
        dc.DrawRectangle(box, TreeLine);
        dc.FillRectangle(centerX - 2, centerY, 5, 1, Text);
        if (!expanded)
            dc.FillRectangle(centerX, centerY - 2, 1, 5, Text);
    }

    /// <inheritdoc/>
    public override void DrawMenu(DrawingContext dc, Rect bounds, Color background)
    {
        dc.FillRectangle(bounds, background);
        DrawRaisedEdge(dc, bounds);
    }

    /// <summary>An engraved line: shadow with a highlight line below.</summary>
    public override void DrawMenuSeparator(DrawingContext dc, Rect bounds)
    {
        var y = bounds.Y + bounds.Height / 2;
        dc.FillRectangle(bounds.X, y - 1, bounds.Width, 1, Shadow);
        dc.FillRectangle(bounds.X, y, bounds.Width, 1, Highlight);
    }

    /// <summary>A thin raised frame under the pointer, a thin sunken one while the menu is open.</summary>
    public override void DrawMenuBarItem(DrawingContext dc, Rect bounds, PartState state)
    {
        if (state.HasFlag(PartState.Pressed))
            DrawThinEdge(dc, bounds, Shadow, Highlight);
        else if (state.HasFlag(PartState.Hot) && !state.HasFlag(PartState.Disabled))
            DrawThinEdge(dc, bounds, Highlight, Shadow);
    }

    /// <summary>A plain black frame, like classic drop-down lists.</summary>
    public override void DrawPopup(DrawingContext dc, Rect bounds, Color background)
    {
        dc.FillRectangle(bounds, background);
        dc.DrawRectangle(bounds, Frame);
    }

    /// <summary>
    /// Raised 2 px edge inside <paramref name="r"/>: highlight on the top and left; dark shadow (outer) and shadow
    /// (inner) on the bottom and right.
    /// </summary>
    protected void DrawRaisedEdge(DrawingContext dc, Rect r)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, Highlight);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, Highlight);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, DarkShadow);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, DarkShadow);
        dc.FillRectangle(r.X + 1, r.Bottom - 2, r.Width - 2, 1, Shadow);
        dc.FillRectangle(r.Right - 2, r.Y + 1, 1, r.Height - 2, Shadow);
    }

    /// <summary>
    /// Sunken 2 px edge inside <paramref name="r"/>: shadow (outer) and dark shadow (inner) on the top and left;
    /// highlight (outer) and face (inner) on the bottom and right.
    /// </summary>
    protected void DrawSunkenEdge(DrawingContext dc, Rect r)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, Shadow);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, Shadow);
        dc.FillRectangle(r.X + 1, r.Y + 1, r.Width - 3, 1, DarkShadow);
        dc.FillRectangle(r.X + 1, r.Y + 1, 1, r.Height - 3, DarkShadow);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, Highlight);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, Highlight);
        dc.FillRectangle(r.X + 1, r.Bottom - 2, r.Width - 2, 1, Face);
        dc.FillRectangle(r.Right - 2, r.Y + 1, 1, r.Height - 2, Face);
    }

    /// <summary>1 px edge: <paramref name="topLeft"/> on the top and left, <paramref name="bottomRight"/> on the bottom and right.</summary>
    protected static void DrawThinEdge(DrawingContext dc, Rect r, Color topLeft, Color bottomRight)
    {
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, topLeft);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, topLeft);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, bottomRight);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, bottomRight);
    }

    /// <summary><paramref name="r"/> made smaller by <paramref name="amount"/> on every side.</summary>
    protected static Rect Shrink(Rect r, int amount) =>
        new(r.X + amount, r.Y + amount, Math.Max(0, r.Width - 2 * amount), Math.Max(0, r.Height - 2 * amount));
}
