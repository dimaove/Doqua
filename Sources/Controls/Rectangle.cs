using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Rectangle filled with a solid color.</summary>
public class Rectangle : Control
{
    private Color _color = Color.Black;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    protected override void OnRender(DrawingContext dc) => dc.FillRectangle(0, 0, Width, Height, _color);
}
