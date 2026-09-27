using System.Collections.ObjectModel;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Hierarchy of <see cref="TreeNode"/>s in the classic style: dotted lines, [+] / [-] boxes and a highlighted
/// selection. Click a node to select it, its box (or double-click it) to expand or collapse it.
/// Keys: Up / Down / Home / End / PageUp / PageDown move the selection; Right expands (then goes to the first
/// child); Left collapses (then goes to the parent); Enter toggles. Scroll bars appear when needed.
/// </summary>
public class TreeView : Control
{
    private const int EdgeSize = 2;
    private const int Indent = 19;
    private const int BoxSize = 9;
    private const int TextGap = 3;
    private const int WheelRows = 3;

    private readonly ClassicScrollBar _verticalBar;
    private readonly ClassicScrollBar _horizontalBar;
    private TreeNode? _selectedNode;
    private int _scrollX;
    private int _scrollY;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _background = Color.White;
    private Color _lineColor = ClassicStyle.Shadow;
    private Color _selectionBackground = new(0, 0, 128);
    private Color _selectionColor = Color.White;
    private Color _inactiveSelectionBackground = new(204, 204, 204);

    public TreeView()
    {
        Nodes = new TreeNodeCollection(this, null);
        _verticalBar = new ClassicScrollBar(this, () => _scrollY, value => ScrollTo(_scrollX, value));
        _horizontalBar = new ClassicScrollBar(this, () => _scrollX, value => ScrollTo(value, _scrollY)) { IsHorizontal = true };
        Focusable = true;
        Width = 200;
        Height = 240;
    }

    /// <summary>The top-level nodes.</summary>
    public TreeNodeCollection Nodes { get; }

    /// <summary>Raised after <see cref="SelectedNode"/> changes, by the user or from code.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// The selected node, or null. Setting a node expands its parents and scrolls it into view; it must belong to
    /// this tree view.
    /// </summary>
    public TreeNode? SelectedNode
    {
        get => _selectedNode;
        set
        {
            if (value != null && value.TreeView != this)
                throw new ArgumentException(Localization.Get("Doqua.Error.NodeNotInTree"), nameof(value));
            if (value == _selectedNode)
                return;
            for (var parent = value?.Parent; parent != null; parent = parent.Parent)
                parent.SetExpanded(true);
            _selectedNode = value;
            if (value != null)
                ScrollIntoView(value);
            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    public Color Background
    {
        get => _background;
        set => SetColor(ref _background, value);
    }

    /// <summary>Color of the dotted lines and of the expand boxes' frames.</summary>
    public Color LineColor
    {
        get => _lineColor;
        set => SetColor(ref _lineColor, value);
    }

    public Color SelectionBackground
    {
        get => _selectionBackground;
        set => SetColor(ref _selectionBackground, value);
    }

    public Color SelectionColor
    {
        get => _selectionColor;
        set => SetColor(ref _selectionColor, value);
    }

    /// <summary>Background of the selected node while the tree view does not have the focus.</summary>
    public Color InactiveSelectionBackground
    {
        get => _inactiveSelectionBackground;
        set => SetColor(ref _inactiveSelectionBackground, value);
    }

    /// <summary>Scrolls so that <paramref name="node"/> (which must be visible, i.e. its parents expanded) is shown.</summary>
    public void ScrollIntoView(TreeNode node)
    {
        var index = VisibleNodes().FindIndex(item => item.Node == node);
        if (index < 0)
            return;
        var layout = GetLayout();
        var top = index * ItemHeight;
        if (top < _scrollY)
            ScrollTo(_scrollX, top);
        else if (top + ItemHeight > _scrollY + layout.Body.Height)
            ScrollTo(_scrollX, top + ItemHeight - layout.Body.Height);
    }

    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left)
            return;
        var layout = GetLayout();
        SyncBars(layout);
        if (layout.Vertical && _verticalBar.Bounds.Contains(e.X, e.Y))
        {
            _verticalBar.Press(e.X, e.Y);
            return;
        }
        if (layout.Horizontal && _horizontalBar.Bounds.Contains(e.X, e.Y))
        {
            _horizontalBar.Press(e.X, e.Y);
            return;
        }
        if (!layout.Body.Contains(e.X, e.Y))
            return;

        var visible = VisibleNodes();
        var index = (e.Y - layout.Body.Y + _scrollY) / ItemHeight;
        if (index < 0 || index >= visible.Count)
            return;
        var (node, level) = visible[index];
        var boxCenter = layout.Body.X - _scrollX + level * Indent + Indent / 2;
        if (node.Nodes.Count > 0 && Math.Abs(e.X - boxCenter) <= BoxSize / 2 + 2)
        {
            node.Toggle(); // A click on the box only expands or collapses.
            return;
        }
        SelectedNode = node;
        if (e.ClickCount == 2 && node.Nodes.Count > 0)
            node.Toggle();
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

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled)
            return;
        var layout = GetLayout();
        var horizontal = layout.Horizontal && (e.Modifiers.HasFlag(KeyModifiers.Shift) || !layout.Vertical);
        if (horizontal)
            ScrollTo(_scrollX - e.Delta * 3 * Indent, _scrollY);
        else if (layout.Vertical)
            ScrollTo(_scrollX, _scrollY - e.Delta * WheelRows * ItemHeight);
        else
            return;
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Modifiers != KeyModifiers.None)
            return;
        var visible = VisibleNodes();
        if (visible.Count == 0)
            return;
        var index = _selectedNode == null ? -1 : visible.FindIndex(item => item.Node == _selectedNode);
        var page = Math.Max(1, GetLayout().Body.Height / ItemHeight - 1);
        var node = _selectedNode;
        switch (e.Key)
        {
            case Key.Up:
                SelectedNode = visible[Math.Max(0, index - 1)].Node;
                break;
            case Key.Down:
                SelectedNode = visible[Math.Min(visible.Count - 1, index + 1)].Node;
                break;
            case Key.PageUp:
                SelectedNode = visible[Math.Max(0, index - page)].Node;
                break;
            case Key.PageDown:
                SelectedNode = visible[Math.Min(visible.Count - 1, Math.Max(0, index) + page)].Node;
                break;
            case Key.Home:
                SelectedNode = visible[0].Node;
                break;
            case Key.End:
                SelectedNode = visible[^1].Node;
                break;
            case Key.Right when node != null:
                if (node.Nodes.Count > 0 && !node.IsExpanded)
                    node.Expand();
                else if (node.Nodes.Count > 0)
                    SelectedNode = node.Nodes[0];
                break;
            case Key.Left when node != null:
                if (node.IsExpanded)
                    node.Collapse();
                else if (node.Parent != null)
                    SelectedNode = node.Parent;
                break;
            case Key.Enter when node is { Nodes.Count: > 0 }:
                node.Toggle();
                break;
            default:
                return;
        }
        e.Handled = true;
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

    protected override void OnRender(DrawingContext dc)
    {
        var enabled = IsEffectivelyEnabled;
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, _background);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var layout = GetLayout();
        ClampScroll(layout);
        var body = layout.Body;
        var font = Font;
        var itemHeight = ItemHeight;
        var active = enabled && Focused && GetWindow()?.IsActive == true;
        var visible = VisibleNodes();

        using (dc.PushClip(body))
        {
            var first = Math.Max(0, _scrollY / itemHeight);
            var last = Math.Min(visible.Count - 1, (_scrollY + body.Height) / itemHeight);
            for (var index = first; index <= last; index++)
            {
                var (node, level) = visible[index];
                var top = body.Y + index * itemHeight - _scrollY;
                var left = body.X - _scrollX;
                var centerY = top + itemHeight / 2;
                var boxX = left + level * Indent + Indent / 2;

                // Vertical lines of the ancestors that have more children below this row.
                var ancestor = node.Parent;
                for (var ancestorLevel = level - 1; ancestorLevel >= 0 && ancestor != null; ancestorLevel--, ancestor = ancestor.Parent)
                {
                    if (NextSibling(ancestor) != null)
                        DottedVertical(dc, left + ancestorLevel * Indent + Indent / 2, top, top + itemHeight);
                }
                // This node: up to the previous node (not for the very first one), down if a sibling follows,
                // and across to the text.
                var isFirst = node.Parent == null && Nodes.Count > 0 && Nodes[0] == node;
                DottedVertical(dc, boxX, isFirst ? centerY : top, NextSibling(node) != null ? top + itemHeight : centerY + 1);
                DottedHorizontal(dc, boxX, left + (level + 1) * Indent - 1, centerY);

                if (node.Nodes.Count > 0)
                    DrawExpandBox(dc, boxX, centerY, node.IsExpanded);

                var textX = left + (level + 1) * Indent + TextGap;
                var textWidth = font.MeasureText(node.Text).Width;
                var textRect = new Rect(textX - 2, top, textWidth + 4, itemHeight);
                var selected = node == _selectedNode;
                if (selected)
                    dc.FillRectangle(textRect, active ? _selectionBackground : _inactiveSelectionBackground);
                var color = !enabled ? _disabledColor : selected && active ? _selectionColor : _color;
                dc.DrawText(node.Text, font, color, textX, top + (itemHeight - (font.Ascent + font.Descent)) / 2);
                if (selected && active)
                    ClassicStyle.DrawFocusRectangle(dc, textRect, _selectionColor);
            }
        }

        if (layout.Vertical || layout.Horizontal)
            SyncBars(layout);
        if (layout.Vertical)
            _verticalBar.Draw(dc, enabled);
        if (layout.Horizontal)
            _horizontalBar.Draw(dc, enabled);
        if (layout.Vertical && layout.Horizontal)
            dc.FillRectangle(body.Right, body.Bottom, ClassicScrollBar.Thickness, ClassicScrollBar.Thickness, ClassicStyle.Face);
    }

    internal void OnTreeChanged()
    {
        ClampScroll(GetLayout());
        Invalidate();
    }

    /// <summary>Called when <paramref name="node"/> collapses: a selection hidden inside it moves to the node.</summary>
    internal void OnCollapsed(TreeNode node)
    {
        for (var ancestor = _selectedNode?.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor == node)
            {
                SelectedNode = node;
                break;
            }
        }
        OnTreeChanged();
    }

    /// <summary>Called when a subtree is removed: if it held the selection, nothing is selected any more.</summary>
    internal void OnRemoved(TreeNode subtree)
    {
        for (var node = _selectedNode; node != null; node = node.Parent)
        {
            if (node == subtree)
            {
                SelectedNode = null;
                break;
            }
        }
        OnTreeChanged();
    }

    private int ItemHeight => Math.Max(BoxSize + 4, Font.LineHeight + 2);

    /// <summary>Nodes shown, top to bottom (children of collapsed nodes are skipped), with their depth.</summary>
    private List<(TreeNode Node, int Level)> VisibleNodes()
    {
        var result = new List<(TreeNode, int)>();
        void Add(TreeNodeCollection nodes, int level)
        {
            foreach (var node in nodes)
            {
                result.Add((node, level));
                if (node.IsExpanded)
                    Add(node.Nodes, level + 1);
            }
        }
        Add(Nodes, 0);
        return result;
    }

    private TreeNode? NextSibling(TreeNode node)
    {
        var siblings = node.Parent?.Nodes ?? Nodes;
        var index = siblings.IndexOf(node);
        return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : null;
    }

    private TreeLayout GetLayout()
    {
        var visible = VisibleNodes();
        var font = Font;
        var contentWidth = visible.Count == 0 ? 0
            : visible.Max(item => (item.Level + 1) * Indent + TextGap + font.MeasureText(item.Node.Text).Width + 4);
        var contentHeight = visible.Count * ItemHeight;
        int innerWidth = Math.Max(0, Width - 2 * EdgeSize), innerHeight = Math.Max(0, Height - 2 * EdgeSize);
        bool vertical = false, horizontal = false;
        int viewWidth = innerWidth, viewHeight = innerHeight;
        for (var pass = 0; pass < 2; pass++)
        {
            vertical = contentHeight > viewHeight;
            horizontal = contentWidth > viewWidth;
            viewWidth = Math.Max(0, innerWidth - (vertical ? ClassicScrollBar.Thickness : 0));
            viewHeight = Math.Max(0, innerHeight - (horizontal ? ClassicScrollBar.Thickness : 0));
        }
        return new TreeLayout(new Rect(EdgeSize, EdgeSize, viewWidth, viewHeight), contentWidth, contentHeight, vertical, horizontal);
    }

    /// <summary>Classic dotted line: every other pixel, aligned to the grid so that joining lines match.</summary>
    private void DottedVertical(DrawingContext dc, int x, int y1, int y2)
    {
        for (var y = y1 + ((x + y1) & 1); y < y2; y += 2)
            dc.FillRectangle(x, y, 1, 1, _lineColor);
    }

    private void DottedHorizontal(DrawingContext dc, int x1, int x2, int y)
    {
        for (var x = x1 + ((x1 + y) & 1); x < x2; x += 2)
            dc.FillRectangle(x, y, 1, 1, _lineColor);
    }

    /// <summary>The 9 x 9 [+] / [-] box: white, framed, with a minus and (when collapsed) a plus.</summary>
    private void DrawExpandBox(DrawingContext dc, int centerX, int centerY, bool expanded)
    {
        var box = new Rect(centerX - BoxSize / 2, centerY - BoxSize / 2, BoxSize, BoxSize);
        dc.FillRectangle(box, Color.White);
        dc.DrawRectangle(box, _lineColor);
        dc.FillRectangle(centerX - 2, centerY, 5, 1, Color.Black);
        if (!expanded)
            dc.FillRectangle(centerX, centerY - 2, 1, 5, Color.Black);
    }

    private void ScrollTo(int x, int y)
    {
        var layout = GetLayout();
        x = Math.Clamp(x, 0, Math.Max(0, layout.ContentWidth - layout.Body.Width));
        y = Math.Clamp(y, 0, Math.Max(0, layout.ContentHeight - layout.Body.Height));
        if ((x, y) == (_scrollX, _scrollY))
            return;
        (_scrollX, _scrollY) = (x, y);
        Invalidate();
    }

    private void ClampScroll(TreeLayout layout)
    {
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, layout.ContentWidth - layout.Body.Width));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, layout.ContentHeight - layout.Body.Height));
    }

    private void SyncBars(TreeLayout layout)
    {
        var body = layout.Body;
        _verticalBar.Bounds = new Rect(body.Right, body.Y, ClassicScrollBar.Thickness, body.Height);
        _verticalBar.Maximum = Math.Max(0, layout.ContentHeight - body.Height);
        _verticalBar.ViewSize = body.Height;
        _verticalBar.ContentSize = layout.ContentHeight;
        _verticalBar.SmallChange = ItemHeight;
        _verticalBar.LargeChange = Math.Max(ItemHeight, body.Height - ItemHeight);

        _horizontalBar.Bounds = new Rect(body.X, body.Bottom, body.Width, ClassicScrollBar.Thickness);
        _horizontalBar.Maximum = Math.Max(0, layout.ContentWidth - body.Width);
        _horizontalBar.ViewSize = body.Width;
        _horizontalBar.ContentSize = layout.ContentWidth;
        _horizontalBar.SmallChange = Indent;
        _horizontalBar.LargeChange = Math.Max(Indent, body.Width - Indent);
    }

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }

    private readonly record struct TreeLayout(Rect Body, int ContentWidth, int ContentHeight, bool Vertical, bool Horizontal);
}

/// <summary>A node of a <see cref="TreeView"/>: its text, its child <see cref="Nodes"/> and any application data in <see cref="Tag"/>.</summary>
public class TreeNode
{
    private string _text;
    private bool _isExpanded;

    public TreeNode(string text, params TreeNode[] children)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        Nodes = new TreeNodeCollection(null, this);
        foreach (var child in children)
            Nodes.Add(child);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? throw new ArgumentNullException(nameof(value));
            TreeView?.OnTreeChanged();
        }
    }

    /// <summary>Child nodes.</summary>
    public TreeNodeCollection Nodes { get; }

    public TreeNode? Parent { get; internal set; }

    /// <summary>The tree view the node is shown in, or null while it is not part of one.</summary>
    public TreeView? TreeView { get; internal set; }

    /// <summary>Depth: 0 for a top-level node.</summary>
    public int Level => Parent == null ? 0 : Parent.Level + 1;

    /// <summary>Any data the application wants to keep with the node.</summary>
    public object? Tag { get; set; }

    /// <summary>Whether the children are shown. Collapsing a node that contains the selection selects the node.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetExpanded(value);
    }

    public void Expand() => IsExpanded = true;

    public void Collapse() => IsExpanded = false;

    public void Toggle() => IsExpanded = !IsExpanded;

    /// <summary>Expands this node and all nodes below it.</summary>
    public void ExpandAll()
    {
        IsExpanded = true;
        foreach (var child in Nodes)
            child.ExpandAll();
    }

    public override string ToString() => _text;

    internal void SetExpanded(bool expanded)
    {
        if (_isExpanded == expanded)
            return;
        _isExpanded = expanded;
        if (expanded)
            TreeView?.OnTreeChanged();
        else
            TreeView?.OnCollapsed(this);
    }

    /// <summary>Sets the tree view of this node and all nodes below it.</summary>
    internal void Attach(TreeView? treeView)
    {
        TreeView = treeView;
        foreach (var child in Nodes)
            child.Attach(treeView);
    }
}

/// <summary>Children of a <see cref="TreeNode"/>, or the top-level nodes of a <see cref="TreeView"/>.</summary>
public sealed class TreeNodeCollection : Collection<TreeNode>
{
    private readonly TreeView? _treeView;
    private readonly TreeNode? _owner;

    internal TreeNodeCollection(TreeView? treeView, TreeNode? owner)
    {
        _treeView = treeView;
        _owner = owner;
    }

    private TreeView? TreeView => _treeView ?? _owner?.TreeView;

    protected override void InsertItem(int index, TreeNode item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Parent != null || item.TreeView != null)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.NodeHasParent"));
        for (var ancestor = _owner; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor == item)
                throw new InvalidOperationException(Localization.Get("Doqua.Error.NodeInsideItself"));
        }
        item.Parent = _owner;
        item.Attach(TreeView);
        base.InsertItem(index, item);
        TreeView?.OnTreeChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        var treeView = TreeView;
        base.RemoveItem(index);
        Detach(item, treeView);
    }

    protected override void SetItem(int index, TreeNode item)
    {
        if (ReferenceEquals(this[index], item))
            return;
        RemoveItem(index);
        InsertItem(index, item);
    }

    protected override void ClearItems()
    {
        while (Count > 0)
            RemoveItem(Count - 1);
    }

    private static void Detach(TreeNode item, TreeView? treeView)
    {
        item.Parent = null;
        item.Attach(null);
        treeView?.OnRemoved(item);
    }
}
