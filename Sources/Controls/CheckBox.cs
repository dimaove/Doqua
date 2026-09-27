using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Check box with a title, drawn in the classic style: a sunken 13 x 13 box with a check mark.
/// A left click (on the box or the text) or Space while focused toggles <see cref="Checked"/>.
/// </summary>
public class CheckBox : ToggleControl
{
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

    private bool _checked;

    public CheckBox()
    {
    }

    public CheckBox(string text) => Text = text;

    /// <summary>Raised after <see cref="Checked"/> changes, by the user or from code.</summary>
    public event EventHandler? CheckedChanged;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
                return;
            _checked = value;
            Invalidate();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    protected override int BoxSize => 13;

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    protected override void OnActivated() => Checked = !Checked;

    protected override void DrawBox(DrawingContext dc, Rect box, bool enabled, bool pressed)
    {
        ClassicStyle.DrawSunkenEdge(dc, box, HighlightColor, FaceColor, ShadowColor, DarkShadowColor);
        dc.FillRectangle(box.X + 2, box.Y + 2, box.Width - 4, box.Height - 4, enabled && !pressed ? BoxColor : FaceColor);
        if (!_checked)
            return;

        var markColor = enabled ? Color : DisabledColor;
        for (var row = 0; row < s_checkMark.Length; row++)
        {
            for (var column = 0; column < s_checkMark[row].Length; column++)
            {
                if (s_checkMark[row][column] == 'X')
                    dc.FillRectangle(box.X + 3 + column, box.Y + 3 + row, 1, 1, markColor);
            }
        }
    }
}
