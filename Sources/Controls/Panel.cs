using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Container for other controls. Children are positioned by their own X/Y.</summary>
public class Panel : Control
{
    private Color _background = Color.Transparent;

    public Panel() => Children = new ControlCollection(this);

    public ControlCollection Children { get; }

    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    protected override IReadOnlyList<Control> VisualChildren => Children;

    protected override void OnRender(DrawingContext dc) => dc.FillRectangle(0, 0, Width, Height, _background);
}
