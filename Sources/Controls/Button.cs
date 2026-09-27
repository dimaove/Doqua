using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Push button with centered text, drawn in the classic 3D style: a raised edge, a sunken frame while
/// pressed (the text moves 1 px down and right), a black frame and dotted rectangle when focused, and
/// embossed grey text when disabled. Raises <see cref="Click"/> on a left-button click or Enter/Space.
/// </summary>
public class Button : Control
{
    private string _text = "";
    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = ClassicStyle.Face;
    private Color _highlightColor = ClassicStyle.Highlight;
    private Color _shadowColor = ClassicStyle.Shadow;
    private Color _darkShadowColor = ClassicStyle.DarkShadow;
    private Color _focusedBorderColor = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _disabledBackground = ClassicStyle.Face;

    public Button()
    {
        Focusable = true;
        Width = 100;
        Height = 32;
    }

    public event EventHandler? Click;

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _text = value;
            Invalidate();
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
            Invalidate();
        }
    }

    /// <summary>Text color.</summary>
    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Face color; the hover color is derived from it.</summary>
    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Light edge on the top and left.</summary>
    public Color HighlightColor
    {
        get => _highlightColor;
        set
        {
            _highlightColor = value;
            Invalidate();
        }
    }

    /// <summary>Inner dark edge on the bottom and right (and the inner frame while pressed).</summary>
    public Color ShadowColor
    {
        get => _shadowColor;
        set
        {
            _shadowColor = value;
            Invalidate();
        }
    }

    /// <summary>Outer dark edge on the bottom and right (and the outer frame while pressed).</summary>
    public Color DarkShadowColor
    {
        get => _darkShadowColor;
        set
        {
            _darkShadowColor = value;
            Invalidate();
        }
    }

    /// <summary>Outer frame drawn around the button while it has keyboard focus.</summary>
    public Color FocusedBorderColor
    {
        get => _focusedBorderColor;
        set
        {
            _focusedBorderColor = value;
            Invalidate();
        }
    }

    /// <summary>Text color (embossed with <see cref="HighlightColor"/>) used when the button is not <see cref="Control.IsEffectivelyEnabled"/>.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set
        {
            _disabledColor = value;
            Invalidate();
        }
    }

    public Color DisabledBackground
    {
        get => _disabledBackground;
        set
        {
            _disabledBackground = value;
            Invalidate();
        }
    }

    /// <summary>True while the left button is held down after being pressed on this button.</summary>
    public bool IsPressed { get; private set; }

    protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);

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
            OnClick(EventArgs.Empty);
    }

    /// <summary>Enter or Space on a focused button clicks it.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Modifiers == KeyModifiers.None && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            OnClick(EventArgs.Empty);
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

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Invalidate();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        // Pressed look only while the pointer is still over the button, like native buttons.
        var pressed = enabled && IsPressed && IsMouseOver;
        var face = !enabled ? _disabledBackground
            : IsMouseOver && !pressed ? Color.Lerp(_background, Color.White, 0.25f)
            : _background;

        var edge = new Rect(0, 0, Width, Height);
        var focused = enabled && Focused;
        if (focused)
        {
            dc.DrawRectangle(edge, _focusedBorderColor); // Classic focused / default button: a black frame.
            edge = Shrink(edge, 1);
        }
        dc.FillRectangle(edge, face);
        if (pressed)
        {
            dc.DrawRectangle(edge, _darkShadowColor);
            dc.DrawRectangle(Shrink(edge, 1), _shadowColor);
        }
        else
        {
            ClassicStyle.DrawRaisedEdge(dc, edge, _highlightColor, _shadowColor, _darkShadowColor);
        }

        if (_text.Length > 0)
        {
            var font = Font;
            var textWidth = font.MeasureText(_text).Width;
            var textHeight = font.Ascent + font.Descent; // Visual height, without line gap.
            var shift = pressed ? 1 : 0;
            var textX = (Width - textWidth) / 2 + shift;
            var textY = (Height - textHeight) / 2 + shift;
            if (enabled)
                dc.DrawText(_text, font, _color, textX, textY);
            else
                ClassicStyle.DrawEmbossedText(dc, _text, font, _disabledColor, _highlightColor, textX, textY);
        }

        if (focused)
            ClassicStyle.DrawFocusRectangle(dc, Shrink(edge, 3), _color);
    }

    private static Rect Shrink(Rect r, int amount) =>
        new(r.X + amount, r.Y + amount, Math.Max(0, r.Width - 2 * amount), Math.Max(0, r.Height - 2 * amount));
}
