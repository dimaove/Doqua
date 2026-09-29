using System.Collections.ObjectModel;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A window's menu bar in the classic style: a strip of titles ("File", "Edit", ...) across the top of its parent, each
/// opening a drop-down list of <see cref="MenuItem"/>s. Add it to the window's root panel, above (or before) the other
/// controls; it anchors itself to the top edge and stretches across. Leave <see cref="Control.Height"/> pixels free
/// for it at the top.
/// <list type="bullet">
/// <item>Mouse: a click on a title opens its menu; while a menu is open, moving to another title opens that one; a click
/// on an item chooses it, a click outside closes the menu.</item>
/// <item>Keyboard: Alt + the letter marked with '&amp;' in a title ("&amp;File" = Alt+F) or F10 open a menu; Left / Right
/// move to the neighbouring menu, Up / Down move in the list, Enter chooses, Escape closes.</item>
/// <item>Shortcuts: an item's <see cref="MenuItem.ShortcutText"/> ("Ctrl+O", "Ctrl+Shift+S", "F5", "Del") also works
/// as a keyboard shortcut in the whole window, after the focused control had the key (so an <see cref="Input"/> keeps
/// its own Ctrl+C). Disabled items and items of disabled menus do not react.</item>
/// </list>
/// Choosing an item raises <see cref="ItemClick"/>, then the item's own <see cref="MenuItem.Click"/>.
/// <example>
/// <code>
/// var open = new MenuItem("Open...", "Ctrl+O");
/// open.Click += (sender, e) => OpenFile();
/// var menu = new MainMenu
/// {
///     Items =
///     {
///         new MainMenuItem("&amp;File") { Items = { open, MenuItem.Separator(), new MenuItem("Exit") } },
///         new MainMenuItem("&amp;Help") { Items = { new MenuItem("About") } },
///     },
/// };
/// root.Children.Add(menu);
/// </code>
/// </example>
/// </summary>
public class MainMenu : Control
{
    internal const int TitlePadding = 8;
    private const int FirstTitleX = 2;

    private Font? _font;
    private Color _color = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;
    private Color _background = ClassicStyle.Face;
    private int _hot = -1;               // Title under the pointer while no menu is open.
    private MainMenuPopup? _popup;

    /// <summary>Creates an empty menu bar, anchored to the top edge of its parent.</summary>
    public MainMenu()
    {
        Items = new MainMenuItemCollection(this);
        Cursor = Cursor.Arrow;
        Height = Font.LineHeight + 6;
        Anchor = new Anchor(Left: 0, Top: 0, Right: 0);
    }

    /// <summary>The titles, left to right.</summary>
    public MainMenuItemCollection Items { get; }

    /// <summary>Raised when an item of one of the menus is chosen, before the item's own <see cref="MenuItem.Click"/>.</summary>
    public event EventHandler<MenuItemEventArgs>? ItemClick;

    /// <summary>True while one of the menus is open.</summary>
    public bool IsOpen => _popup != null;

    /// <summary>Font of the titles and of the drop-down menus; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            Height = _font.LineHeight + 6;
            Invalidate();
        }
    }

    /// <summary>Text color of the titles and items.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    /// <summary>Text color of disabled titles and items.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    /// <summary>Face color of the bar and of the menus.</summary>
    public Color Background
    {
        get => _background;
        set => SetColor(ref _background, value);
    }

    /// <summary>Background of the highlighted item in a menu.</summary>
    public Color SelectionBackground { get; set; } = new(0, 0, 128);

    /// <summary>Text color of the highlighted item in a menu.</summary>
    public Color SelectionColor { get; set; } = Color.White;

    /// <summary>Opens the menu of the title at <paramref name="index"/> (if it and the bar are enabled and shown in a window).</summary>
    public void Open(int index) => Open(index, fromKeyboard: false);

    /// <summary>Closes the open menu, if any, without choosing an item.</summary>
    public void Close() => GetWindow()?.ClosePopup();

    protected virtual void OnItemClick(MenuItemEventArgs e) => ItemClick?.Invoke(this, e);

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        SetHot(TitleAt(e.X));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHot(-1);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Left && TitleAt(e.X) is var index and >= 0)
            Open(index, fromKeyboard: false);
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.FillRectangle(0, 0, Width, Height, _background);
        DrawTitles(dc, 0, 0, _popup?.OpenIndex ?? -1, _popup == null ? _hot : -1);
    }

    /// <summary>Alt + a title's mnemonic, F10, and the items' shortcut keys.</summary>
    internal override void ProcessShortcut(KeyEventArgs e)
    {
        if (_popup != null || Items.Count == 0)
            return;
        if (e.Modifiers == KeyModifiers.Alt && KeyChar(e.Key) is { } ch)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                if (Items[i].Enabled && char.ToUpperInvariant(Items[i].Mnemonic) == ch)
                {
                    e.Handled = true;
                    Open(i, fromKeyboard: true);
                    return;
                }
            }
        }
        if (e.Key == Key.F10 && e.Modifiers == KeyModifiers.None && NextEnabled(-1, 1) is var first and >= 0)
        {
            e.Handled = true;
            Open(first, fromKeyboard: true);
            return;
        }
        foreach (var title in Items)
        {
            if (!title.Enabled)
                continue;
            foreach (var item in title.Items)
            {
                if (item.IsSelectable && Shortcut.Parse(item.ShortcutText) is var (key, modifiers) && key == e.Key && modifiers == e.Modifiers)
                {
                    e.Handled = true;
                    Choose(item);
                    return;
                }
            }
        }
    }

    // ------------------------------------------------------------------ used by the popup

    /// <summary>Left edge of each title and the width of the bar's titles, in the bar's coordinates.</summary>
    internal (int X, int Width) TitleBounds(int index)
    {
        var x = FirstTitleX;
        for (var i = 0; i < index; i++)
            x += TitleWidth(i);
        return (x, TitleWidth(index));
    }

    internal int TitleAt(int x)
    {
        var left = FirstTitleX;
        for (var i = 0; i < Items.Count; i++)
        {
            var width = TitleWidth(i);
            if (x >= left && x < left + width)
                return i;
            left += width;
        }
        return -1;
    }

    /// <summary>The next enabled title after <paramref name="index"/> in the direction <paramref name="step"/> (wrapping), or -1.</summary>
    internal int NextEnabled(int index, int step)
    {
        var count = Items.Count;
        for (var i = 1; i <= count; i++)
        {
            var candidate = ((index + step * i) % count + count) % count;
            if (Items[candidate].Enabled && Items[candidate].Items.Count > 0)
                return candidate;
        }
        return -1;
    }

    /// <summary>
    /// Draws the titles with the bar's top-left corner at (<paramref name="x"/>, <paramref name="y"/>): the open title
    /// pressed, the hot one raised.
    /// </summary>
    internal void DrawTitles(DrawingContext dc, int x, int y, int open, int hot)
    {
        var font = Font;
        var textY = y + (Height - (font.Ascent + font.Descent)) / 2;
        var enabled = IsEffectivelyEnabled;
        for (var i = 0; i < Items.Count; i++)
        {
            var (left, width) = TitleBounds(i);
            var item = Items[i];
            var box = new Rect(x + left, y + 1, width, Height - 2);
            var itemEnabled = enabled && item.Enabled;
            var shift = 0;
            if (i == open)
            {
                DrawThinEdge(dc, box, ClassicStyle.Shadow, ClassicStyle.Highlight);
                shift = 1;
            }
            else if (i == hot && itemEnabled)
            {
                DrawThinEdge(dc, box, ClassicStyle.Highlight, ClassicStyle.Shadow);
            }
            DrawMnemonicText(dc, item, font, itemEnabled, x + left + TitlePadding + shift, textY + shift);
        }
    }

    internal PopupMenu CreatePopupMenu() => new()
    {
        Font = Font,
        Color = _color,
        DisabledColor = _disabledColor,
        Background = _background,
        SelectionBackground = SelectionBackground,
        SelectionColor = SelectionColor,
    };

    /// <summary>An item was chosen (or null: the menu was dismissed): close, then report it.</summary>
    internal void Choose(MenuItem? item)
    {
        Close();
        if (item == null)
            return;
        OnItemClick(new MenuItemEventArgs(item));
        item.RaiseClick();
    }

    internal void OnItemsChanged() => Invalidate();

    private void Open(int index, bool fromKeyboard)
    {
        if (index < 0 || index >= Items.Count || !Items[index].Enabled || !IsEffectivelyEnabled || GetWindow() is not { } window)
            return;
        var popup = new MainMenuPopup(this, window);
        _popup = popup;
        _hot = -1;
        window.OpenPopup(popup, () =>
        {
            _popup = null;
            Invalidate();
        });
        popup.Show(index, fromKeyboard);
        Invalidate();
    }

    private int TitleWidth(int index) => Font.MeasureText(Items[index].DisplayText).Width + 2 * TitlePadding;

    private void SetHot(int index)
    {
        if (_hot == index)
            return;
        _hot = index;
        Invalidate();
    }

    private void DrawMnemonicText(DrawingContext dc, MainMenuItem item, Font font, bool enabled, int x, int y)
    {
        var text = item.DisplayText;
        if (enabled)
            dc.DrawText(text, font, _color, x, y);
        else
            ClassicStyle.DrawEmbossedText(dc, text, font, _disabledColor, ClassicStyle.Highlight, x, y);
        if (item.MnemonicIndex is var at and >= 0)
        {
            var left = x + font.MeasureText(text[..at]).Width;
            var width = font.MeasureText(text[at..(at + 1)]).Width;
            dc.FillRectangle(left, y + font.Ascent + 1, width, 1, enabled ? _color : _disabledColor);
        }
    }

    /// <summary>1 px edge: <paramref name="topLeft"/> on the top and left, <paramref name="bottomRight"/> on the bottom and right.</summary>
    private static void DrawThinEdge(DrawingContext dc, Rect r, Color topLeft, Color bottomRight)
    {
        dc.FillRectangle(r.X, r.Y, r.Width - 1, 1, topLeft);
        dc.FillRectangle(r.X, r.Y, 1, r.Height - 1, topLeft);
        dc.FillRectangle(r.X, r.Bottom - 1, r.Width, 1, bottomRight);
        dc.FillRectangle(r.Right - 1, r.Y, 1, r.Height, bottomRight);
    }

    private static char? KeyChar(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null,
    };

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }
}

/// <summary>A title of a <see cref="MainMenu"/> with its drop-down items.</summary>
public class MainMenuItem
{
    private string _text;
    private bool _enabled = true;

    /// <summary>
    /// Creates a title. A '&amp;' before a letter marks it as the mnemonic (underlined; Alt + that letter opens the
    /// menu); "&amp;&amp;" shows a single '&amp;'.
    /// </summary>
    public MainMenuItem(string text)
    {
        _text = text ?? throw new ArgumentNullException(nameof(text));
        ParseText();
    }

    /// <summary>The title, with an optional '&amp;' before the mnemonic letter.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? throw new ArgumentNullException(nameof(value));
            ParseText();
            Menu?.OnItemsChanged();
        }
    }

    /// <summary>A disabled title is grey and its menu cannot be opened; its items' shortcuts do not work either.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Menu?.OnItemsChanged();
        }
    }

    /// <summary>The drop-down items, from top to bottom (<see cref="MenuItem.Separator"/> for lines between groups).</summary>
    public List<MenuItem> Items { get; } = [];

    /// <summary>Any data the application wants to attach.</summary>
    public object? Tag { get; set; }

    internal MainMenu? Menu { get; set; }

    /// <summary>The title as shown: without the mnemonic marker.</summary>
    internal string DisplayText { get; private set; } = "";

    /// <summary>Index of the mnemonic letter in <see cref="DisplayText"/>, or -1.</summary>
    internal int MnemonicIndex { get; private set; } = -1;

    internal char Mnemonic => MnemonicIndex >= 0 ? DisplayText[MnemonicIndex] : '\0';

    private void ParseText()
    {
        var shown = new System.Text.StringBuilder();
        MnemonicIndex = -1;
        for (var i = 0; i < _text.Length; i++)
        {
            if (_text[i] == '&' && i + 1 < _text.Length)
            {
                i++;
                if (_text[i] != '&' && MnemonicIndex < 0)
                    MnemonicIndex = shown.Length;
            }
            shown.Append(_text[i]);
        }
        DisplayText = shown.ToString();
    }
}

/// <summary>Titles of a <see cref="MainMenu"/>.</summary>
public sealed class MainMenuItemCollection : Collection<MainMenuItem>
{
    private readonly MainMenu _owner;

    internal MainMenuItemCollection(MainMenu owner) => _owner = owner;

    protected override void InsertItem(int index, MainMenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Menu = _owner;
        base.InsertItem(index, item);
        _owner.OnItemsChanged();
    }

    protected override void SetItem(int index, MainMenuItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        this[index].Menu = null;
        item.Menu = _owner;
        base.SetItem(index, item);
        _owner.OnItemsChanged();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Menu = null;
        base.RemoveItem(index);
        _owner.OnItemsChanged();
    }

    protected override void ClearItems()
    {
        foreach (var item in this)
            item.Menu = null;
        base.ClearItems();
        _owner.OnItemsChanged();
    }
}

/// <summary>A menu item was chosen.</summary>
public class MenuItemEventArgs(MenuItem item) : EventArgs
{
    /// <summary>The chosen item.</summary>
    public MenuItem Item { get; } = item;
}

/// <summary>
/// The open menu of a <see cref="MainMenu"/>, hosted by the window as a popup. It covers the bar as well as the drop-down
/// list, so that the pointer moving to another title (which a popup would otherwise not see) switches menus.
/// </summary>
internal sealed class MainMenuPopup : Control
{
    private readonly MainMenu _bar;
    private readonly Window _window;
    private PopupMenuView? _list;
    private Rect _barArea;   // Where the bar is, in the popup's coordinates.
    private Rect _listArea;  // Where the drop-down list is, in the popup's coordinates.

    public MainMenuPopup(MainMenu bar, Window window)
    {
        _bar = bar;
        _window = window;
        Cursor = Cursor.Arrow;
    }

    public int OpenIndex { get; private set; } = -1;

    protected override IReadOnlyList<Control> VisualChildren => _list == null ? [] : [_list];

    /// <summary>Opens (or switches to) the menu of title <paramref name="index"/>.</summary>
    public void Show(int index, bool fromKeyboard)
    {
        OpenIndex = index;
        var menu = _bar.CreatePopupMenu();
        var list = new PopupMenuView(menu, [.. _bar.Items[index].Items], _bar.Choose) { Parent = this };
        if (fromKeyboard)
            list.HighlightFirst();

        // In window coordinates: the bar, and the list below its title (moved left to fit the window).
        var (barX, barY) = _bar.PointToWindow(0, 0);
        var bar = new Rect(barX, barY, _bar.Width, _bar.Height);
        var (titleX, _) = _bar.TitleBounds(index);
        var listX = Math.Clamp(barX + titleX, 0, Math.Max(0, _window.Width - list.Width));
        var list0 = new Rect(listX, bar.Bottom, list.Width, Math.Min(list.Height, Math.Max(0, _window.Height - bar.Bottom)));
        var bounds = Rect.FromEdges(Math.Min(bar.X, list0.X), bar.Y, Math.Max(bar.Right, list0.Right), Math.Max(bar.Bottom, list0.Bottom));

        _barArea = new Rect(bar.X - bounds.X, bar.Y - bounds.Y, bar.Width, bar.Height);
        _listArea = new Rect(list0.X - bounds.X, list0.Y - bounds.Y, list0.Width, list0.Height);
        list.Bounds = _listArea;
        _list = list;
        Bounds = bounds;
        Invalidate();
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        // Moving to another title while a menu is open opens that menu.
        if (TitleAt(e.X, e.Y) is var index and >= 0 && index != OpenIndex && _bar.Items[index].Enabled)
            Show(index, fromKeyboard: false);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var index = TitleAt(e.X, e.Y);
        if (index == OpenIndex || !_listArea.Contains(e.X, e.Y) && index < 0)
            _bar.Close(); // A click on the open title, or beside the list (inside the popup's box): close.
        else if (index >= 0 && _bar.Items[index].Enabled)
            Show(index, fromKeyboard: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true; // An open menu takes all keys.
        if (e.Key is Key.Left or Key.Right && e.Modifiers == KeyModifiers.None)
        {
            if (_bar.NextEnabled(OpenIndex, e.Key == Key.Right ? 1 : -1) is var next and >= 0 && next != OpenIndex)
                Show(next, fromKeyboard: true);
            return;
        }
        if (e.Key == Key.F10 || e.Key == Key.Up && e.Modifiers == KeyModifiers.Alt)
        {
            _bar.Close();
            return;
        }
        _list?.HandleKey(e);
    }

    protected override void OnRender(DrawingContext dc)
    {
        // The bar, drawn over the real one with the open title pressed; the list is the child.
        dc.FillRectangle(_barArea, _bar.Background);
        _bar.DrawTitles(dc, _barArea.X, _barArea.Y, OpenIndex, -1);
    }

    private int TitleAt(int x, int y) => _barArea.Contains(x, y) ? _bar.TitleAt(x - _barArea.X) : -1;
}

/// <summary>Parses shortcut labels such as "Ctrl+O", "Ctrl+Shift+S", "Alt+X", "F5", "Del" into a key and modifiers.</summary>
internal static class Shortcut
{
    private static readonly Dictionary<string, Key> s_names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Del"] = Key.Delete, ["Ins"] = Key.Insert, ["Esc"] = Key.Escape, ["Return"] = Key.Enter,
        ["PgUp"] = Key.PageUp, ["PgDn"] = Key.PageDown, ["PageUp"] = Key.PageUp, ["PageDown"] = Key.PageDown,
    };

    public static (Key Key, KeyModifiers Modifiers)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var modifiers = KeyModifiers.None;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL" or "CONTROL": modifiers |= KeyModifiers.Control; break;
                case "SHIFT": modifiers |= KeyModifiers.Shift; break;
                case "ALT": modifiers |= KeyModifiers.Alt; break;
                default: return null;
            }
        }
        var name = parts[^1];
        if (name.Length == 1 && char.IsAsciiLetterOrDigit(name[0]))
        {
            var ch = char.ToUpperInvariant(name[0]);
            return (char.IsAsciiDigit(ch) ? Key.D0 + (ch - '0') : Key.A + (ch - 'A'), modifiers);
        }
        if (s_names.TryGetValue(name, out var named))
            return (named, modifiers);
        return Enum.TryParse<Key>(name, ignoreCase: true, out var key) && key != Key.None && !int.TryParse(name, out _)
            ? (key, modifiers)
            : null;
    }
}
