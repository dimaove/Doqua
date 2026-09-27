using System.Collections.ObjectModel;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Drop-down list in the classic style: a sunken field showing the selected item and an arrow button;
/// clicking opens the list below (or above) it. Items are values of type <typeparamref name="T"/>, shown
/// with <see cref="ItemText"/>. The text cannot be edited.
/// <list type="bullet">
/// <item>Closed: Up/Down, PageUp/PageDown, Home/End change the selection; F4 or Alt+Down opens the list;
/// typing a letter selects the next item starting with it.</item>
/// <item>Open: the same keys move the highlight, Enter selects, Escape (or a click outside) cancels;
/// the mouse wheel and the scroll bar scroll the list.</item>
/// </list>
/// </summary>
public class ComboBox<T> : Control
{
    private const int EdgeSize = 2;
    private const int ButtonWidth = 16;
    private const int TextPadding = 3;

    private int _selectedIndex = -1;
    private Func<T, string>? _itemText;
    private string _placeholderText = "";
    private int _maxDropDownItems = 8;
    private ComboBoxDropDown<T>? _dropDown;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _background = Color.White;
    private Color _disabledBackground = ClassicStyle.Face;
    private Color _placeholderColor = ClassicStyle.Shadow;
    private Color _selectionBackground = new(0, 0, 128);
    private Color _selectionColor = Color.White;

    public ComboBox()
    {
        Items = new ComboBoxItemCollection<T>(this);
        Focusable = true;
        Width = 160;
        Height = Math.Max(22, Font.LineHeight + 6);
    }

    /// <summary>The choices, in list order. The selection follows when items are added or removed.</summary>
    public ComboBoxItemCollection<T> Items { get; }

    /// <summary>Raised after the selection changes, by the user or from code.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Index of the selected item, or -1 when nothing is selected.</summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, Items.Count);
            if (_selectedIndex == value)
                return;
            _selectedIndex = value;
            Invalidate();
            OnSelectionChanged(EventArgs.Empty);
        }
    }

    public bool HasSelection => _selectedIndex >= 0;

    /// <summary>
    /// The selected item; default(T) when nothing is selected (check <see cref="HasSelection"/>).
    /// Setting it selects the first equal item and throws if there is none.
    /// </summary>
    public T? SelectedItem
    {
        get => HasSelection ? Items[_selectedIndex] : default;
        set
        {
            var index = Items.IndexOf(value!);
            if (index < 0)
                throw new ArgumentException($"The item '{value}' is not in the list.", nameof(value));
            SelectedIndex = index;
        }
    }

    /// <summary>How an item is displayed; <c>ToString()</c> unless set.</summary>
    public Func<T, string> ItemText
    {
        get => _itemText ?? (item => item?.ToString() ?? "");
        set
        {
            _itemText = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Grey text shown while nothing is selected.</summary>
    public string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Number of items the open list shows before it needs a scroll bar.</summary>
    public int MaxDropDownItems
    {
        get => _maxDropDownItems;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxDropDownItems = value;
        }
    }

    public bool IsDroppedDown => _dropDown != null;

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

    public Color DisabledBackground
    {
        get => _disabledBackground;
        set => SetColor(ref _disabledBackground, value);
    }

    public Color PlaceholderColor
    {
        get => _placeholderColor;
        set => SetColor(ref _placeholderColor, value);
    }

    /// <summary>Background of the highlighted item in the list, and of the selected text while focused.</summary>
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

    /// <summary>Opens the list (if there are items and the control is in a window).</summary>
    public void ShowDropDown()
    {
        if (_dropDown != null || Items.Count == 0 || GetWindow() is not { } window || !IsEffectivelyEnabled)
            return;

        var dropDown = new ComboBoxDropDown<T>(this, Math.Min(Items.Count, _maxDropDownItems));
        var (x, below) = PointToWindow(0, Height);
        var above = below - Height - dropDown.Height;
        dropDown.X = Math.Clamp(x, 0, Math.Max(0, window.Width - dropDown.Width));
        dropDown.Y = below + dropDown.Height <= window.Height || above < 0 ? below : above;
        _dropDown = dropDown;
        window.OpenPopup(dropDown, () =>
        {
            _dropDown = null;
            Invalidate();
        });
        Invalidate();
    }

    /// <summary>Closes the list without changing the selection.</summary>
    public void CloseDropDown() => GetWindow()?.ClosePopup();

    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        // A click on the field while the list is open never gets here: the window closes the list first.
        if (e.Button == MouseButton.Left)
            ShowDropDown();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
            return;
        var alt = e.Modifiers == KeyModifiers.Alt;
        if (e.Key == Key.F4 && e.Modifiers == KeyModifiers.None || alt && e.Key is Key.Down or Key.Up)
        {
            e.Handled = true;
            ShowDropDown();
            return;
        }
        if (e.Modifiers != KeyModifiers.None || Items.Count == 0)
            return;

        var target = e.Key switch
        {
            Key.Up => Math.Max(0, _selectedIndex - 1),
            Key.Down => Math.Min(Items.Count - 1, _selectedIndex + 1),
            Key.PageUp => Math.Max(0, _selectedIndex - _maxDropDownItems),
            Key.PageDown => Math.Min(Items.Count - 1, Math.Max(0, _selectedIndex) + _maxDropDownItems),
            Key.Home => 0,
            Key.End => Items.Count - 1,
            _ => -2,
        };
        if (target == -2)
            return;
        e.Handled = true;
        SelectedIndex = target;
    }

    /// <summary>Typing a character selects the next item whose text starts with it.</summary>
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || e.Text.Length == 0 || Items.Count == 0)
            return;
        e.Handled = true;
        var found = FindByPrefix(e.Text, _selectedIndex);
        if (found >= 0)
            SelectedIndex = found;
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
        dc.FillRectangle(bounds, enabled ? _background : _disabledBackground);
        ClassicStyle.DrawSunkenEdge(dc, bounds, ClassicStyle.Highlight, ClassicStyle.Face, ClassicStyle.Shadow, ClassicStyle.DarkShadow);

        var inner = Rect.FromEdges(EdgeSize, EdgeSize, Width - EdgeSize, Height - EdgeSize);
        var button = Rect.FromEdges(Math.Max(inner.X, inner.Right - ButtonWidth), inner.Y, inner.Right, inner.Bottom);
        ClassicScrollBar.DrawArrowButton(dc, button, pointsUp: false, pressed: IsDroppedDown, enabled);

        var textArea = Rect.FromEdges(inner.X, inner.Y, button.X, inner.Bottom);
        var font = Font;
        var textY = textArea.Y + (textArea.Height - (font.Ascent + font.Descent)) / 2;
        var textX = textArea.X + TextPadding;
        var focused = enabled && Focused && !IsDroppedDown && GetWindow()?.IsActive == true;
        using (dc.PushClip(textArea))
        {
            if (focused)
            {
                // Classic focused drop-down list: the text is highlighted, with a dotted focus rectangle.
                var highlight = Rect.FromEdges(textArea.X + 1, textArea.Y + 1, textArea.Right - 1, textArea.Bottom - 1);
                dc.FillRectangle(highlight, _selectionBackground);
                ClassicStyle.DrawFocusRectangle(dc, highlight, _selectionColor);
            }

            if (HasSelection)
            {
                var text = ItemText(Items[_selectedIndex]);
                if (!enabled)
                    ClassicStyle.DrawEmbossedText(dc, text, font, _disabledColor, ClassicStyle.Highlight, textX, textY);
                else
                    dc.DrawText(text, font, focused ? _selectionColor : _color, textX, textY);
            }
            else if (_placeholderText.Length > 0)
            {
                dc.DrawText(_placeholderText, font, focused ? _selectionColor : _placeholderColor, textX, textY);
            }
        }
    }

    /// <summary>Index of the next item after <paramref name="start"/> whose text starts with <paramref name="prefix"/> (wrapping), or -1.</summary>
    internal int FindByPrefix(string prefix, int start)
    {
        for (var i = 1; i <= Items.Count; i++)
        {
            var index = ((start + i) % Items.Count + Items.Count) % Items.Count;
            if (ItemText(Items[index]).StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
                return index;
        }
        return -1;
    }

    /// <summary>The list chose an item: select it and close.</summary>
    internal void Commit(int index)
    {
        CloseDropDown();
        if (index >= 0 && index < Items.Count)
            SelectedIndex = index;
    }

    /// <summary>Keeps the selection on the same item when items are inserted or removed.</summary>
    internal void OnItemsChanged(int index, int removed, int inserted)
    {
        CloseDropDown();
        if (_selectedIndex >= 0)
        {
            if (_selectedIndex >= index && _selectedIndex < index + removed)
            {
                _selectedIndex = -1; // The selected item itself was removed or replaced.
                OnSelectionChanged(EventArgs.Empty);
            }
            else if (_selectedIndex >= index + removed)
            {
                _selectedIndex += inserted - removed;
            }
        }
        Invalidate();
    }

    internal int ItemHeight => Math.Max(16, Font.LineHeight + 2);

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}

/// <summary>Items of a <see cref="ComboBox{T}"/>; changes keep the combo box's selection consistent.</summary>
public sealed class ComboBoxItemCollection<T> : Collection<T>
{
    private readonly ComboBox<T> _owner;

    internal ComboBoxItemCollection(ComboBox<T> owner) => _owner = owner;

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        _owner.OnItemsChanged(index, 0, 1);
    }

    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        _owner.OnItemsChanged(index, 1, 0);
    }

    protected override void SetItem(int index, T item)
    {
        base.SetItem(index, item);
        _owner.OnItemsChanged(index, 1, 1);
    }

    protected override void ClearItems()
    {
        var count = Count;
        base.ClearItems();
        _owner.OnItemsChanged(0, count, 0);
    }
}

/// <summary>The open list of a combo box, hosted by the window above its content.</summary>
internal sealed class ComboBoxDropDown<T> : Control
{
    private const int Border = 1;
    private const int TextPadding = 3;
    private const int WheelItems = 3;

    private readonly ComboBox<T> _combo;
    private readonly int _visibleCount;
    private readonly int _itemHeight;
    private readonly ClassicScrollBar _scrollBar;
    private int _top;       // First visible item.
    private int _highlighted;

    public ComboBoxDropDown(ComboBox<T> combo, int visibleCount)
    {
        _combo = combo;
        _visibleCount = visibleCount;
        _itemHeight = combo.ItemHeight;
        _highlighted = Math.Max(0, combo.SelectedIndex);
        _scrollBar = new ClassicScrollBar(this, () => _top, ScrollTo);
        Cursor = Cursor.Arrow;
        Width = combo.Width;
        Height = visibleCount * _itemHeight + 2 * Border;
        EnsureVisible(_highlighted);
    }

    private int Count => _combo.Items.Count;

    private bool HasScrollBar => Count > _visibleCount && Height >= 2 * ClassicScrollBar.Thickness;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left && HasScrollBar)
        {
            SyncScrollBar();
            if (_scrollBar.Bounds.Contains(e.X, e.Y))
                _scrollBar.Press(e.X, e.Y);
        }
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_scrollBar.IsPressed)
        {
            _scrollBar.Drag(e.Y);
            return;
        }
        if (IndexAt(e.X, e.Y) is var index and >= 0)
            Highlight(index);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _scrollBar.Release();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButton.Left && IndexAt(e.X, e.Y) is var index and >= 0)
            _combo.Commit(index);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;
        ScrollTo(_top - e.Delta * WheelItems);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // The open list takes all keys.
        var alt = e.Modifiers == KeyModifiers.Alt;
        switch (e.Key)
        {
            case Key.Escape:
            case Key.F4:
            case Key.Up or Key.Down when alt:
                _combo.CloseDropDown();
                return;
            case Key.Enter:
                _combo.Commit(_highlighted);
                return;
            case Key.Up:
                Highlight(Math.Max(0, _highlighted - 1));
                break;
            case Key.Down:
                Highlight(Math.Min(Count - 1, _highlighted + 1));
                break;
            case Key.PageUp:
                Highlight(Math.Max(0, _highlighted - _visibleCount));
                break;
            case Key.PageDown:
                Highlight(Math.Min(Count - 1, _highlighted + _visibleCount));
                break;
            case Key.Home:
                Highlight(0);
                break;
            case Key.End:
                Highlight(Count - 1);
                break;
            default:
                return;
        }
        EnsureVisible(_highlighted);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, Width, Height);
        dc.FillRectangle(bounds, _combo.Background);
        dc.DrawRectangle(bounds, Color.Black); // Classic drop-down lists have a plain black frame.

        var font = _combo.Font;
        var textOffset = (_itemHeight - (font.Ascent + font.Descent)) / 2;
        var right = Width - Border - (HasScrollBar ? ClassicScrollBar.Thickness : 0);
        using (dc.PushClip(Rect.FromEdges(Border, Border, right, Height - Border)))
        {
            for (var i = 0; i < _visibleCount && _top + i < Count; i++)
            {
                var index = _top + i;
                var y = Border + i * _itemHeight;
                var highlighted = index == _highlighted;
                if (highlighted)
                    dc.FillRectangle(Border, y, right - Border, _itemHeight, _combo.SelectionBackground);
                dc.DrawText(_combo.ItemText(_combo.Items[index]), font, highlighted ? _combo.SelectionColor : _combo.Color,
                    Border + TextPadding, y + textOffset);
            }
        }

        if (HasScrollBar)
        {
            SyncScrollBar();
            _scrollBar.Draw(dc, enabled: true);
        }
    }

    private int IndexAt(int x, int y)
    {
        var right = Width - Border - (HasScrollBar ? ClassicScrollBar.Thickness : 0);
        if (x < Border || x >= right || y < Border || y >= Height - Border)
            return -1;
        var index = _top + (y - Border) / _itemHeight;
        return index < Count ? index : -1;
    }

    private void Highlight(int index)
    {
        if (_highlighted == index)
            return;
        _highlighted = index;
        Invalidate();
    }

    private void EnsureVisible(int index)
    {
        if (index < _top)
            ScrollTo(index);
        else if (index >= _top + _visibleCount)
            ScrollTo(index - _visibleCount + 1);
    }

    private void ScrollTo(int top)
    {
        var clamped = Math.Clamp(top, 0, Math.Max(0, Count - _visibleCount));
        if (clamped == _top)
            return;
        _top = clamped;
        Invalidate();
    }

    private void SyncScrollBar()
    {
        _scrollBar.Bounds = Rect.FromEdges(Width - Border - ClassicScrollBar.Thickness, Border, Width - Border, Height - Border);
        _scrollBar.Maximum = Math.Max(0, Count - _visibleCount);
        _scrollBar.ViewSize = _visibleCount;
        _scrollBar.ContentSize = Count;
        _scrollBar.SmallChange = 1;
        _scrollBar.LargeChange = Math.Max(1, _visibleCount - 1);
    }
}
