using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Base of <see cref="CheckBox"/> and <see cref="RadioButton"/>: a small box (drawn by <see cref="Style.Current"/>) followed by text.
/// A left click (on the box or the text) or Space while focused calls <see cref="OnActivated"/>.
/// </summary>
public abstract class ToggleControl : Control
{
    private const int TextGap = 5; // Between the box and the text.

    private string _text = "";
    private bool _autoSize = true;
    private Font? _font;
    private Color? _color;
    private Color? _disabledColor;
    private Color? _boxColor;

    /// <summary>Makes the control focusable and sizes it to its box.</summary>
    protected ToggleControl()
    {
        Focusable = true;
        UpdateSize();
    }

    /// <summary>Text shown right of the box.</summary>
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

    /// <summary>True while the left button is held after being pressed on the control.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Text and mark color; the style's text color unless set.</summary>
    public Color Color
    {
        get => _color ?? Style.Current.Text;
        set => SetColor(ref _color, value);
    }

    /// <summary>Text and mark color when disabled; the style's disabled text color unless set.</summary>
    public Color DisabledColor
    {
        get => _disabledColor ?? Style.Current.DisabledText;
        set => SetColor(ref _disabledColor, value);
    }

    /// <summary>Inside of the box; the style's field background unless set.</summary>
    public Color BoxColor
    {
        get => _boxColor ?? Style.Current.FieldBackground;
        set => SetColor(ref _boxColor, value);
    }

    /// <summary>Width and height of the box.</summary>
    protected abstract int BoxSize { get; }

    /// <summary>Called on a left click or Space: toggles a check box, selects a radio button.</summary>
    protected abstract void OnActivated();

    /// <summary>
    /// Draws the box (edge, inside and mark) into <paramref name="box"/>, usually with <see cref="Style.Current"/>;
    /// <paramref name="state"/> tells whether it is disabled, under the pointer, pressed and focused.
    /// </summary>
    protected abstract void DrawBox(DrawingContext dc, Rect box, PartState state);

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
            OnActivated();
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
            OnActivated();
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
        var state = enabled ? PartState.Normal : PartState.Disabled;
        if (enabled && IsMouseOver)
            state |= PartState.Hot;
        if (enabled && IsPressed && IsMouseOver)
            state |= PartState.Pressed;
        if (Focused)
            state |= PartState.Focused;
        var boxSize = BoxSize;
        DrawBox(dc, new Rect(0, (Height - boxSize) / 2, boxSize, boxSize), state);

        if (_text.Length == 0)
            return;
        var style = Style.Current;
        var font = Font;
        var textHeight = font.Ascent + font.Descent;
        var textX = boxSize + TextGap;
        var textY = (Height - textHeight) / 2;
        style.DrawText(dc, _text, font, enabled ? Color : DisabledColor, textX, textY, state);

        if (Focused)
        {
            var focus = new Rect(textX - 2, textY - 1, font.MeasureText(_text).Width + 4, textHeight + 2);
            style.DrawFocusRectangle(dc, focus, enabled ? Color : DisabledColor);
        }
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

    private void SetColor(ref Color? field, Color value)
    {
        field = value;
        Invalidate();
    }
}
