using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Check box with a title, drawn in the classic style: a sunken 13 x 13 box with a check mark.
/// A left click (on the box or the text) or Space while focused toggles <see cref="Checked"/>.
/// </summary>
public class CheckBox : Control
{
    private const int BoxSize = 13;
    private const int TextGap = 5; // Between the box and the text.

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

    private string _text = "";
    private bool _checked;
    private bool _autoSize = true;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = new(128, 128, 128);
    private Color _boxColor = Color.White;
    private Color _faceColor = ClassicStyle.Face;
    private Color _highlightColor = ClassicStyle.Highlight;
    private Color _shadowColor = ClassicStyle.Shadow;
    private Color _darkShadowColor = ClassicStyle.DarkShadow;

    public CheckBox()
    {
        Focusable = true;
        UpdateSize();
    }

    public CheckBox(string text)
        : this() => Text = text;

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

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
            UpdateSize();
        }
    }

    /// <summary>Font of the text; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _font = value;
            UpdateSize();
        }
    }

    /// <summary>When true (default), Width and Height follow the box and the text.</summary>
    public bool AutoSize
    {
        get => _autoSize;
        set
        {
            _autoSize = value;
            UpdateSize();
        }
    }

    /// <summary>True while the left button is held after being pressed on the check box.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Text and check mark color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    /// <summary>Text and check mark color when disabled (the text is embossed with <see cref="HighlightColor"/>).</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    /// <summary>Inside of the box.</summary>
    public Color BoxColor
    {
        get => _boxColor;
        set => SetColor(ref _boxColor, value);
    }

    /// <summary>Inside of the box while pressed or disabled, and the inner light edge.</summary>
    public Color FaceColor
    {
        get => _faceColor;
        set => SetColor(ref _faceColor, value);
    }

    public Color HighlightColor
    {
        get => _highlightColor;
        set => SetColor(ref _highlightColor, value);
    }

    public Color ShadowColor
    {
        get => _shadowColor;
        set => SetColor(ref _shadowColor, value);
    }

    public Color DarkShadowColor
    {
        get => _darkShadowColor;
        set => SetColor(ref _darkShadowColor, value);
    }

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left)
        {
            IsPressed = true;
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButton.Left && IsPressed)
        {
            IsPressed = false;
            Invalidate();
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButton.Left)
            Checked = !Checked;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate(); // The pressed look depends on the pointer being over the control.
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Space && e.Modifiers == KeyModifiers.None)
        {
            e.Handled = true;
            Checked = !Checked;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var font = Font;
        var textHeight = font.Ascent + font.Descent;
        var boxY = (Height - BoxSize) / 2;
        var box = new Rect(0, boxY, BoxSize, BoxSize);

        ClassicStyle.DrawSunkenEdge(dc, box, _highlightColor, _faceColor, _shadowColor, _darkShadowColor);

        var pressed = IsPressed && IsMouseOver;
        dc.FillRectangle(box.X + 2, box.Y + 2, BoxSize - 4, BoxSize - 4, enabled && !pressed ? _boxColor : _faceColor);

        var markColor = enabled ? _color : _disabledColor;
        if (_checked)
        {
            for (var row = 0; row < s_checkMark.Length; row++)
            {
                for (var column = 0; column < s_checkMark[row].Length; column++)
                {
                    if (s_checkMark[row][column] == 'X')
                        dc.FillRectangle(box.X + 3 + column, box.Y + 3 + row, 1, 1, markColor);
                }
            }
        }

        if (_text.Length == 0)
            return;
        var textX = BoxSize + TextGap;
        var textY = (Height - textHeight) / 2;
        if (enabled)
        {
            dc.DrawText(_text, font, _color, textX, textY);
        }
        else
        {
            ClassicStyle.DrawEmbossedText(dc, _text, font, _disabledColor, _highlightColor, textX, textY);
        }

        if (Focused)
            ClassicStyle.DrawFocusRectangle(dc, new Rect(textX - 2, textY - 1, font.MeasureText(_text).Width + 4, textHeight + 2), markColor);
    }

    private void UpdateSize()
    {
        if (_autoSize)
        {
            var font = Font;
            var textWidth = _text.Length == 0 ? 0 : TextGap + font.MeasureText(_text).Width + 3; // Room for the focus rectangle.
            Width = BoxSize + textWidth;
            Height = Math.Max(BoxSize + 2, font.Ascent + font.Descent + 4);
        }
        Invalidate();
    }

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}
