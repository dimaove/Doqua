using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>Which scroll bars a <see cref="Panel"/> may show when its content does not fit.</summary>
public enum ScrollBars
{
    /// <summary>Never scrolls.</summary>
    None,
    /// <summary>Vertical bar only.</summary>
    Vertical,
    /// <summary>Horizontal bar only.</summary>
    Horizontal,
    /// <summary>Both bars.</summary>
    Both,
}

/// <summary>
/// Container for other controls, positioned by their own X/Y (or <see cref="Control.Anchor"/>).
/// With <see cref="ScrollBars"/> set, the content (the children's right and bottom edges) can be
/// larger than the panel: classic scroll bars appear when needed, the mouse wheel scrolls (Shift+wheel
/// horizontally), and a child that gets the keyboard focus is scrolled into view. Anchors then refer to
/// the visible area, without the scroll bars, so stretched children never make the panel scroll.
/// </summary>
public class Panel : Control
{
    private const int WheelStep = 48;

    private readonly ClassicScrollBar _verticalBar;
    private readonly ClassicScrollBar _horizontalBar;
    private Color _background = Color.Transparent;
    private ScrollBars _scrollBars;
    private int _scrollX;
    private int _scrollY;
    private ScrollLayout _layout;
    private bool _updatingLayout;

    /// <summary>Creates an empty, transparent panel.</summary>
    public Panel()
    {
        Children = new ControlCollection(this);
        _verticalBar = new ClassicScrollBar(this, () => _scrollY, value => ScrollY = value);
        _horizontalBar = new ClassicScrollBar(this, () => _scrollX, value => ScrollX = value) { IsHorizontal = true };
    }

    /// <summary>The controls in the panel, drawn in this order (the last one on top).</summary>
    public ControlCollection Children { get; }

    /// <summary>Fill color; transparent by default.</summary>
    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Scroll bars the panel may show; <see cref="Controls.ScrollBars.None"/> (default) never scrolls.</summary>
    public ScrollBars ScrollBars
    {
        get => _scrollBars;
        set
        {
            if (_scrollBars == value)
                return;
            _scrollBars = value;
            UpdateScrollLayout();
        }
    }

    /// <summary>Horizontal scroll position in pixels: how far the content is moved to the left.</summary>
    public int ScrollX
    {
        get => _scrollX;
        set => SetScroll(value, _scrollY);
    }

    /// <summary>Vertical scroll position in pixels: how far the content is moved up.</summary>
    public int ScrollY
    {
        get => _scrollY;
        set => SetScroll(_scrollX, value);
    }

    /// <summary>Size of the content: the right-most and bottom-most edges of the visible children.</summary>
    public GUI.Size ContentSize
    {
        get
        {
            int width = 0, height = 0;
            foreach (var child in Children)
            {
                if (!child.Visible)
                    continue;
                width = Math.Max(width, child.X + child.Width);
                height = Math.Max(height, child.Y + child.Height);
            }
            return new GUI.Size(width, height);
        }
    }

    /// <summary>Raised after <see cref="ScrollX"/> or <see cref="ScrollY"/> changes.</summary>
    public event EventHandler? Scrolled;

    /// <summary>Scrolls as little as needed to show <paramref name="descendant"/> (a child, or a child's child, ...).</summary>
    public void ScrollIntoView(Control descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);
        if (_scrollBars == ScrollBars.None)
            return;
        // Position of the descendant in content coordinates, through window coordinates.
        var (windowX, windowY) = descendant.PointToWindow(0, 0);
        var (originX, originY) = PointToWindow(0, 0);
        var x = windowX - originX + _scrollX;
        var y = windowY - originY + _scrollY;
        SetScroll(ScrollToShow(_scrollX, x, descendant.Width, _layout.ViewWidth),
            ScrollToShow(_scrollY, y, descendant.Height, _layout.ViewHeight));
    }

    protected virtual void OnScrolled(EventArgs e) => Scrolled?.Invoke(this, e);

    protected override IReadOnlyList<Control> VisualChildren => Children;

    internal override (int X, int Y) ChildOffset => (-_scrollX, -_scrollY);

    internal override Rect? ChildClip =>
        _scrollBars == ScrollBars.None ? null : new Rect(0, 0, _layout.ViewWidth, _layout.ViewHeight);

    internal override (int Width, int Height) AnchorArea =>
        _scrollBars == ScrollBars.None ? base.AnchorArea : (_layout.ViewWidth, _layout.ViewHeight);

    internal override void OnChildLayoutChanged() => UpdateScrollLayout();

    internal override void OnDescendantFocused(Control descendant) => ScrollIntoView(descendant);

    protected override void OnSizeChanged(EventArgs e)
    {
        UpdateScrollLayout();
        base.OnSizeChanged(e);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.FillRectangle(0, 0, Width, Height, _background);
        var layout = _layout;
        var enabled = IsEffectivelyEnabled;
        if (layout.Vertical)
        {
            SyncBars();
            _verticalBar.Draw(dc, enabled);
        }
        if (layout.Horizontal)
        {
            SyncBars();
            _horizontalBar.Draw(dc, enabled);
        }
        if (layout.Vertical && layout.Horizontal) // The corner between the two bars.
            dc.FillRectangle(layout.ViewWidth, layout.ViewHeight, ClassicScrollBar.Thickness, ClassicScrollBar.Thickness, ClassicStyle.Face);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;
        SyncBars();
        if (_layout.Vertical && _verticalBar.Bounds.Contains(e.X, e.Y))
            _verticalBar.Press(e.X, e.Y);
        else if (_layout.Horizontal && _horizontalBar.Bounds.Contains(e.X, e.Y))
            _horizontalBar.Press(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        _verticalBar.Drag(e.X, e.Y);
        _horizontalBar.Drag(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _verticalBar.Release();
        _horizontalBar.Release();
    }

    /// <summary>The wheel scrolls vertically, or horizontally with Shift (or when there is only a horizontal bar).</summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled)
            return;
        var horizontal = _layout.Horizontal && (e.Modifiers.HasFlag(KeyModifiers.Shift) || !_layout.Vertical);
        if (horizontal)
            ScrollX -= e.Delta * WheelStep;
        else if (_layout.Vertical)
            ScrollY -= e.Delta * WheelStep;
        else
            return; // Nothing to scroll: let a parent panel have it.
        e.Handled = true;
    }

    /// <summary>
    /// Decides which bars are needed and how large the visible area is. Anchored children depend on the
    /// visible area, which depends on the bars, which depend on the children, so this repeats until stable.
    /// </summary>
    private void UpdateScrollLayout()
    {
        if (_updatingLayout)
            return;
        _updatingLayout = true;
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var layout = ComputeLayout();
                var viewChanged = (layout.ViewWidth, layout.ViewHeight) != (_layout.ViewWidth, _layout.ViewHeight);
                _layout = layout;
                if (!viewChanged || _scrollBars == ScrollBars.None)
                    break;
                foreach (var child in Children)
                    child.ApplyAnchor();
            }
            SetScroll(_scrollX, _scrollY); // Clamp to the new size.
        }
        finally
        {
            _updatingLayout = false;
        }
        Invalidate();
    }

    private ScrollLayout ComputeLayout()
    {
        var content = ContentSize;
        bool allowVertical = _scrollBars is ScrollBars.Vertical or ScrollBars.Both;
        bool allowHorizontal = _scrollBars is ScrollBars.Horizontal or ScrollBars.Both;
        bool vertical = false, horizontal = false;
        int viewWidth = Width, viewHeight = Height;
        // Twice: showing one bar makes the area smaller, which can make the other one necessary.
        for (var pass = 0; pass < 2; pass++)
        {
            vertical = allowVertical && content.Height > viewHeight;
            horizontal = allowHorizontal && content.Width > viewWidth;
            viewWidth = Math.Max(0, Width - (vertical ? ClassicScrollBar.Thickness : 0));
            viewHeight = Math.Max(0, Height - (horizontal ? ClassicScrollBar.Thickness : 0));
        }
        return new ScrollLayout(vertical, horizontal, viewWidth, viewHeight, content.Width, content.Height);
    }

    private void SetScroll(int x, int y)
    {
        x = _layout.Horizontal ? Math.Clamp(x, 0, Math.Max(0, _layout.ContentWidth - _layout.ViewWidth)) : 0;
        y = _layout.Vertical ? Math.Clamp(y, 0, Math.Max(0, _layout.ContentHeight - _layout.ViewHeight)) : 0;
        if (x == _scrollX && y == _scrollY)
            return;
        (_scrollX, _scrollY) = (x, y);
        Invalidate();
        GetWindow()?.UpdateCursor(); // A different child may now be under the pointer.
        OnScrolled(EventArgs.Empty);
    }

    /// <summary>New scroll position so that [start, start + size) is inside the view of <paramref name="viewSize"/>.</summary>
    private static int ScrollToShow(int scroll, int start, int size, int viewSize)
    {
        if (start < scroll || size > viewSize)
            return start;
        if (start + size > scroll + viewSize)
            return start + size - viewSize;
        return scroll;
    }

    private void SyncBars()
    {
        var layout = _layout;
        _verticalBar.Bounds = new Rect(layout.ViewWidth, 0, ClassicScrollBar.Thickness, layout.ViewHeight);
        _verticalBar.Maximum = Math.Max(0, layout.ContentHeight - layout.ViewHeight);
        _verticalBar.ViewSize = layout.ViewHeight;
        _verticalBar.ContentSize = layout.ContentHeight;
        _verticalBar.SmallChange = 16;
        _verticalBar.LargeChange = Math.Max(16, layout.ViewHeight - 16);

        _horizontalBar.Bounds = new Rect(0, layout.ViewHeight, layout.ViewWidth, ClassicScrollBar.Thickness);
        _horizontalBar.Maximum = Math.Max(0, layout.ContentWidth - layout.ViewWidth);
        _horizontalBar.ViewSize = layout.ViewWidth;
        _horizontalBar.ContentSize = layout.ContentWidth;
        _horizontalBar.SmallChange = 16;
        _horizontalBar.LargeChange = Math.Max(16, layout.ViewWidth - 16);
    }

    private readonly record struct ScrollLayout(bool Vertical, bool Horizontal, int ViewWidth, int ViewHeight, int ContentWidth, int ContentHeight);
}
