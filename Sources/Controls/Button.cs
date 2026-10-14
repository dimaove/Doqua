using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Push button with centered text, drawn by <see cref="Style.Current"/> (normal, under the pointer, pressed, focused
/// and disabled looks). Raises <see cref="Click"/> on a left-button click or Enter/Space.
/// </summary>
public class Button : Control
{
    private string _text = "";
    private Font? _font;
    private Color? _color;
    private Color? _background;
    private Color? _disabledColor;

    /// <summary>Creates a 100 x 32 button without text.</summary>
    public Button()
    {
        Focusable = true;
        Width = 100;
        Height = 32;
    }

    /// <summary>Raised when the button is clicked with the left mouse button, or with Enter or Space while it has the focus.</summary>
    public event EventHandler? Click;

    /// <summary>Caption, centered on the button.</summary>
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

    /// <summary>Text color; the style's text color unless set.</summary>
    public Color Color
    {
        get => _color ?? Style.Current.Text;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Face color; the style's button face unless set. The style derives the hover and pressed looks from it.</summary>
    public Color Background
    {
        get => _background ?? Style.Current.ButtonFace;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Text color while the button is not <see cref="Control.IsEffectivelyEnabled"/>; the style's disabled text color unless set.</summary>
    public Color DisabledColor
    {
        get => _disabledColor ?? Style.Current.DisabledText;
        set
        {
            _disabledColor = value;
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

    /// <summary>Draws the button with <see cref="Style.Current"/> (<see cref="Style.DrawButton"/>).</summary>
    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var state = !enabled ? PartState.Disabled : PartState.Normal;
        if (enabled && IsMouseOver)
            state |= PartState.Hot;
        // Pressed look only while the pointer is still over the button, like native buttons.
        if (enabled && IsPressed && IsMouseOver)
            state |= PartState.Pressed;
        if (enabled && Focused)
            state |= PartState.Focused;
        Style.Current.DrawButton(dc, new Rect(0, 0, Width, Height), _text, Font, Background, enabled ? Color : DisabledColor, state);
    }
}
