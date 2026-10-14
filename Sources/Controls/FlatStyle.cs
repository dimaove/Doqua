using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A modern flat <see cref="Style"/>: light surfaces, 1-pixel borders with rounded corners, an <see cref="Accent"/>
/// color for selections, checked boxes and the keyboard focus, chevron arrows and thin scroll bars, and no 3D edges.
/// <example>
/// <code>
/// Style.Current = new FlatStyle();                                       // the standard blue accent
/// Style.Current = new FlatStyle { Accent = Color.FromRgb(0x2E8B57) };    // a green accent
/// </code>
/// </example>
/// </summary>
public class FlatStyle : Style
{
    private const float ButtonRadius = 4;
    private const float FieldRadius = 3;

    /// <summary>Creates the flat style with its standard colors.</summary>
    public FlatStyle()
    {
        Face = Color.FromRgb(0xF3F3F3);
        WindowBackground = Face;
        ButtonFace = Color.FromRgb(0xFDFDFD);
        MenuBackground = Color.White;
        Text = Color.FromRgb(0x1B1B1B);
        DisabledText = Color.FromRgb(0xA0A0A0);
        FieldBackground = Color.White;
        DisabledFieldBackground = Color.FromRgb(0xF7F7F7);
        SelectionBackground = Color.FromRgb(0x0067C0);
        SelectionText = Color.White;
        InactiveSelectionBackground = Color.FromRgb(0xE1E1E1);
        TextSelectionBackground = Color.FromRgb(0x0067C0);
        TextSelectionText = Color.White;
        MenuSelectionBackground = Color.FromRgb(0xE3EEFA);
        MenuSelectionText = Color.FromRgb(0x1B1B1B);
        PlaceholderText = Color.FromRgb(0x8A8A8A);
        GridLine = Color.FromRgb(0xEAEAEA);
        TreeLine = Color.FromRgb(0x707070);
        TreeLines = false;
        HighlightFocusedValue = false;
        PressedOffset = 0;
    }

    /// <inheritdoc/>
    public override string Name => "Flat";

    /// <summary>The accent: focus rings, checked boxes, the selected tab, the focused field's underline.</summary>
    public Color Accent { get; init; } = Color.FromRgb(0x0067C0);

    /// <summary>Border of buttons, fields, menus and panels.</summary>
    public Color Border { get; init; } = Color.FromRgb(0xC9C9C9);

    /// <summary>Border under the pointer, and of unchecked boxes.</summary>
    public Color StrongBorder { get; init; } = Color.FromRgb(0x8A8A8A);

    /// <inheritdoc/>
    public override void DrawButton(DrawingContext dc, Rect bounds, string text, Font font, Color face, Color textColor, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        var fill = !enabled ? Color.Lerp(face, Face, 0.6f)
            : state.HasFlag(PartState.Pressed) ? Color.Lerp(face, Accent, 0.16f)
            : state.HasFlag(PartState.Hot) ? Color.Lerp(face, Accent, 0.07f)
            : face;
        var border = !enabled ? Color.Lerp(Border, Face, 0.5f) : state.HasFlag(PartState.Hot) ? StrongBorder : Border;
        if (state.HasFlag(PartState.Focused) && enabled)
        {
            // A 2 px accent ring around the face.
            FillRounded(dc, bounds, ButtonRadius, Accent);
            FillRounded(dc, Inset(bounds, 2), ButtonRadius - 2, fill);
        }
        else
        {
            FillRounded(dc, bounds, ButtonRadius, border);
            FillRounded(dc, Inset(bounds, 1), ButtonRadius - 1, fill);
        }

        if (text.Length > 0)
        {
            var x = bounds.X + (bounds.Width - font.MeasureText(text).Width) / 2;
            var y = bounds.Y + (bounds.Height - (font.Ascent + font.Descent)) / 2;
            dc.DrawText(text, font, textColor, x, y);
        }
    }

    /// <summary>No frame: a light tint under the pointer, a darker one while pressed.</summary>
    public override void DrawToolButton(DrawingContext dc, Rect bounds, PartState state)
    {
        if (state.HasFlag(PartState.Disabled))
            return;
        if (state.HasFlag(PartState.Pressed))
            FillRounded(dc, bounds, 2, new Color(0, 0, 0, 36));
        else if (state.HasFlag(PartState.Hot))
            FillRounded(dc, bounds, 2, new Color(0, 0, 0, 18));
    }

    /// <summary>Chevrons (thin "v" shapes), several side by side for "«".</summary>
    public override void DrawArrow(DrawingContext dc, Rect bounds, ArrowDirection direction, Color color, int count = 1)
    {
        var vertical = direction is ArrowDirection.Up or ArrowDirection.Down;
        for (var arrow = 0; arrow < count; arrow++)
        {
            var along = (arrow - (count - 1) / 2f) * 4;
            var cx = bounds.X + bounds.Width / 2f + (vertical ? 0 : along);
            var cy = bounds.Y + bounds.Height / 2f + (vertical ? along : 0);
            // Tip and the two ends of the chevron.
            var (tipX, tipY, endX1, endY1, endX2, endY2) = direction switch
            {
                ArrowDirection.Down => (cx, cy + 2, cx - 4, cy - 2, cx + 4, cy - 2),
                ArrowDirection.Up => (cx, cy - 2, cx - 4, cy + 2, cx + 4, cy + 2),
                ArrowDirection.Left => (cx - 2, cy, cx + 2, cy - 4, cx + 2, cy + 4),
                _ => (cx + 2, cy, cx - 2, cy - 4, cx - 2, cy + 4),
            };
            dc.DrawLine(endX1, endY1, tipX, tipY, color, 1.4f);
            dc.DrawLine(tipX, tipY, endX2, endY2, color, 1.4f);
        }
    }

    /// <summary>A rounded 1 px border; when focused an accent border with a 2 px accent line at the bottom.</summary>
    public override void DrawField(DrawingContext dc, Rect bounds, Color background, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        var focused = enabled && state.HasFlag(PartState.Focused);
        var border = !enabled ? Color.Lerp(Border, Face, 0.5f) : focused ? Accent : state.HasFlag(PartState.Hot) ? StrongBorder : Border;
        FillRounded(dc, bounds, FieldRadius, border);
        FillRounded(dc, Inset(bounds, 1), FieldRadius - 1, background);
        if (focused)
            dc.FillRectangle(bounds.X + 1, bounds.Bottom - 2, bounds.Width - 2, 1, Accent);
    }

    /// <summary>A rounded box; checked: filled with the accent and a white check mark.</summary>
    public override void DrawCheckBox(DrawingContext dc, Rect box, Color background, Color mark, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        if (state.HasFlag(PartState.Checked))
        {
            var fill = !enabled ? DisabledText : state.HasFlag(PartState.Pressed) ? Color.Lerp(Accent, Color.Black, 0.2f) : Accent;
            FillRounded(dc, box, 3, fill);
            float x = box.X, y = box.Y;
            dc.DrawLine(x + 3, y + 6.5f, x + 5.5f, y + 9, Color.White, 1.6f);
            dc.DrawLine(x + 5.5f, y + 9, x + 10, y + 4, Color.White, 1.6f);
            return;
        }
        var border = !enabled ? Color.Lerp(StrongBorder, Face, 0.6f) : state.HasFlag(PartState.Hot) ? Text : StrongBorder;
        FillRounded(dc, box, 3, border);
        FillRounded(dc, Inset(box, 1), 2, enabled && !state.HasFlag(PartState.Pressed) ? background : Face);
    }

    /// <summary>A circle; selected: an accent ring with an accent dot.</summary>
    public override void DrawRadioButton(DrawingContext dc, Rect box, Color background, Color mark, PartState state)
    {
        var enabled = !state.HasFlag(PartState.Disabled);
        float r = box.Width / 2f, cx = box.X + r, cy = box.Y + r;
        var ring = !enabled ? Color.Lerp(StrongBorder, Face, 0.6f)
            : state.HasFlag(PartState.Checked) ? Accent
            : state.HasFlag(PartState.Hot) ? Text
            : StrongBorder;
        dc.FillEllipse(cx, cy, r, r, ring);
        dc.FillEllipse(cx, cy, r - 1, r - 1, enabled && !state.HasFlag(PartState.Pressed) ? background : Face);
        if (state.HasFlag(PartState.Checked))
            dc.FillEllipse(cx, cy, r - 3, r - 3, enabled ? Accent : DisabledText);
    }

    /// <summary>Plain text, also when disabled (in the disabled color the control passes).</summary>
    public override void DrawText(DrawingContext dc, string text, Font font, Color color, int x, int y, PartState state) =>
        dc.DrawText(text, font, color, x, y);

    /// <summary>A 1 px accent rectangle.</summary>
    public override void DrawFocusRectangle(DrawingContext dc, Rect bounds, Color color) => dc.DrawRectangle(bounds, Accent);

    /// <inheritdoc/>
    public override void DrawTabPanel(DrawingContext dc, Rect bounds, Rect selectedTab, Color face)
    {
        dc.FillRectangle(bounds, Border);
        dc.FillRectangle(Inset(bounds, 1), face);
    }

    /// <summary>Only the selected tab has a frame, joined to the panel, with an accent line on top; others are plain text.</summary>
    public override void DrawTab(DrawingContext dc, Rect bounds, Color face, PartState state)
    {
        if (state.HasFlag(PartState.Selected))
        {
            dc.FillRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height, Border);
            dc.FillRectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height, face);
            dc.FillRectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, 2, state.HasFlag(PartState.Disabled) ? DisabledText : Accent);
        }
        else if (state.HasFlag(PartState.Hot) && !state.HasFlag(PartState.Disabled))
        {
            FillRounded(dc, new Rect(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 3), 3, new Color(0, 0, 0, 14));
        }
    }

    /// <inheritdoc/>
    public override void DrawGroupBox(DrawingContext dc, Rect r, int gapLeft, int gapRight)
    {
        if (r.Width < 2 || r.Height < 2)
            return;
        if (gapLeft < 0)
        {
            dc.FillRectangle(r.X, r.Y, r.Width, 1, Border);
        }
        else
        {
            dc.FillRectangle(r.X, r.Y, Math.Max(0, gapLeft - r.X), 1, Border);
            dc.FillRectangle(gapRight, r.Y, Math.Max(0, r.Right - gapRight), 1, Border);
        }
        dc.FillRectangle(r.X, r.Y, 1, r.Height, Border);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, Border);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, Border);
    }

    /// <summary>A light track and a thin rounded thumb, darker under the pointer or while dragged.</summary>
    public override void DrawScrollBar(DrawingContext dc, Rect track, Rect thumb, bool horizontal, Rect? pressedTrack, PartState thumbState)
    {
        dc.FillRectangle(track, ScrollTrack);
        if (thumbState.HasFlag(PartState.Disabled))
            return;
        var color = thumbState.HasFlag(PartState.Pressed) || thumbState.HasFlag(PartState.Hot) ? StrongBorder : Color.FromRgb(0xC2C2C2);
        var bar = horizontal
            ? new Rect(thumb.X + 1, thumb.Y + 4, Math.Max(0, thumb.Width - 2), Math.Max(0, thumb.Height - 8))
            : new Rect(thumb.X + 4, thumb.Y + 1, Math.Max(0, thumb.Width - 8), Math.Max(0, thumb.Height - 2));
        FillRounded(dc, bar, 4, color);
    }

    /// <summary>Just a small arrow on the track (tinted under the pointer).</summary>
    public override void DrawScrollBarButton(DrawingContext dc, Rect bounds, ArrowDirection direction, PartState state, int arrows = 1)
    {
        dc.FillRectangle(bounds, ScrollTrack);
        DrawToolButton(dc, bounds, state);
        DrawArrow(dc, bounds, direction, state.HasFlag(PartState.Disabled) ? DisabledText : StrongBorder, arrows);
    }

    /// <inheritdoc/>
    public override void DrawScrollBarCorner(DrawingContext dc, Rect bounds) => dc.FillRectangle(bounds, ScrollTrack);

    /// <summary>A light cell with a short separator on the right and a line below.</summary>
    public override void DrawHeaderCell(DrawingContext dc, Rect bounds, PartState state)
    {
        dc.FillRectangle(bounds, state.HasFlag(PartState.Hot) ? Color.FromRgb(0xF0F0F0) : Color.FromRgb(0xFAFAFA));
        dc.FillRectangle(bounds.Right - 1, bounds.Y + 4, 1, Math.Max(0, bounds.Height - 8), Border);
        dc.FillRectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1, Border);
    }

    /// <summary>The face with a short grip line in the middle.</summary>
    public override void DrawSplitter(DrawingContext dc, Rect bounds, bool vertical, PartState state)
    {
        dc.FillRectangle(bounds, Face);
        var grip = state.HasFlag(PartState.Pressed) || state.HasFlag(PartState.Hot) ? Accent : Border;
        if (vertical)
            dc.FillRectangle(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2 - 12, 1, Math.Min(24, bounds.Height), grip);
        else
            dc.FillRectangle(bounds.X + bounds.Width / 2 - 12, bounds.Y + bounds.Height / 2, Math.Min(24, bounds.Width), 1, grip);
    }

    /// <summary>A chevron: right when collapsed, down when expanded.</summary>
    public override void DrawTreeExpander(DrawingContext dc, Rect box, bool expanded, Color background, PartState state) =>
        DrawArrow(dc, box, expanded ? ArrowDirection.Down : ArrowDirection.Right, TreeLine);

    /// <inheritdoc/>
    public override void DrawMenu(DrawingContext dc, Rect bounds, Color background)
    {
        dc.FillRectangle(bounds, Border);
        dc.FillRectangle(Inset(bounds, 1), background);
    }

    /// <summary>A rounded highlight, inset from the menu's edges.</summary>
    public override void DrawMenuHighlight(DrawingContext dc, Rect bounds, Color background) =>
        FillRounded(dc, new Rect(bounds.X + 2, bounds.Y + 1, Math.Max(0, bounds.Width - 4), Math.Max(0, bounds.Height - 2)), 3, background);

    /// <inheritdoc/>
    public override void DrawMenuSeparator(DrawingContext dc, Rect bounds) =>
        dc.FillRectangle(bounds.X + 4, bounds.Y + bounds.Height / 2, Math.Max(0, bounds.Width - 8), 1, GridLine);

    /// <summary>A rounded tint under the pointer, a stronger one while the menu is open.</summary>
    public override void DrawMenuBarItem(DrawingContext dc, Rect bounds, PartState state)
    {
        if (state.HasFlag(PartState.Pressed))
            FillRounded(dc, Inset(bounds, 1), 3, new Color(0, 0, 0, 30));
        else if (state.HasFlag(PartState.Hot) && !state.HasFlag(PartState.Disabled))
            FillRounded(dc, Inset(bounds, 1), 3, new Color(0, 0, 0, 15));
    }

    /// <inheritdoc/>
    public override void DrawPopup(DrawingContext dc, Rect bounds, Color background)
    {
        dc.FillRectangle(bounds, Border);
        dc.FillRectangle(Inset(bounds, 1), background);
    }

    private static Color ScrollTrack => Color.FromRgb(0xF4F4F4);

    /// <summary>Fills <paramref name="r"/> with rounded corners of <paramref name="radius"/> pixels (antialiased).</summary>
    protected static void FillRounded(DrawingContext dc, Rect r, float radius, Color color)
    {
        if (r.Width <= 0 || r.Height <= 0)
            return;
        radius = Math.Clamp(radius, 0, Math.Min(r.Width, r.Height) / 2f);
        var ri = (int)MathF.Ceiling(radius);
        if (ri == 0)
        {
            dc.FillRectangle(r, color);
            return;
        }
        // The middle as three rectangles, then the four corners as quarter circles (whole circles,
        // clipped to the corner squares).
        dc.FillRectangle(r.X + ri, r.Y, Math.Max(0, r.Width - 2 * ri), r.Height, color);
        dc.FillRectangle(r.X, r.Y + ri, ri, Math.Max(0, r.Height - 2 * ri), color);
        dc.FillRectangle(r.Right - ri, r.Y + ri, ri, Math.Max(0, r.Height - 2 * ri), color);
        foreach (var (cornerX, cornerY, cx, cy) in new[]
        {
            (r.X, r.Y, r.X + radius, r.Y + radius),
            (r.Right - ri, r.Y, r.Right - radius, r.Y + radius),
            (r.X, r.Bottom - ri, r.X + radius, r.Bottom - radius),
            (r.Right - ri, r.Bottom - ri, r.Right - radius, r.Bottom - radius),
        })
        {
            using (dc.PushClip(new Rect(cornerX, cornerY, ri, ri)))
                dc.FillEllipse(cx, cy, radius, radius, color);
        }
    }

    /// <summary><paramref name="r"/> made smaller by <paramref name="amount"/> on every side.</summary>
    protected static Rect Inset(Rect r, int amount) =>
        new(r.X + amount, r.Y + amount, Math.Max(0, r.Width - 2 * amount), Math.Max(0, r.Height - 2 * amount));
}
