using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Menu shown on top of a window's content, in the classic style. It closes when an item is chosen,
/// on a click outside it, on Escape, or when the window is deactivated or resized; then
/// <see cref="Closed"/> reports the chosen item, or null. Items: mouse (left or right button),
/// Up/Down and Enter. Open it with <see cref="Show"/> or through <see cref="Control.ContextMenu"/>.
/// </summary>
/// <remarks>The menu is drawn inside its window, so it is moved to fit there when opened near an edge.</remarks>
public class PopupMenu
{
    private Window? _window;
    private Action<MenuItem?>? _onClosed;
    private MenuItem? _result;
    private Font? _font;

    /// <summary>Items from top to bottom. An item may also be used in other menus.</summary>
    public List<MenuItem> Items { get; } = [];

    /// <summary>Font of the items; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set => _font = value ?? throw new ArgumentNullException(nameof(value));
    }

    public Color Color { get; set; } = Color.Black;
    public Color DisabledColor { get; set; } = ClassicStyle.Shadow;
    public Color Background { get; set; } = ClassicStyle.Face;

    /// <summary>Background of the item under the pointer or chosen with the arrow keys.</summary>
    public Color SelectionBackground { get; set; } = new(0, 0, 128);

    /// <summary>Text color of the highlighted item.</summary>
    public Color SelectionColor { get; set; } = Color.White;

    public bool IsOpen => _window != null;

    /// <summary>Control that opened the menu most recently (kept after it closes, e.g. for <see cref="Closed"/> handlers).</summary>
    public Control? Owner { get; private set; }

    /// <summary>Raised when the menu closes: with the chosen item, or null if it was dismissed.</summary>
    public event EventHandler<MenuClosedEventArgs>? Closed;

    /// <summary>
    /// Opens the menu with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>) in
    /// <paramref name="owner"/>'s coordinates. <paramref name="onClosed"/> receives the chosen item or null.
    /// A menu that is already open, here or elsewhere in the window, is closed first (with null).
    /// </summary>
    public void Show(Control owner, int x, int y, Action<MenuItem?>? onClosed = null)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var window = owner.GetWindow() ?? throw new InvalidOperationException(Localization.Get("Doqua.Error.NotInWindow"));
        Close();

        var view = new PopupMenuView(this, [.. Items]);
        var (left, top) = owner.PointToWindow(x, y);
        // Near the right or bottom edge, open to the left of / above the point, like native menus.
        if (left + view.Width > window.Width)
            left -= view.Width;
        if (top + view.Height > window.Height)
            top -= view.Height;
        view.X = Math.Clamp(left, 0, Math.Max(0, window.Width - view.Width));
        view.Y = Math.Clamp(top, 0, Math.Max(0, window.Height - view.Height));

        Owner = owner;
        _window = window;
        _onClosed = onClosed;
        _result = null;
        window.OpenPopup(view, OnPopupClosed);
    }

    /// <summary>Closes the menu without choosing an item.</summary>
    public void Close() => Close(null);

    protected virtual void OnClosed(MenuClosedEventArgs e) => Closed?.Invoke(this, e);

    /// <summary>Closes with <paramref name="item"/> as the result.</summary>
    internal void Close(MenuItem? item)
    {
        if (_window == null)
            return;
        _result = item;
        _window.ClosePopup(); // Calls OnPopupClosed.
    }

    /// <summary>Reports a result for this menu when another menu showed its items (see Input's context menu).</summary>
    internal void RaiseClosed(Control? owner, MenuItem? item)
    {
        Owner = owner;
        OnClosed(new MenuClosedEventArgs(item));
    }

    private void OnPopupClosed()
    {
        var item = _result;
        var onClosed = _onClosed;
        (_window, _onClosed, _result) = (null, null, null);
        item?.RaiseClick();
        OnClosed(new MenuClosedEventArgs(item));
        onClosed?.Invoke(item);
    }
}

public class MenuItem
{
    public MenuItem()
    {
    }

    public MenuItem(string text, string? shortcutText = null)
    {
        Text = text;
        ShortcutText = shortcutText;
    }

    public string Text { get; set; } = "";

    /// <summary>Shown right-aligned, e.g. "Ctrl+C". Display only: it does not register a shortcut.</summary>
    public string? ShortcutText { get; set; }

    /// <summary>Disabled items are grey and cannot be chosen.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>A separator is a horizontal line between groups of items.</summary>
    public bool IsSeparator { get; init; }

    /// <summary>Any data the application wants to attach.</summary>
    public object? Tag { get; set; }

    /// <summary>Raised when the item is chosen, before the menu's Closed event.</summary>
    public event EventHandler? Click;

    public static MenuItem Separator() => new() { IsSeparator = true };

    internal bool IsSelectable => Enabled && !IsSeparator;

    internal void RaiseClick() => Click?.Invoke(this, EventArgs.Empty);
}

public class MenuClosedEventArgs(MenuItem? selectedItem) : EventArgs
{
    /// <summary>The chosen item, or null if the menu was closed without choosing one.</summary>
    public MenuItem? SelectedItem { get; } = selectedItem;
}

/// <summary>The open menu as a control, hosted by the window above its content.</summary>
internal sealed class PopupMenuView : Control
{
    private const int Edge = 3;           // 3D frame plus 1 px padding.
    private const int TextLeft = 20;      // Classic space for a check mark or icon.
    private const int TextRight = 16;
    private const int ShortcutGap = 30;
    private const int SeparatorHeight = 9;

    private readonly PopupMenu _menu;
    private readonly MenuItem[] _items;
    private readonly int[] _tops;
    private readonly int _itemHeight;
    private int _highlighted = -1;

    public PopupMenuView(PopupMenu menu, MenuItem[] items)
    {
        Cursor = Cursor.Arrow;
        _menu = menu;
        _items = items;
        _tops = new int[items.Length];

        var font = menu.Font;
        _itemHeight = Math.Max(18, font.LineHeight + 6);
        var widest = 0;
        var y = Edge;
        for (var i = 0; i < items.Length; i++)
        {
            _tops[i] = y;
            y += items[i].IsSeparator ? SeparatorHeight : _itemHeight;
            if (!items[i].IsSeparator)
            {
                var width = font.MeasureText(items[i].Text).Width;
                if (items[i].ShortcutText is { Length: > 0 } shortcut)
                    width += ShortcutGap + font.MeasureText(shortcut).Width;
                widest = Math.Max(widest, width);
            }
        }
        Width = Math.Max(120, 2 * Edge + TextLeft + widest + TextRight);
        Height = y + Edge;
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        var index = IndexAt(e.X, e.Y);
        Highlight(index >= 0 && _items[index].IsSelectable ? index : -1);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        Highlight(-1);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        var index = IndexAt(e.X, e.Y);
        if (index >= 0 && _items[index].IsSelectable && e.Button != MouseButton.Middle)
            _menu.Close(_items[index]); // Separators and disabled items keep the menu open.
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // The menu takes all keys while it is open.
        switch (e.Key)
        {
            case Key.Up:
                Highlight(NextSelectable(-1));
                break;
            case Key.Down:
                Highlight(NextSelectable(1));
                break;
            case Key.Enter or Key.Space when _highlighted >= 0:
                _menu.Close(_items[_highlighted]);
                break;
            case Key.Escape:
                _menu.Close(null);
                break;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, _menu.Background);
        ClassicStyle.DrawRaisedEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var font = _menu.Font;
        var textHeight = font.Ascent + font.Descent;
        for (var i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            var top = _tops[i];
            if (item.IsSeparator)
            {
                // Engraved line: shadow with a highlight line below.
                dc.FillRectangle(Edge, top + SeparatorHeight / 2 - 1, Width - 2 * Edge, 1, ClassicStyle.Shadow);
                dc.FillRectangle(Edge, top + SeparatorHeight / 2, Width - 2 * Edge, 1, ClassicStyle.Highlight);
                continue;
            }

            var highlighted = i == _highlighted;
            if (highlighted)
                dc.FillRectangle(Edge, top, Width - 2 * Edge, _itemHeight, _menu.SelectionBackground);

            var textY = top + (_itemHeight - textHeight) / 2;
            var shortcutX = item.ShortcutText is { Length: > 0 } shortcut
                ? Width - Edge - TextRight - font.MeasureText(shortcut).Width
                : 0;
            if (!item.Enabled)
            {
                ClassicStyle.DrawEmbossedText(dc, item.Text, font, _menu.DisabledColor, ClassicStyle.Highlight, Edge + TextLeft, textY);
                if (shortcutX > 0)
                    ClassicStyle.DrawEmbossedText(dc, item.ShortcutText!, font, _menu.DisabledColor, ClassicStyle.Highlight, shortcutX, textY);
                continue;
            }
            var color = highlighted ? _menu.SelectionColor : _menu.Color;
            dc.DrawText(item.Text, font, color, Edge + TextLeft, textY);
            if (shortcutX > 0)
                dc.DrawText(item.ShortcutText!, font, color, shortcutX, textY);
        }
    }

    private int IndexAt(int x, int y)
    {
        if (x < Edge || x >= Width - Edge)
            return -1;
        for (var i = 0; i < _items.Length; i++)
        {
            var height = _items[i].IsSeparator ? SeparatorHeight : _itemHeight;
            if (y >= _tops[i] && y < _tops[i] + height)
                return i;
        }
        return -1;
    }

    /// <summary>The next (<paramref name="step"/> 1) or previous (-1) selectable item, wrapping around.</summary>
    private int NextSelectable(int step)
    {
        var count = _items.Length;
        var start = _highlighted >= 0 ? _highlighted : step > 0 ? -1 : count;
        for (var i = 1; i <= count; i++)
        {
            var index = ((start + step * i) % count + count) % count;
            if (_items[index].IsSelectable)
                return index;
        }
        return -1;
    }

    private void Highlight(int index)
    {
        if (_highlighted == index)
            return;
        _highlighted = index;
        Invalidate();
    }
}
