using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A <see cref="Panel"/> with a frame (etched in the classic style) and a <see cref="Title"/> on its top edge, grouping related
/// controls. Children are placed as in a panel, relative to the group's top-left corner: leave
/// <see cref="ContentTop"/> pixels at the top for the title. Disabling the group disables everything in it.
/// </summary>
public class GroupBox : Panel
{
    private const int TitleIndent = 8;
    private const int TitlePadding = 3;

    private string _title;
    private Font? _font;
    private Color? _color;
    private Color? _disabledColor;

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
        get => _color ?? Style.Current.Text;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Color of the title while disabled.</summary>
    public Color DisabledColor
    {
        get => _disabledColor ?? Style.Current.DisabledText;
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

        // The frame's top line is broken for the title.
        var style = Style.Current;
        style.DrawGroupBox(dc, Rect.FromEdges(0, frameTop, Width, Height), titleWidth > 0 ? gapLeft : -1, gapRight);

        if (titleWidth == 0)
            return;
        var enabled = IsEffectivelyEnabled;
        style.DrawText(dc, _title, font, enabled ? Color : DisabledColor, TitleIndent + TitlePadding, 0, enabled ? PartState.Normal : PartState.Disabled);
    }
}
