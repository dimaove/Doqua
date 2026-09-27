using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Direction of the bar of a <see cref="SplitContainer"/>.</summary>
public enum SplitOrientation
{
    /// <summary>A vertical bar: Panel1 on the left, Panel2 on the right.</summary>
    Vertical,

    /// <summary>A horizontal bar: Panel1 on top, Panel2 below.</summary>
    Horizontal,
}

/// <summary>
/// Two panels separated by a bar that the user drags to share the space between them. Panel1 keeps its size
/// (<see cref="SplitterDistance"/>) when the container is resized; Panel2 gets the rest. Neither panel becomes
/// smaller than its minimum size.
/// </summary>
public class SplitContainer : Control
{
    private int _requestedDistance = 200; // As set or dragged; the limits apply when laying out.
    private int _splitterWidth = 5;
    private int _panel1MinSize = 50;
    private int _panel2MinSize = 50;
    private SplitOrientation _orientation;
    private int _dragOffset = -1; // Pointer position inside the bar while dragging, -1 otherwise.

    public SplitContainer()
    {
        Panel1 = new Panel();
        Panel2 = new Panel();
        Children = [Panel1, Panel2];
        Panel1.Parent = this;
        Panel2.Parent = this;
        Width = 400;
        Height = 300;
    }

    /// <summary>Left (or top) panel: its size is <see cref="SplitterDistance"/>.</summary>
    public Panel Panel1 { get; }

    /// <summary>Right (or bottom) panel: the remaining space.</summary>
    public Panel Panel2 { get; }

    /// <summary>Raised after the user or the program moved the bar.</summary>
    public event EventHandler? SplitterMoved;

    public SplitOrientation Orientation
    {
        get => _orientation;
        set
        {
            _orientation = value;
            LayoutPanels();
        }
    }

    /// <summary>
    /// Width (or height) of <see cref="Panel1"/> in pixels, kept within the minimum sizes. The value set is
    /// remembered: a Panel1 squeezed by a small container gets its size back when the container grows.
    /// </summary>
    public int SplitterDistance
    {
        get => ClampDistance(_requestedDistance);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            var before = SplitterDistance;
            _requestedDistance = value;
            LayoutPanels();
            if (SplitterDistance != before)
                OnSplitterMoved(EventArgs.Empty);
        }
    }

    /// <summary>Thickness of the bar (default 5 pixels).</summary>
    public int SplitterWidth
    {
        get => _splitterWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _splitterWidth = value;
            LayoutPanels();
        }
    }

    public int Panel1MinSize
    {
        get => _panel1MinSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _panel1MinSize = value;
            LayoutPanels();
        }
    }

    public int Panel2MinSize
    {
        get => _panel2MinSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _panel2MinSize = value;
            LayoutPanels();
        }
    }

    private IReadOnlyList<Control> Children { get; }

    private bool IsVertical => _orientation == SplitOrientation.Vertical;

    /// <summary>Length along which the panels are split: the width for a vertical bar.</summary>
    private int Length => IsVertical ? Width : Height;

    private Rect BarBounds => IsVertical
        ? new Rect(SplitterDistance, 0, _splitterWidth, Height)
        : new Rect(0, SplitterDistance, Width, _splitterWidth);

    protected override IReadOnlyList<Control> VisualChildren => Children;

    protected virtual void OnSplitterMoved(EventArgs e) => SplitterMoved?.Invoke(this, e);

    protected override void OnSizeChanged(EventArgs e)
    {
        LayoutPanels(); // Panel1 keeps its size; only a container too small for it squeezes it.
        base.OnSizeChanged(e);
    }

    protected override Cursor GetCursor(int x, int y) =>
        _dragOffset >= 0 || BarBounds.Contains(x, y) ? (IsVertical ? Cursor.SizeWE : Cursor.SizeNS) : base.GetCursor(x, y);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left && BarBounds.Contains(e.X, e.Y))
            _dragOffset = (IsVertical ? e.X : e.Y) - SplitterDistance;
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragOffset >= 0) // Live resize while dragging; what the user drags to is within the limits.
            SplitterDistance = ClampDistance((IsVertical ? e.X : e.Y) - _dragOffset);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButton.Left)
            _dragOffset = -1;
    }

    protected override void OnRender(DrawingContext dc) => dc.FillRectangle(BarBounds, ClassicStyle.Face);

    private int ClampDistance(int distance)
    {
        var max = Length - _splitterWidth - _panel2MinSize;
        return Math.Max(0, Math.Min(Math.Max(distance, _panel1MinSize), Math.Max(_panel1MinSize, max)));
    }

    private void LayoutPanels()
    {
        var distance = SplitterDistance;
        var rest = Math.Max(0, Length - distance - _splitterWidth);
        if (IsVertical)
        {
            Panel1.Bounds = new Rect(0, 0, distance, Height);
            Panel2.Bounds = new Rect(distance + _splitterWidth, 0, rest, Height);
        }
        else
        {
            Panel1.Bounds = new Rect(0, 0, Width, distance);
            Panel2.Bounds = new Rect(0, distance + _splitterWidth, Width, rest);
        }
        Invalidate();
    }
}
