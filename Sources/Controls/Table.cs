using System.Collections.ObjectModel;
using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// Grid of text cells with a header row, in the classic style. <see cref="Columns"/> define the headers and
/// widths; <see cref="Rows"/> hold the cells (see also <see cref="SetData"/> and <see cref="AddRow"/>).
/// Every cell uses the table's <see cref="Font"/> and <see cref="Color"/> unless its own are set.
/// Drag a header border to resize a column, double-click it to fit the column to its contents.
/// Scroll bars appear when the cells do not fit; the mouse wheel scrolls (Shift+wheel horizontally).
/// </summary>
public class Table : Control
{
    private const int EdgeSize = 2;
    private const int CellPadding = 5;
    private const int ResizeGrip = 3;   // Pixels on each side of a header border that start a resize.
    private const int WheelRows = 3;

    private readonly ClassicScrollBar _verticalBar;
    private readonly ClassicScrollBar _horizontalBar;
    private int _scrollX;
    private int _scrollY;
    private int _resizingColumn = -1;
    private int _resizeStartX;
    private int _resizeStartWidth;
    private (int X, int Y)? _lastRightPress;
    private Font? _font;
    private Color _color = Color.Black;
    private Color _background = Color.White;
    private Color _gridColor = new(192, 192, 192);
    private Color _headerColor = Color.Black;
    private Color _disabledColor = ClassicStyle.Shadow;

    /// <summary>Creates an empty table, 300 x 200 pixels.</summary>
    public Table()
    {
        Columns = new TableColumnCollection(this);
        Rows = new TableRowCollection(this);
        _verticalBar = new ClassicScrollBar(this, () => _scrollY, value => ScrollTo(_scrollX, value));
        _horizontalBar = new ClassicScrollBar(this, () => _scrollX, value => ScrollTo(value, _scrollY)) { IsHorizontal = true };
        Width = 300;
        Height = 200;
    }

    /// <summary>The columns, left to right.</summary>
    public TableColumnCollection Columns { get; }

    /// <summary>The rows, top to bottom.</summary>
    public TableRowCollection Rows { get; }

    /// <summary>The cell at (<paramref name="row"/>, <paramref name="column"/>); missing cells of a short row are created empty.</summary>
    public TableCell this[int row, int column] => Rows[row][column];

    /// <summary>Raised on a left click; the indexes are null outside the cells (header, empty area).</summary>
    public event EventHandler<TableCellEventArgs>? CellClick;

    /// <summary>Raised on a right click; the indexes are null outside the cells. The context menu opens after it.</summary>
    public event EventHandler<TableCellEventArgs>? CellRightClick;

    /// <summary>
    /// Raised before the context menu opens (right click, or Shift+F10 with no cell). Set <see cref="TableContextMenuEventArgs.Menu"/>
    /// to the menu to show (it starts as <see cref="Control.ContextMenu"/>), or to null for none.
    /// </summary>
    public event EventHandler<TableContextMenuEventArgs>? ContextMenuOpening;

    /// <summary>Default font of the cells and the font of the headers.</summary>
    public Font Font
    {
        get => _font ??= Font.Default;
        set
        {
            _font = value ?? throw new ArgumentNullException(nameof(value));
            Invalidate();
        }
    }

    /// <summary>Default text color of the cells.</summary>
    public Color Color
    {
        get => _color;
        set => SetColor(ref _color, value);
    }

    /// <summary>Background of the cells.</summary>
    public Color Background
    {
        get => _background;
        set => SetColor(ref _background, value);
    }

    /// <summary>Color of the grid lines.</summary>
    public Color GridColor
    {
        get => _gridColor;
        set => SetColor(ref _gridColor, value);
    }

    /// <summary>Text color of the headers.</summary>
    public Color HeaderColor
    {
        get => _headerColor;
        set => SetColor(ref _headerColor, value);
    }

    /// <summary>Text color while disabled.</summary>
    public Color DisabledColor
    {
        get => _disabledColor;
        set => SetColor(ref _disabledColor, value);
    }

    /// <summary>Replaces all rows with the texts of <paramref name="data"/> ([row, column]).</summary>
    public void SetData(string[,] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Rows.Clear();
        for (var row = 0; row < data.GetLength(0); row++)
        {
            var texts = new string[data.GetLength(1)];
            for (var column = 0; column < texts.Length; column++)
                texts[column] = data[row, column] ?? "";
            Rows.Add(new TableRow(texts));
        }
    }

    /// <summary>Adds a row with these cell texts and returns it.</summary>
    public TableRow AddRow(params string[] texts)
    {
        var row = new TableRow(texts);
        Rows.Add(row);
        return row;
    }

    /// <summary>Cell under the point (in the table's coordinates), or null outside the cells.</summary>
    public (int Row, int Column)? CellAt(int x, int y)
    {
        var layout = GetLayout();
        if (!layout.Body.Contains(x, y))
            return null;
        var column = ColumnAt(x - layout.Body.X + _scrollX);
        if (column < 0)
            return null;
        var top = y - layout.Body.Y + _scrollY;
        var rowTop = 0;
        for (var row = 0; row < Rows.Count; row++)
        {
            var height = RowHeight(Rows[row]);
            if (top < rowTop + height)
                return (row, column);
            rowTop += height;
        }
        return null;
    }

    /// <summary>Scrolls vertically so that <paramref name="row"/> is visible.</summary>
    public void ScrollToRow(int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(row, Rows.Count);
        var layout = GetLayout();
        var top = 0;
        for (var i = 0; i < row; i++)
            top += RowHeight(Rows[i]);
        var height = RowHeight(Rows[row]);
        if (top < _scrollY)
            ScrollTo(_scrollX, top);
        else if (top + height > _scrollY + layout.Body.Height)
            ScrollTo(_scrollX, top + height - layout.Body.Height);
    }

    /// <summary>Makes <paramref name="column"/> as wide as its header and its widest cell.</summary>
    public void FitColumn(int column)
    {
        var width = HeaderFont.MeasureText(Columns[column].Header).Width;
        foreach (var row in Rows)
        {
            if (column < row.CellCount)
            {
                var cell = row[column];
                width = Math.Max(width, (cell.Font ?? Font).MeasureText(cell.Text).Width);
            }
        }
        Columns[column].Width = width + 2 * CellPadding + 2;
    }

    protected virtual void OnCellClick(TableCellEventArgs e) => CellClick?.Invoke(this, e);

    protected virtual void OnCellRightClick(TableCellEventArgs e) => CellRightClick?.Invoke(this, e);

    protected virtual void OnContextMenuOpening(TableContextMenuEventArgs e) => ContextMenuOpening?.Invoke(this, e);

    /// <summary>The context menu for the cell under the last right-button press, decided by <see cref="ContextMenuOpening"/>.</summary>
    protected override PopupMenu? GetContextMenu()
    {
        var cell = _lastRightPress is var (x, y) ? CellAt(x, y) : null;
        _lastRightPress = null;
        var e = new TableContextMenuEventArgs(cell?.Row, cell?.Column, ContextMenu);
        OnContextMenuOpening(e);
        return e.Menu;
    }

    /// <summary>The resize cursor over a header border, and while a column is being resized.</summary>
    protected override Cursor GetCursor(int x, int y) =>
        _resizingColumn >= 0 || BorderAt(x, y) >= 0 ? Cursor.SizeWE : base.GetCursor(x, y);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButton.Right)
        {
            _lastRightPress = (e.X, e.Y);
            return;
        }
        if (e.Button != MouseButton.Left)
            return;

        if (BorderAt(e.X, e.Y) is var border and >= 0)
        {
            if (e.ClickCount == 2)
            {
                FitColumn(border);
                return;
            }
            (_resizingColumn, _resizeStartX, _resizeStartWidth) = (border, e.X, Columns[border].Width);
            return;
        }

        var layout = GetLayout();
        SyncBars(layout);
        if (layout.Vertical && _verticalBar.Bounds.Contains(e.X, e.Y))
            _verticalBar.Press(e.X, e.Y);
        else if (layout.Horizontal && _horizontalBar.Bounds.Contains(e.X, e.Y))
            _horizontalBar.Press(e.X, e.Y);
    }

    protected override void OnMouseMove(MouseMoveEventArgs e)
    {
        base.OnMouseMove(e);
        if (_resizingColumn >= 0)
        {
            var column = Columns[_resizingColumn];
            column.Width = Math.Max(column.MinWidth, _resizeStartWidth + e.X - _resizeStartX);
            return;
        }
        _verticalBar.Drag(e.X, e.Y);
        _horizontalBar.Drag(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _resizingColumn = -1;
        _verticalBar.Release();
        _horizontalBar.Release();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button is not (MouseButton.Left or MouseButton.Right) || BorderAt(e.X, e.Y) >= 0)
            return;
        var cell = CellAt(e.X, e.Y);
        var args = new TableCellEventArgs(cell?.Row, cell?.Column, e.Button);
        if (e.Button == MouseButton.Left)
            OnCellClick(args);
        else
            OnCellRightClick(args);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (e.Handled)
            return;
        var layout = GetLayout();
        var horizontal = layout.Horizontal && (e.Modifiers.HasFlag(KeyModifiers.Shift) || !layout.Vertical);
        if (horizontal)
            ScrollTo(_scrollX - e.Delta * 3 * Font.LineHeight, _scrollY);
        else if (layout.Vertical)
            ScrollTo(_scrollX, _scrollY - e.Delta * WheelRows * DefaultRowHeight);
        else
            return;
        e.Handled = true;
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

        // Header: a raised button per column, scrolled sideways with the body; the rest of the row (also above
        // the vertical scroll bar) is empty face.
        var header = new Rect(body.X, EdgeSize, Math.Max(0, Width - 2 * EdgeSize), layout.HeaderHeight);
        using (dc.PushClip(header))
        {
            dc.FillRectangle(header, ClassicStyle.Face);
            var x = body.X - _scrollX;
            foreach (var column in Columns)
            {
                var rect = new Rect(x, header.Y, column.Width, header.Height);
                ClassicStyle.DrawRaisedEdge(dc, rect, ClassicStyle.Highlight, ClassicStyle.Shadow, ClassicStyle.DarkShadow);
                DrawCellText(dc, column.Header, HeaderFont, enabled ? _headerColor : _disabledColor, rect);
                x += column.Width;
            }
        }

        // Body: only the visible rows are drawn.
        using (dc.PushClip(body))
        {
            var y = body.Y - _scrollY;
            foreach (var row in Rows)
            {
                var height = RowHeight(row);
                if (y + height > body.Y)
                {
                    if (y >= body.Bottom)
                        break;
                    var x = body.X - _scrollX;
                    for (var column = 0; column < Columns.Count; column++)
                    {
                        var width = Columns[column].Width;
                        if (column < row.CellCount)
                        {
                            var cell = row[column];
                            var color = enabled ? cell.Color ?? _color : _disabledColor;
                            DrawCellText(dc, cell.Text, cell.Font ?? Font, color, new Rect(x, y, width, height));
                        }
                        dc.FillRectangle(x + width - 1, y, 1, height, _gridColor);
                        x += width;
                    }
                    dc.FillRectangle(body.X - _scrollX, y + height - 1, layout.ContentWidth, 1, _gridColor);
                }
                y += height;
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

    internal void OnContentChanged() => Invalidate();

    private Font HeaderFont => Font;

    private int DefaultRowHeight => Font.LineHeight + 4;

    private int RowHeight(TableRow row)
    {
        var height = Font.LineHeight;
        for (var i = 0; i < row.CellCount; i++)
        {
            if (row[i].Font is { } font)
                height = Math.Max(height, font.LineHeight);
        }
        return height + 4;
    }

    /// <summary>Header layout, visible body area, content size and which scroll bars are shown.</summary>
    private TableLayout GetLayout()
    {
        var headerHeight = HeaderFont.LineHeight + 6;
        var contentWidth = Columns.Sum(column => column.Width);
        var contentHeight = Rows.Sum(RowHeight);
        int innerWidth = Math.Max(0, Width - 2 * EdgeSize), innerHeight = Math.Max(0, Height - 2 * EdgeSize - headerHeight);
        bool vertical = false, horizontal = false;
        int viewWidth = innerWidth, viewHeight = innerHeight;
        for (var pass = 0; pass < 2; pass++) // Showing one bar can make the other one necessary.
        {
            vertical = contentHeight > viewHeight;
            horizontal = contentWidth > viewWidth;
            viewWidth = Math.Max(0, innerWidth - (vertical ? ClassicScrollBar.Thickness : 0));
            viewHeight = Math.Max(0, innerHeight - (horizontal ? ClassicScrollBar.Thickness : 0));
        }
        var body = new Rect(EdgeSize, EdgeSize + headerHeight, viewWidth, viewHeight);
        return new TableLayout(body, headerHeight, contentWidth, contentHeight, vertical, horizontal);
    }

    /// <summary>Column under a content x coordinate, or -1 past the last column.</summary>
    private int ColumnAt(int contentX)
    {
        var right = 0;
        for (var column = 0; column < Columns.Count; column++)
        {
            right += Columns[column].Width;
            if (contentX < right)
                return contentX < 0 ? -1 : column;
        }
        return -1;
    }

    /// <summary>Column whose right header border is near the point, or -1.</summary>
    private int BorderAt(int x, int y)
    {
        var layout = GetLayout();
        if (y < EdgeSize || y >= EdgeSize + layout.HeaderHeight || x < layout.Body.X || x >= layout.Body.Right + ResizeGrip)
            return -1;
        var right = layout.Body.X - _scrollX;
        for (var column = 0; column < Columns.Count; column++)
        {
            right += Columns[column].Width;
            if (Math.Abs(x - right) <= ResizeGrip)
                return column;
        }
        return -1;
    }

    private void DrawCellText(DrawingContext dc, string text, Font font, Color color, Rect cell)
    {
        if (text.Length == 0)
            return;
        using (dc.PushClip(Rect.FromEdges(cell.X + 1, cell.Y, cell.Right - 2, cell.Bottom)))
            dc.DrawText(text, font, color, cell.X + CellPadding, cell.Y + (cell.Height - (font.Ascent + font.Descent)) / 2);
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

    private void ClampScroll(TableLayout layout)
    {
        _scrollX = Math.Clamp(_scrollX, 0, Math.Max(0, layout.ContentWidth - layout.Body.Width));
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, layout.ContentHeight - layout.Body.Height));
    }

    private void SyncBars(TableLayout layout)
    {
        var body = layout.Body;
        // The vertical bar runs from under the header to the bottom; the horizontal one along the bottom.
        _verticalBar.Bounds = new Rect(body.Right, body.Y, ClassicScrollBar.Thickness, body.Height);
        _verticalBar.Maximum = Math.Max(0, layout.ContentHeight - body.Height);
        _verticalBar.ViewSize = body.Height;
        _verticalBar.ContentSize = layout.ContentHeight;
        _verticalBar.SmallChange = DefaultRowHeight;
        _verticalBar.LargeChange = Math.Max(DefaultRowHeight, body.Height - DefaultRowHeight);

        _horizontalBar.Bounds = new Rect(body.X, body.Bottom, body.Width, ClassicScrollBar.Thickness);
        _horizontalBar.Maximum = Math.Max(0, layout.ContentWidth - body.Width);
        _horizontalBar.ViewSize = body.Width;
        _horizontalBar.ContentSize = layout.ContentWidth;
        _horizontalBar.SmallChange = 16;
        _horizontalBar.LargeChange = Math.Max(16, body.Width - 16);
    }

    private void SetColor(ref Color field, Color value)
    {
        field = value;
        Invalidate();
    }

    private readonly record struct TableLayout(Rect Body, int HeaderHeight, int ContentWidth, int ContentHeight, bool Vertical, bool Horizontal);
}

/// <summary>A column of a <see cref="Table"/>: its header text and width.</summary>
public class TableColumn
{
    private string _header;
    private int _width;
    private int _minWidth = 20;

    /// <summary>Creates a column with a header text and a width in pixels.</summary>
    public TableColumn(string header, int width = 100)
    {
        _header = header ?? throw new ArgumentNullException(nameof(header));
        _width = Math.Max(_minWidth, width);
    }

    /// <summary>Header text.</summary>
    public string Header
    {
        get => _header;
        set
        {
            _header = value ?? throw new ArgumentNullException(nameof(value));
            Table?.OnContentChanged();
        }
    }

    /// <summary>Width in pixels; the user can change it by dragging the header border.</summary>
    public int Width
    {
        get => _width;
        set
        {
            _width = Math.Max(_minWidth, value);
            Table?.OnContentChanged();
        }
    }

    /// <summary>Smallest width the user can drag the column to (default 20).</summary>
    public int MinWidth
    {
        get => _minWidth;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _minWidth = value;
            Width = _width;
        }
    }

    internal Table? Table { get; set; }
}

/// <summary>A row of a <see cref="Table"/>: its cells, in column order.</summary>
public class TableRow
{
    private readonly List<TableCell> _cells = [];

    /// <summary>Creates a row with these cell texts.</summary>
    public TableRow(params string[] texts)
    {
        ArgumentNullException.ThrowIfNull(texts);
        foreach (var text in texts)
            _cells.Add(new TableCell(this, text ?? ""));
    }

    /// <summary>The cell in <paramref name="column"/>; cells missing at the end of the row are created empty.</summary>
    public TableCell this[int column]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(column);
            while (_cells.Count <= column)
                _cells.Add(new TableCell(this, ""));
            return _cells[column];
        }
    }

    /// <summary>Number of cells the row has (it may differ from the number of columns).</summary>
    public int CellCount => _cells.Count;

    internal Table? Table { get; set; }
}

/// <summary>
/// A cell of a <see cref="Table"/>. <see cref="Font"/> and <see cref="Color"/> are null (use the table's)
/// unless the program redefines them for this cell.
/// </summary>
public class TableCell
{
    private readonly TableRow _row;
    private string _text;
    private Font? _font;
    private Color? _color;

    internal TableCell(TableRow row, string text)
    {
        _row = row;
        _text = text;
    }

    /// <summary>Text of the cell.</summary>
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? throw new ArgumentNullException(nameof(value));
            Changed();
        }
    }

    /// <summary>This cell's font, or null to use the table's.</summary>
    public Font? Font
    {
        get => _font;
        set
        {
            _font = value;
            Changed();
        }
    }

    /// <summary>This cell's text color, or null to use the table's.</summary>
    public Color? Color
    {
        get => _color;
        set
        {
            _color = value;
            Changed();
        }
    }

    /// <summary>True if the cell has its own font or color.</summary>
    public bool IsStyled => _font != null || _color != null;

    /// <summary>Goes back to the table's font and color.</summary>
    public void ResetStyle()
    {
        (_font, _color) = (null, null);
        Changed();
    }

    private void Changed() => _row.Table?.OnContentChanged();
}

/// <summary>Columns of a <see cref="Table"/>.</summary>
public sealed class TableColumnCollection : Collection<TableColumn>
{
    private readonly Table _owner;

    internal TableColumnCollection(Table owner) => _owner = owner;

    protected override void InsertItem(int index, TableColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Table = _owner;
        base.InsertItem(index, item);
        _owner.OnContentChanged();
    }

    protected override void SetItem(int index, TableColumn item)
    {
        ArgumentNullException.ThrowIfNull(item);
        this[index].Table = null;
        item.Table = _owner;
        base.SetItem(index, item);
        _owner.OnContentChanged();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Table = null;
        base.RemoveItem(index);
        _owner.OnContentChanged();
    }

    protected override void ClearItems()
    {
        foreach (var column in this)
            column.Table = null;
        base.ClearItems();
        _owner.OnContentChanged();
    }
}

/// <summary>Rows of a <see cref="Table"/>.</summary>
public sealed class TableRowCollection : Collection<TableRow>
{
    private readonly Table _owner;

    internal TableRowCollection(Table owner) => _owner = owner;

    protected override void InsertItem(int index, TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        item.Table = _owner;
        base.InsertItem(index, item);
        _owner.OnContentChanged();
    }

    protected override void SetItem(int index, TableRow item)
    {
        ArgumentNullException.ThrowIfNull(item);
        this[index].Table = null;
        item.Table = _owner;
        base.SetItem(index, item);
        _owner.OnContentChanged();
    }

    protected override void RemoveItem(int index)
    {
        this[index].Table = null;
        base.RemoveItem(index);
        _owner.OnContentChanged();
    }

    protected override void ClearItems()
    {
        foreach (var row in this)
            row.Table = null;
        base.ClearItems();
        _owner.OnContentChanged();
    }
}

/// <summary>A click on a <see cref="Table"/>: the cell's indexes, or null outside the cells.</summary>
public class TableCellEventArgs(int? row, int? column, MouseButton button) : EventArgs
{
    /// <summary>Row index of the clicked cell, or null outside the cells.</summary>
    public int? Row { get; } = row;

    /// <summary>Column index of the clicked cell, or null outside the cells.</summary>
    public int? Column { get; } = column;

    /// <summary>The mouse button that was clicked.</summary>
    public MouseButton Button { get; } = button;
}

/// <summary>The context menu of a <see cref="Table"/> is about to open for a cell (or outside the cells: null indexes).</summary>
public class TableContextMenuEventArgs(int? row, int? column, PopupMenu? menu) : EventArgs
{
    /// <summary>Row index of the right-clicked cell, or null outside the cells.</summary>
    public int? Row { get; } = row;

    /// <summary>Column index of the right-clicked cell, or null outside the cells.</summary>
    public int? Column { get; } = column;

    /// <summary>The menu to show; starts as the table's ContextMenu. Set it to null to show none.</summary>
    public PopupMenu? Menu { get; set; } = menu;
}
