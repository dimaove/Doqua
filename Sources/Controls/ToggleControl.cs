using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Base of <see cref="CheckBox"/> and <see cref="RadioButton"/>: a small classic box followed by text.
/// A left click (on the box or the text) or Space while focused calls <see cref="OnActivated"/>.
/// </summary>
public abstract class ToggleControl : Control
{
    private const int TextGap = 5; // Between the box and the text.

    private string _text = "";
    private bool _autoSize = true;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _boxColor = Color.White;
    private Color _faceColor = ClassicStyle.Face;
    private Color _highlightColor = ClassicStyle.Highlight;
    private Color _shadowColor = ClassicStyle.Shadow;
    private Color _darkShadowColor = ClassicStyle.DarkShadow;

    protected ToggleControl()
    {
        Focusable = true;
        UpdateSize();
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

    /// <summary>True while the left button is held after being pressed on the control.</summary>
    public bool IsPressed { get; private set; }

    /// <summary>Text and mark color.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    /// <summary>Text and mark color when disabled (the text is embossed with <see cref="HighlightColor"/>).</summary>
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

    /// <summary>Width and height of the box.</summary>
    protected abstract int BoxSize { get; }

    /// <summary>Called on a left click or Space: toggles a check box, selects a radio button.</summary>
    protected abstract void OnActivated();

    /// <summary>Draws the box (edge, inside and mark) into <paramref name="box"/>.</summary>
    protected abstract void DrawBox(DrawingContext dc, Rect box, bool enabled, bool pressed);

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
        var boxSize = BoxSize;
        DrawBox(dc, new Rect(0, (Height - boxSize) / 2, boxSize, boxSize), enabled, IsPressed && IsMouseOver);

        if (_text.Length == 0)
            return;
        var font = Font;
        var textHeight = font.Ascent + font.Descent;
        var textX = boxSize + TextGap;
        var textY = (Height - textHeight) / 2;
        if (enabled)
            dc.DrawText(_text, font, _color, textX, textY);
        else
            ClassicStyle.DrawEmbossedText(dc, _text, font, _disabledColor, _highlightColor, textX, textY);

        if (Focused)
        {
            var focus = new Rect(textX - 2, textY - 1, font.MeasureText(_text).Width + 4, textHeight + 2);
            ClassicStyle.DrawFocusRectangle(dc, focus, enabled ? _color : _disabledColor);
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

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}
