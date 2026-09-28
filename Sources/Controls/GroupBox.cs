using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A <see cref="Panel"/> with a classic etched frame and a <see cref="Title"/> on its top edge, grouping related
/// controls. Children are placed as in a panel, relative to the group's top-left corner: leave
/// <see cref="ContentTop"/> pixels at the top for the title. Disabling the group disables everything in it.
/// </summary>
public class GroupBox : Panel
{
    private const int TitleIndent = 8;
    private const int TitlePadding = 3;

    private string _title;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;

    /// <summary>Creates a group with a title, 200 x 100 pixels.</summary>
    public GroupBox(string title = "")
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        Width = 200;
        Height = 100;
    }

    /// <summary>Text on the top edge of the frame; empty for a plain frame.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Font of the title; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Color of the title.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Color of the title while disabled.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set
        {
            _disabledColor = value;
            Invalidate();
        }
    }

    /// <summary>Suggested Y for the first row of children: just below the title.</summary>
    public int ContentTop => Font.LineHeight + 8;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var font = Font;
        var frameTop = font.LineHeight / 2;
        var titleWidth = _title.Length == 0 ? 0 : font.MeasureText(_title).Width + 2 * TitlePadding;
        var gapLeft = TitleIndent;
        var gapRight = Math.Min(Width - 2, TitleIndent + titleWidth);

        // Etched frame: a shadow line with a highlight line 1 px below and right of it; the top line is broken for the title.
        var frame = Rect.FromEdges(0, frameTop, Width, Height);
        DrawEtched(dc, frame, titleWidth > 0 ? gapLeft : -1, gapRight);

        if (titleWidth == 0)
            return;
        var x = TitleIndent + TitlePadding;
        if (IsEffectivelyEnabled)
            dc.DrawText(_title, font, _color, x, 0);
        else
            ClassicStyle.DrawEmbossedText(dc, _title, font, _disabledColor, ClassicStyle.Highlight, x, 0);
    }

    /// <summary>
    /// A shadow outline from (0, top) to (Width - 2, Height - 2) and a highlight outline 1 px below and right of it,
    /// both inclusive; the top lines leave out [gapLeft, gapRight) unless gapLeft is negative.
    /// </summary>
    private static void DrawEtched(DrawingContext dc, Rect r, int gapLeft, int gapRight)
    {
        if (r.Width < 4 || r.Height < 4)
            return;
        foreach (var (offset, color) in new[] { (0, ClassicStyle.Shadow), (1, ClassicStyle.Highlight) })
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
}
