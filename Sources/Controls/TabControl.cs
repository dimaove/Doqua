using System.Collections.ObjectModel;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Pages with a strip of tabs on top, drawn by <see cref="Style.Current"/>. The selected page
/// fills the area below the tabs; the others are hidden. Disabled pages cannot be selected and their
/// tabs are grey; if every page is disabled, no page is selected or shown.
/// Keys (while focus is inside): Ctrl+Tab / Ctrl+PageDown next page, Ctrl+Shift+Tab / Ctrl+PageUp previous.
/// </summary>
public class TabControl : Control
{
    private const int TabPadding = 10;   // Space left and right of a tab's title.
    private const int SelectedGrowth = 2; // The selected tab is this much taller and wider on each side.

    private readonly ControlCollection _children; // Same pages, in the same order, as the control's children.
    private TabPage? _selectedPage;
    private Font? _font;
    private Color? _color;
    private Color? _disabledColor;
    private Color? _background;
    private TabPage? _hotPage; // Tab under the pointer.

    /// <summary>Creates an empty tab control, 300 x 200 pixels.</summary>
    public TabControl()
    {
        _children = new ControlCollection(this);
        Pages = new TabPageCollection(this);
        Width = 300;
        Height = 200;
    }

    /// <summary>The pages, in tab order.</summary>
    public TabPageCollection Pages { get; }

    /// <summary>
    /// The page that is shown, or null when there are no enabled pages. Setting a disabled page or
    /// a page of another TabControl throws; setting null is only allowed when no page is enabled.
    /// </summary>
    public TabPage? SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (value == _selectedPage)
                return;
            if (value == null)
            {
                if (Pages.Any(page => page.Enabled))
                    throw new InvalidOperationException(Localization.Get("Doqua.Error.PageMustBeSelected"));
            }
            else if (!Pages.Contains(value))
            {
                throw new ArgumentException(Localization.Get("Doqua.Error.PageNotInTabControl"), nameof(value));
            }
            else if (!value.Enabled)
            {
                throw new InvalidOperationException(Localization.Get("Doqua.Error.DisabledPage"));
            }
            SetSelectedPage(value);
        }
    }

    /// <summary>Index of <see cref="SelectedPage"/> in <see cref="Pages"/>, or -1.</summary>
    public int SelectedIndex
    {
        get => _selectedPage == null ? -1 : Pages.IndexOf(_selectedPage);
        set => SelectedPage = value == -1 ? null : Pages[value];
    }

    /// <summary>Raised after <see cref="SelectedPage"/> changes.</summary>
    public event EventHandler? SelectedPageChanged;

    /// <summary>Font of the tab titles; <see cref="GUI.Font.Default"/> unless set.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _font = value;
            LayoutPages(); // The tab strip height follows the font.
            Invalidate();
        }
    }

    /// <summary>Tab title color; the style's text color unless set.</summary>
    public Color Color
    {
        get => _color ?? Style.Current.Text;
        set
        {
            _color = value;
            Invalidate();
        }
    }

    /// <summary>Title color of disabled tabs; the style's disabled text color unless set.</summary>
    public Color DisabledColor
    {
        get => _disabledColor ?? Style.Current.DisabledText;
        set
        {
            _disabledColor = value;
            Invalidate();
        }
    }

    /// <summary>Face color of the tabs and the page area; the style's face color unless set.</summary>
    public Color Background
    {
        get => _background ?? Style.Current.Face;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Height of the tab strip.</summary>
    public int HeaderHeight => Font.LineHeight + 8;

    /// <summary>Area of the selected page: below the tabs, inside the 3D border.</summary>
    public Rect PageBounds => new(2, HeaderHeight + 2, Math.Max(0, Width - 4), Math.Max(0, Height - HeaderHeight - 4));

    protected override IReadOnlyList<Control> VisualChildren => _children;

    protected virtual void OnSelectedPageChanged(EventArgs e) => SelectedPageChanged?.Invoke(this, e);

    protected override void OnSizeChanged(EventArgs e)
    {
        LayoutPages();
        base.OnSizeChanged(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButton.Left || e.Y >= HeaderHeight)
            return;
        foreach (var (page, rect) in GetTabs())
        {
            if (rect.Contains(e.X, e.Y))
            {
                if (page.Enabled)
                    SelectedPage = page;
                return;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !e.Modifiers.HasFlag(KeyModifiers.Control))
            return;
        var shift = e.Modifiers.HasFlag(KeyModifiers.Shift);
        var step = e.Key switch
        {
            Key.Tab => shift ? -1 : 1,
            Key.PageDown when !shift => 1,
            Key.PageUp when !shift => -1,
            _ => 0,
        };
        if (step == 0)
            return;
        e.Handled = true;
        if (_selectedPage == null)
            return;

        var index = Pages.IndexOf(_selectedPage);
        for (var i = 1; i < Pages.Count; i++)
        {
            var page = Pages[((index + step * i) % Pages.Count + Pages.Count) % Pages.Count];
            if (page.Enabled)
            {
                SelectedPage = page;
                return;
            }
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        var header = HeaderHeight;
        var body = new Rect(0, header, Width, Height - header);
        var tabs = GetTabs().ToList();
        var selectedRect = tabs.FirstOrDefault(tab => tab.Page == _selectedPage).Rect;
        Style.Current.DrawTabPanel(dc, body, selectedRect, Background);

        // The selected tab is drawn last so that it overlaps its neighbours and the border below it.
        (TabPage Page, Rect Rect)? selected = null;
        foreach (var tab in tabs)
        {
            if (tab.Page == _selectedPage)
                selected = tab;
            else
                DrawTab(dc, tab.Page, tab.Rect, isSelected: false);
        }
        if (selected is { } s)
            DrawTab(dc, s.Page, s.Rect, isSelected: true);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        SetHotPage(e.Y < HeaderHeight ? GetTabs().FirstOrDefault(tab => tab.Rect.Contains(e.X, e.Y)).Page : null);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHotPage(null);
    }

    private void SetHotPage(TabPage? page)
    {
        if (_hotPage == page)
            return;
        _hotPage = page;
        Invalidate();
    }

    /// <summary>Tabs from left to right; the selected one includes its extra size.</summary>
    private IEnumerable<(TabPage Page, Rect Rect)> GetTabs()
    {
        var font = Font;
        var header = HeaderHeight;
        var x = SelectedGrowth;
        foreach (var page in Pages)
        {
            var width = font.MeasureText(page.Title).Width + 2 * TabPadding;
            var rect = page == _selectedPage
                ? new Rect(x - SelectedGrowth, 0, width + 2 * SelectedGrowth, header + 1) // Covers the border's top line.
                : new Rect(x, SelectedGrowth, width, header - SelectedGrowth);
            yield return (page, rect);
            x += width;
        }
    }

    private void DrawTab(DrawingContext dc, TabPage page, Rect r, bool isSelected)
    {
        var style = Style.Current;
        var enabled = page.Enabled && IsEffectivelyEnabled;
        var state = enabled ? PartState.Normal : PartState.Disabled;
        if (isSelected)
            state |= PartState.Selected;
        if (page == _hotPage)
            state |= PartState.Hot;
        style.DrawTab(dc, r, Background, state);

        var font = Font;
        var textWidth = font.MeasureText(page.Title).Width;
        var textX = r.X + (r.Width - textWidth) / 2;
        var textY = r.Y + (HeaderHeight - SelectedGrowth - (font.Ascent + font.Descent)) / 2 + (isSelected ? 0 : 1);
        style.DrawText(dc, page.Title, font, enabled ? Color : DisabledColor, textX, textY, state);
    }

    private void LayoutPages()
    {
        var bounds = PageBounds;
        foreach (var page in Pages)
            page.Bounds = bounds;
    }

    private void SetSelectedPage(TabPage? page)
    {
        var previous = _selectedPage;
        _selectedPage = page;
        if (previous != null)
            previous.Visible = false;
        if (page != null)
            page.Visible = true;
        Invalidate();
        OnSelectedPageChanged(EventArgs.Empty);
    }

    /// <summary>First enabled page from <paramref name="index"/> onwards, else the nearest one before it.</summary>
    private TabPage? FindEnabledPage(int index)
    {
        for (var i = Math.Max(0, index); i < Pages.Count; i++)
        {
            if (Pages[i].Enabled)
                return Pages[i];
        }
        for (var i = Math.Min(index, Pages.Count) - 1; i >= 0; i--)
        {
            if (Pages[i].Enabled)
                return Pages[i];
        }
        return null;
    }

    private void OnPageEnabledChanged(object? sender, EventArgs e)
    {
        var page = (TabPage)sender!;
        if (!page.Enabled && page == _selectedPage)
            SetSelectedPage(FindEnabledPage(Pages.IndexOf(page) + 1) ?? FindEnabledPage(0));
        else if (page.Enabled && _selectedPage == null)
            SetSelectedPage(page);
        Invalidate();
    }

    internal void AttachPage(int index, TabPage page)
    {
        _children.Insert(index, page); // Validates that the page has no other parent.
        page.Visible = false;
        page.Bounds = PageBounds;
        page.EnabledChanged += OnPageEnabledChanged;
    }

    internal void OnPageAdded(TabPage page)
    {
        if (_selectedPage == null && page.Enabled)
            SetSelectedPage(page);
        Invalidate();
    }

    internal void DetachPage(int index, TabPage page)
    {
        page.EnabledChanged -= OnPageEnabledChanged;
        _children.Remove(page);
        page.Visible = true; // Leave it usable elsewhere.
        if (page == _selectedPage)
            SetSelectedPage(FindEnabledPage(index));
        Invalidate();
    }
}

/// <summary>Pages of a <see cref="TabControl"/>, in tab order.</summary>
public sealed class TabPageCollection : Collection<TabPage>
{
    private readonly TabControl _owner;

    internal TabPageCollection(TabControl owner) => _owner = owner;

    protected override void InsertItem(int index, TabPage item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _owner.AttachPage(index, item);
        base.InsertItem(index, item);
        _owner.OnPageAdded(item);
    }

    protected override void RemoveItem(int index)
    {
        var page = this[index];
        base.RemoveItem(index);
        _owner.DetachPage(index, page);
    }

    protected override void SetItem(int index, TabPage item)
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
}
