namespace Doqua.GUI;

/// <summary>
/// Base class of all visual components. <see cref="X"/> and <see cref="Y"/> are relative to the parent.
/// </summary>
public abstract class Control
{
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private bool _isVisible = true;

    public string? Name { get; set; }

    public Control? Parent { get; internal set; }

    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public int Width
    {
        get => _width;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SetField(ref _width, value);
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            SetField(ref _height, value);
        }
    }

    public Rect Bounds
    {
        get => new(_x, _y, _width, _height);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.Width);
            ArgumentOutOfRangeException.ThrowIfNegative(value.Height);
            if (value == Bounds)
                return;
            (_x, _y, _width, _height) = (value.X, value.Y, value.Width, value.Height);
            Invalidate();
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            Invalidate();
        }
    }

    /// <summary>Raised when a mouse button is pressed and released over this control.</summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    /// <summary>Window that shows this control as its <see cref="Window.Content"/> (set on the root only).</summary>
    internal Window? Host { get; set; }

    /// <summary>Children in drawing order: the last one is drawn on top and hit-tested first.</summary>
    protected virtual IReadOnlyList<Control> VisualChildren => Array.Empty<Control>();

    /// <summary>Requests a redraw of the window that contains this control.</summary>
    public void Invalidate()
    {
        var root = this;
        while (root.Parent != null)
            root = root.Parent;
        root.Host?.Invalidate();
    }

    /// <summary>
    /// Draws this control's own content. (0, 0) is the control's top-left corner and drawing is
    /// clipped to its bounds. Children are drawn by the framework afterwards, on top.
    /// </summary>
    protected virtual void OnRender(DrawingContext dc)
    {
    }

    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);

    internal void RaiseMouseClick(MouseEventArgs e) => OnMouseClick(e);

    internal void Render(DrawingContext dc)
    {
        if (!_isVisible)
            return;
        var saved = dc.PushBounds(Bounds);
        if (!dc.IsClipEmpty)
        {
            OnRender(dc);
            foreach (var child in VisualChildren)
                child.Render(dc);
        }
        dc.Restore(saved);
    }

    /// <summary>
    /// Finds the topmost visible control at (<paramref name="x"/>, <paramref name="y"/>), given in the
    /// parent's coordinates, and returns the point in that control's coordinates.
    /// </summary>
    internal Control? HitTest(int x, int y, out int localX, out int localY)
    {
        localX = x - _x;
        localY = y - _y;
        if (!_isVisible || localX < 0 || localY < 0 || localX >= _width || localY >= _height)
            return null;

        var children = VisualChildren;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var hit = children[i].HitTest(localX, localY, out var childX, out var childY);
            if (hit != null)
            {
                (localX, localY) = (childX, childY);
                return hit;
            }
        }
        return this;
    }

    private void SetField(ref int field, int value)
    {
        if (field == value)
            return;
        field = value;
        Invalidate();
    }
}
