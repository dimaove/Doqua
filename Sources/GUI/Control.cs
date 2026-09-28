using Doqua.Controls;

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
    private Anchor _anchor;
    private bool _applyingAnchor;
    private Cursor _cursor;

    /// <summary>Optional name, for the application's own use.</summary>
    public string? Name { get; set; }

    /// <summary>The container this control is in, or null.</summary>
    public Control? Parent { get; internal set; }

    /// <summary>Left edge, relative to the parent.</summary>
    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    /// <summary>Top edge, relative to the parent.</summary>
    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    /// <summary>Width in pixels.</summary>
    public int Width
    {
        get => _width;
        set => Bounds = Bounds with { Width = value };
    }

    /// <summary>Height in pixels.</summary>
    public int Height
    {
        get => _height;
        set => Bounds = Bounds with { Height = value };
    }

    /// <summary>Position (relative to the parent) and size, together.</summary>
    public Rect Bounds
    {
        get => new(_x, _y, _width, _height);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value.Width);
            ArgumentOutOfRangeException.ThrowIfNegative(value.Height);
            if (value == Bounds)
                return;
            var resized = value.Width != _width || value.Height != _height;
            (_x, _y, _width, _height) = (value.X, value.Y, value.Width, value.Height);
            Invalidate();
            if (resized)
                OnSizeChanged(EventArgs.Empty);
            Parent?.OnChildLayoutChanged();
        }
    }

    /// <summary>
    /// Keeps the control at fixed distances from its parent's edges; see <see cref="GUI.Anchor"/>.
    /// Applied when the parent is resized, when the control gets a parent or changes size, and when set.
    /// </summary>
    public Anchor Anchor
    {
        get => _anchor;
        set
        {
            if (_anchor == value)
                return;
            _anchor = value;
            ApplyAnchor();
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
            Parent?.OnChildLayoutChanged();
            OnVisibleChanged(EventArgs.Empty);
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
            OnEnabledChanged(EventArgs.Empty);
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
    /// <summary>Raised after <see cref="Visible"/> changed.</summary>
    public event EventHandler? VisibleChanged;

    /// <summary>Raised after <see cref="Enabled"/> changed (not when only a parent's Enabled changed).</summary>
    public event EventHandler? EnabledChanged;

    /// <summary>Raised after Width or Height changed; anchored children have already been rearranged.</summary>
    public event EventHandler? SizeChanged;

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

    /// <summary>Raised when the mouse wheel turns over this control or one of its children (until handled).</summary>
    public event EventHandler<MouseWheelEventArgs>? MouseWheel;

    /// <summary>Raised when the pointer moves onto this control.</summary>
    public event EventHandler? MouseEnter;

    /// <summary>Raised when the control receives the keyboard focus.</summary>
    public event EventHandler? GotFocus;

    /// <summary>Raised when the control loses the keyboard focus.</summary>
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

    /// <summary>
    /// Mouse pointer shape over this control. <see cref="GUI.Cursor.Default"/> (the default) uses the
    /// parent's cursor, and the arrow at the top of the tree. See <see cref="GetCursor"/>.
    /// </summary>
    public Cursor Cursor
    {
        get => _cursor;
        set
        {
            if (_cursor == value)
                return;
            _cursor = value;
            GetWindow()?.UpdateCursor();
        }
    }

    /// <summary>
    /// Menu opened by a right click on this control (or on a child without its own menu) and by
    /// Shift+F10 while the control has focus. See <see cref="GetContextMenu"/>.
    /// </summary>
    public PopupMenu? ContextMenu { get; set; }

    /// <summary>
    /// Whether Tab / Shift+Tab stops at this focusable control. A radio button returns false unless it
    /// is the one that represents its group, so a whole group is a single Tab stop.
    /// </summary>
    protected virtual bool IsTabStop => true;

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

    protected virtual void OnMouseWheel(MouseWheelEventArgs e) => MouseWheel?.Invoke(this, e);

    protected virtual void OnMouseEnter(EventArgs e) => MouseEnter?.Invoke(this, e);

    protected virtual void OnMouseLeave(EventArgs e) => MouseLeave?.Invoke(this, e);

    /// <summary>Re-applies this control's anchor and rearranges anchored children, then raises <see cref="SizeChanged"/>.</summary>
    protected virtual void OnSizeChanged(EventArgs e)
    {
        ApplyAnchor(); // e.g. an auto-sized label anchored to the right keeps its right edge.
        foreach (var child in VisualChildren)
            child.ApplyAnchor();
        SizeChanged?.Invoke(this, e);
    }

    /// <summary>
    /// Returns the menu to open as the context menu; <see cref="ContextMenu"/> by default. Override it to
    /// supply a different menu, e.g. one with the control's own commands followed by the user's items.
    /// </summary>
    protected virtual PopupMenu? GetContextMenu() => ContextMenu;

    /// <summary>
    /// Cursor at (<paramref name="x"/>, <paramref name="y"/>) in this control's coordinates; <see cref="Cursor"/>
    /// by default. Override it to vary the cursor inside the control (e.g. an arrow over a scroll bar).
    /// </summary>
    protected virtual Cursor GetCursor(int x, int y) => _cursor;

    protected virtual void OnVisibleChanged(EventArgs e) => VisibleChanged?.Invoke(this, e);

    protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);

    protected virtual void OnGotFocus(EventArgs e) => GotFocus?.Invoke(this, e);

    protected virtual void OnLostFocus(EventArgs e) => LostFocus?.Invoke(this, e);

    protected virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);

    protected virtual void OnTextInput(TextInputEventArgs e) => TextInput?.Invoke(this, e);

    internal void RaiseGotFocus() => OnGotFocus(EventArgs.Empty);

    internal void RaiseLostFocus() => OnLostFocus(EventArgs.Empty);

    internal void RaiseKeyDown(KeyEventArgs e) => OnKeyDown(e);

    internal void RaiseTextInput(TextInputEventArgs e) => OnTextInput(e);

    /// <summary>Moves and resizes the control inside its parent according to <see cref="Anchor"/>.</summary>
    internal void ApplyAnchor()
    {
        if (Parent is not { } parent || _anchor.IsNone || _applyingAnchor)
            return;
        _applyingAnchor = true; // Setting Bounds below calls back here through OnSizeChanged.
        try
        {
            var (areaWidth, areaHeight) = parent.AnchorArea;
            var (x, width) = Anchor.Arrange(_x, _width, _anchor.Left, _anchor.Right, areaWidth);
            var (y, height) = Anchor.Arrange(_y, _height, _anchor.Top, _anchor.Bottom, areaHeight);
            Bounds = new Rect(x, y, width, height);
        }
        finally
        {
            _applyingAnchor = false;
        }
    }

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
        if (_focusable && IsTabStop)
            result.Add(this);
        foreach (var child in VisualChildren)
            child.CollectFocusable(result);
    }

    internal void RaiseMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void RaiseMouseUp(MouseEventArgs e) => OnMouseUp(e);

    internal void RaiseMouseClick(MouseEventArgs e) => OnMouseClick(e);

    internal void RaiseMouseMove(MouseMoveEventArgs e) => OnMouseMove(e);

    internal void RaiseMouseWheel(MouseWheelEventArgs e) => OnMouseWheel(e);

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

    internal PopupMenu? GetContextMenuForWindow() => GetContextMenu();

    /// <summary>Resolves <see cref="Cursor.Default"/> through the parents; (x, y) is in this control's coordinates.</summary>
    internal Cursor ResolveCursor(int x, int y)
    {
        for (var control = this; control != null; control = control.Parent)
        {
            var cursor = control.GetCursor(x, y);
            if (cursor != Cursor.Default)
                return cursor;
            var (offsetX, offsetY) = control.Parent?.ChildOffset ?? default;
            x += control._x + offsetX;
            y += control._y + offsetY;
        }
        return Cursor.Arrow;
    }

    /// <summary>Converts a point in this control's coordinates to window client coordinates.</summary>
    internal (int X, int Y) PointToWindow(int x, int y)
    {
        for (var control = this; control != null; control = control.Parent)
        {
            var (offsetX, offsetY) = control.Parent?.ChildOffset ?? default;
            x += control._x + offsetX;
            y += control._y + offsetY;
        }
        return (x, y);
    }

    /// <summary>Converts a point in window client coordinates to this control's coordinates.</summary>
    internal (int X, int Y) PointFromWindow(int x, int y)
    {
        for (var control = this; control != null; control = control.Parent)
        {
            var (offsetX, offsetY) = control.Parent?.ChildOffset ?? default;
            x -= control._x + offsetX;
            y -= control._y + offsetY;
        }
        return (x, y);
    }

    /// <summary>
    /// Shift applied to the children when they are drawn and hit-tested, in this control's coordinates
    /// (a scrolling panel returns minus its scroll position). Children's X and Y are not changed by it.
    /// </summary>
    internal virtual (int X, int Y) ChildOffset => default;

    /// <summary>Area (in this control's coordinates) outside which children are neither drawn nor hit; null = the whole control.</summary>
    internal virtual Rect? ChildClip => null;

    /// <summary>Size that children's anchors refer to: the whole control, or a scrolling panel's visible area.</summary>
    internal virtual (int Width, int Height) AnchorArea => (_width, _height);

    /// <summary>Called when a child moved, resized, was shown or hidden, added or removed.</summary>
    internal virtual void OnChildLayoutChanged()
    {
    }

    /// <summary>Called on each ancestor, innermost first, when <paramref name="descendant"/> gets the keyboard focus.</summary>
    internal virtual void OnDescendantFocused(Control descendant)
    {
    }

    internal void Render(DrawingContext dc)
    {
        if (!_visible)
            return;
        var saved = dc.PushBounds(Bounds);
        if (!dc.IsClipEmpty)
        {
            OnRender(dc);
            var children = VisualChildren;
            if (children.Count > 0)
            {
                var beforeChildren = dc.PushChildArea(ChildClip, ChildOffset);
                if (!dc.IsClipEmpty)
                {
                    foreach (var child in children)
                        child.Render(dc);
                }
                dc.Restore(beforeChildren);
            }
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

        // Outside the children's area (e.g. over a scroll bar) the control itself is hit.
        if (ChildClip is { } clip && !clip.Contains(localX, localY))
            return this;
        var (offsetX, offsetY) = ChildOffset;
        var children = VisualChildren;
        for (var i = children.Count - 1; i >= 0; i--)
        {
            var hit = children[i].HitTest(localX - offsetX, localY - offsetY, out var childX, out var childY);
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
