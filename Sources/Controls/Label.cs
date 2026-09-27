using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Single- or multi-line ('\n') text drawn with one font and color.</summary>
public class Label : Control
{
    private string _text = "";
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = new(160, 160, 160);
    private bool _autoSize = true;

    public string Text
    {
        get => _text;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_text == value)
                return;
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
            if (_font == value)
                return;
            _font = value;
            UpdateSize();
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Text color used when the label is not <see cref="Control.IsEffectivelyEnabled"/>.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set
        {
            _disabledColor = value;
            Invalidate();
        }
    }

    /// <summary>When true (default), Width and Height follow the size of the text.</summary>
    public bool AutoSize
    {
        get => _autoSize;
        set
        {
            if (_autoSize == value)
                return;
            _autoSize = value;
            UpdateSize();
        }
    }

    protected override void OnRender(DrawingContext dc) =>
        dc.DrawText(_text, Font, IsEffectivelyEnabled ? _color : _disabledColor, 0, 0);

    private void UpdateSize()
    {
        if (_autoSize)
        {
            var size = Font.MeasureText(_text);
            Width = size.Width;
            Height = size.Height;
        }
        Invalidate();
    }
}
