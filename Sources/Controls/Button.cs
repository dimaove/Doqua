using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Push button with centered text. Raises <see cref="Click"/> on a left-button click.</summary>
public class Button : Control
{
    private string _text = "";
    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = new(225, 225, 225);
    private Color _borderColor = new(173, 173, 173);
    private Color _disabledColor = new(160, 160, 160);
    private Color _disabledBackground = new(240, 240, 240);
    private Color _disabledBorderColor = new(204, 204, 204);

    public Button()
    {
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

    /// <summary>Face color. Hover and pressed colors are derived from it.</summary>
    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    /// <summary>Text color used when the button is not <see cref="Control.IsEffectivelyEnabled"/>.</summary>
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

    public Color DisabledBorderColor
    {
        get => _disabledBorderColor;
        set
        {
            _disabledBorderColor = value;
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
        var face = !enabled ? _disabledBackground
            : IsPressed && IsMouseOver ? Color.Lerp(_background, Color.Black, 0.15f)
            : IsMouseOver ? Color.Lerp(_background, Color.White, 0.5f)
            : _background;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, face);
        dc.DrawRectangle(bounds, enabled ? _borderColor : _disabledBorderColor);

        if (_text.Length > 0)
        {
            var font = Font;
            var textWidth = font.MeasureText(_text).Width;
            var textHeight = font.Ascent + font.Descent; // Visual height, without line gap.
            dc.DrawText(_text, font, enabled ? _color : _disabledColor, (Width - textWidth) / 2, (Height - textHeight) / 2);
        }
    }
}
