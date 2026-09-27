using Doqua.Controls;
using Doqua.GUI;

namespace DictionaryExample;

class MainWindow : Window
{
    private readonly Table _table;
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Dictionary";
        Icons = ExampleIcon.Load();
        Width = 620;
        Height = 460;
        Background = new Color(212, 208, 200);

        _table = new Table
        {
            Anchor = new Anchor(Left: 8, Top: 8, Right: 8, Bottom: 78),
            Columns = { new TableColumn("Key", 200), new TableColumn("Value", 330) },
        };
        _table.SetData(new[,]
        {
            { "application.name", "Doqua Dictionary" },
            { "application.version", "1.0.3" },
            { "server.host", "example.com" },
            { "server.port", "8080" },
            { "database.url", "postgres://db.example.com/main" },
            { "database.status", "Error: connection refused" },
            { "cache.size", "256 MB" },
            { "log.level", "Information" },
            { "ui.language", "en-US" },
        });
        // One value stands out: the program redefines the font and color of just this cell.
        var error = _table[5, 1];
        error.Color = Color.Red;
        error.Font = _table.Font with { Style = FontStyle.Bold };

        _status = new Label { Anchor = new Anchor(Left: 10, Bottom: 8), Color = Color.Gray };
        _table.CellRightClick += (sender, e) => _status.Text = e.Row is { } row && e.Column is { } column
            ? $"Right click: row {row + 1}, column \"{_table.Columns[column].Header}\" ({_table[row, column].Text})"
            : "Right click outside the cells";
        _table.ContextMenuOpening += (sender, e) => e.Menu = CreateCellMenu(e.Row, e.Column);

        var key = new Input { Anchor = new Anchor(Left: 48, Bottom: 38), Width = 170, Height = 28 };
        var value = new Input { Anchor = new Anchor(Left: 280, Bottom: 38, Right: 110), Height = 28 };
        var add = new Button { Anchor = new Anchor(Right: 8, Bottom: 38), Width = 94, Height = 28, Text = "Add" };
        void AddPair()
        {
            if (key.Text.Trim().Length == 0)
            {
                _status.Text = "Type a key first";
                key.Focus();
                return;
            }
            _table.AddRow(key.Text.Trim(), value.Text);
            _table.ScrollToRow(_table.Rows.Count - 1);
            _status.Text = $"Added row {_table.Rows.Count}: {key.Text.Trim()} => {value.Text}";
            key.Text = value.Text = "";
            key.Focus();
        }
        add.Click += (sender, e) => AddPair();
        value.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Enter)
            {
                AddPair();
                e.Handled = true;
            }
        };

        Content = new Panel
        {
            Children =
            {
                _table,
                new Label { Anchor = new Anchor(Left: 10, Bottom: 44), Text = "Key:" },
                key,
                new Label { Anchor = new Anchor(Left: 232, Bottom: 44), Text = "Value:" },
                value,
                add,
                _status,
            },
        };
        _status.Text = "Right-click a cell to mark it; drag a header border to resize a column.";
        key.Focus();
    }

    /// <summary>
    /// The menu depends on the cell: a cell with the table's own font and color can be marked in a color;
    /// a marked cell (or one the program styled, like the error) can be unmarked. No menu outside the cells.
    /// </summary>
    private PopupMenu? CreateCellMenu(int? row, int? column)
    {
        if (row is not { } r || column is not { } c)
            return null;
        var cell = _table[r, c];
        var menu = new PopupMenu();
        if (!cell.IsStyled)
        {
            foreach (var (name, color) in new[] { ("red", Color.Red), ("green", Color.Green), ("blue", Color.Blue) })
            {
                var item = new MenuItem($"Mark it {name}");
                item.Click += (sender, e) =>
                {
                    cell.Color = color;
                    cell.Font = _table.Font with { Style = FontStyle.Bold };
                };
                menu.Items.Add(item);
            }
        }
        else
        {
            var item = new MenuItem("Unmark it");
            item.Click += (sender, e) => cell.ResetStyle();
            menu.Items.Add(item);
        }
        return menu;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
