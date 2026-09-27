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
    private bool _visible = true;
    private bool _enabled = true;
    private bool _focusable;

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

    /// <summary>When false, the control and its children are not drawn and ignore the mouse.</summary>
    public bool Visible
    {
        get => _visible;
        set
        {
            if (_visible == value)
                return;
            _visible = value;
            Invalidate();
            GetWindow()?.ValidateFocus();
        }
    }

    /// <summary>
    /// When false, the control and its children are still drawn (usually in disabled colors) but
    /// get no mouse events. They still cover controls below them, so clicks do not pass through.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            Invalidate();
            GetWindow()?.ValidateFocus();
        }
    }

    /// <summary>True if this control and all its parents are visible.</summary>
    public bool IsEffectivelyVisible
    {
        get
        {
            for (var control = this; control != null; control = control.Parent)
            {
                if (!control._visible)
                    return false;
            }
            return true;
        }
    }

    /// <summary>True if this control and all its parents are enabled.</summary>
    public bool IsEffectivelyEnabled
    {
        get
        {
            for (var control = this; control != null; control = control.Parent)
            {
                if (!control._enabled)
                    return false;
            }
            return true;
        }
    }

    /// <summary>Whether the control can receive keyboard focus (by click, Tab or <see cref="Focus"/>).</summary>
    public bool Focusable
    {
        get => _focusable;
        set
        {
            if (_focusable == value)
                return;
            _focusable = value;
            GetWindow()?.ValidateFocus();
        }
    }

    /// <summary>True if this control is its window's <see cref="Window.FocusedControl"/>.</summary>
    public bool Focused => GetWindow()?.FocusedControl == this;

    /// <summary>Focusable, visible and enabled (with all parents), and shown in a window.</summary>
    public bool CanFocus => _focusable && IsEffectivelyVisible && IsEffectivelyEnabled && GetWindow() != null;

    /// <summary>True while the mouse pointer is over this control (and not over one of its children).</summary>
    public bool IsMouseOver { get; private set; }

    /// <summary>Raised when a mouse button is pressed over this control.</summary>
    public event EventHandler<MouseEventArgs>? MouseDown;

    /// <summary>
    /// Raised when a mouse button pressed over this control is released, even if the pointer
    /// has moved away or the control was disabled meanwhile: every MouseDown gets its MouseUp.
    /// </summary>
    public event EventHandler<MouseEventArgs>? MouseUp;

    /// <summary>Raised when a mouse button is pressed and released over this control.</summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    /// <summary>
    /// Raised when the pointer moves over this control, or anywhere while a mouse button
    /// pressed over this control is held (so dragging keeps reporting positions).
    /// </summary>
    public event EventHandler<MouseMoveEventArgs>? MouseMove;

    public event EventHandler? MouseEnter;

    public event EventHandler? GotFocus;

    public event EventHandler? LostFocus;

    /// <summary>
    /// Raised when a key is pressed while this control or one of its children is focused.
    /// Goes from the focused control up through its parents until handled.
    /// </summary>
    public event EventHandler<KeyEventArgs>? KeyDown;

    /// <summary>Raised with typed text; routed like <see cref="KeyDown"/>.</summary>
    public event EventHandler<TextInputEventArgs>? TextInput;

    /// <summary>Raised after <see cref="MouseEnter"/> when the pointer leaves, even if the control was disabled meanwhile.</summary>
    public event EventHandler? MouseLeave;

    /// <summary>Window that shows this control as its <see cref="Window.Content"/> (set on the root only).</summary>
    internal Window? Host { get; set; }

    /// <summary>Children in drawing order: the last one is drawn on top and hit-tested first.</summary>
    protected virtual IReadOnlyList<Control> VisualChildren => Array.Empty<Control>();

    /// <summary>Moves keyboard focus to this control. Returns false if it <see cref="CanFocus"/> not.</summary>
    public bool Focus() => GetWindow()?.TrySetFocus(this) ?? false;

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

    protected virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);

    protected virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);

    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);

    protected virtual void OnMouseMove(MouseMoveEventArgs e) => MouseMove?.Invoke(this, e);

    protected virtual void OnMouseEnter(EventArgs e) => MouseEnter?.Invoke(this, e);

    protected virtual void OnMouseLeave(EventArgs e) => MouseLeave?.Invoke(this, e);

    protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);

    protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);

    protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);

    protected virtual void OnTextInput(TextInputEventArgs e) => TextInput?.Invoke(this, e);

    internal void RaiseGotFocus() => OnGotFocus(EventArgs.Empty);

    internal void RaiseLostFocus() => OnLostFocus(EventArgs.Empty);

    internal void RaiseKeyDown(KeyEventArgs e) => OnKeyDown(e);

    internal void RaiseTextInput(TextInputEventArgs e) => OnTextInput(e);

    /// <summary>Window whose content tree contains this control, if any.</summary>
    internal Window? GetWindow()
    {
        var root = this;
        while (root.Parent != null)
            root = root.Parent;
        return root.Host;
    }

    /// <summary>Adds focusable controls of this subtree in tree (Tab) order, skipping hidden and disabled ones.</summary>
    internal void CollectFocusable(List<Control> result)
    {
        if (!_visible || !_enabled)
            return;
        if (_focusable)
            result.Add(this);
        foreach (var child in VisualChildren)
            child.CollectFocusable(result);
    }

    internal void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);

    internal void RaiseMouseClick(MouseEventArgs e) => OnMouseClick(e);

    internal void RaiseMouseMove(MouseMoveEventArgs e) => OnMouseMove(e);

    internal void SetMouseOver(bool value)
    {
        if (IsMouseOver == value)
            return;
        IsMouseOver = value;
        if (value)
            OnMouseEnter(EventArgs.Empty);
        else
            OnMouseLeave(EventArgs.Empty);
    }

    /// <summary>Converts a point in window client coordinates to this control's coordinates.</summary>
    internal (int X, int Y) PointFromWindow(int x, int y)
    {
        for (var control = this; control != null; control = control.Parent)
        {
            x -= control._x;
            y -= control._y;
        }
        return (x, y);
    }

    internal void Render(DrawingContext dc)
    {
        if (!_visible)
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
    /// Finds the topmost visible control (enabled or not) at (<paramref name="x"/>, <paramref name="y"/>), given in the
    /// parent's coordinates, and returns the point in that control's coordinates.
    /// </summary>
    internal Control? HitTest(int x, int y, out int localX, out int localY)
    {
        localX = x - _x;
        localY = y - _y;
        if (!_visible || localX < 0 || localY < 0 || localX >= _width || localY >= _height)
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
