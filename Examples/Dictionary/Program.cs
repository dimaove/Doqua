using Doqua.Controls;
using Doqua.GUI;

namespace DictionaryExample;

/// <summary>Color flag of a cell, stored with the data.</summary>
enum Mark
{
    None,
    Red,
    Green,
    Blue,
}

/// <summary>One key => value pair of a node, with the flags of its two cells.</summary>
class Entry(string key, string value, Mark valueMark = Mark.None)
{
    public string Key { get; } = key;
    public string Value { get; } = value;
    public Mark[] Marks { get; } = [Mark.None, valueMark]; // Indexed by table column: 0 = key, 1 = value.
}

class MainWindow : Window
{
    private readonly TreeView _tree;
    private readonly Table _table;
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Dictionary";
        Icons = ExampleIcon.Load();
        Width = 820;
        Height = 480;
        Background = new Color(212, 208, 200);

        _status = new Label { Anchor = new Anchor(Left: 10, Bottom: 8), Color = Color.Gray };
        _tree = new TreeView { Anchor = Anchor.Fill() };
        foreach (var node in CreateTestData())
            _tree.Nodes.Add(node);
        _tree.Nodes[0].ExpandAll();
        _tree.SelectionChanged += (sender, e) => ShowNode(_tree.SelectedNode);

        _table = new Table
        {
            Anchor = Anchor.Fill(),
            Columns = { new TableColumn("Key", 200), new TableColumn("Value", 330) },
        };
        _table.CellRightClick += (sender, e) => _status.Text = e.Row is { } row && e.Column is { } column
            ? $"Right click: row {row + 1}, column \"{_table.Columns[column].Header}\" ({_table[row, column].Text})"
            : "Right click outside the cells";
        _table.ContextMenuOpening += (sender, e) => e.Menu = CreateCellMenu(e.Row, e.Column);

        // Left: fixed width, full height. Right: takes the rest of the width.
        var left = new Panel { Anchor = new Anchor(Left: 8, Top: 8, Bottom: 78), Width = 230, Children = { _tree } };
        var right = new Panel { Anchor = new Anchor(Left: 246, Top: 8, Right: 8, Bottom: 78), Children = { _table } };

        var key = new Input { Anchor = new Anchor(Left: 48, Bottom: 38), Width = 190, Height = 28 };
        var value = new Input { Anchor = new Anchor(Left: 300, Bottom: 38, Right: 110), Height = 28 };
        var add = new Button { Anchor = new Anchor(Right: 8, Bottom: 38), Width = 94, Height = 28, Text = "Add" };
        void AddPair()
        {
            if (_tree.SelectedNode?.Tag is not List<Entry> entries)
            {
                _status.Text = "Select a node first";
                return;
            }
            if (key.Text.Trim().Length == 0)
            {
                _status.Text = "Type a key first";
                key.Focus();
                return;
            }
            var entry = new Entry(key.Text.Trim(), value.Text);
            entries.Add(entry); // Into the node's dictionary, then into the table.
            AddRow(entry);
            _table.ScrollToRow(_table.Rows.Count - 1);
            _status.Text = $"Added to {PathOf(_tree.SelectedNode)}: {entry.Key} => {entry.Value}";
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
                left, right,
                new Label { Anchor = new Anchor(Left: 10, Bottom: 44), Text = "Key:" },
                key,
                new Label { Anchor = new Anchor(Left: 252, Bottom: 44), Text = "Value:" },
                value,
                add,
                _status,
            },
        };
        _tree.SelectedNode = _tree.Nodes[0];
        _tree.Focus();
    }

    /// <summary>Fills the table from the node's dictionary, with the stored cell colors.</summary>
    private void ShowNode(TreeNode? node)
    {
        _table.Rows.Clear();
        if (node?.Tag is not List<Entry> entries)
            return;
        foreach (var entry in entries)
            AddRow(entry);
        _status.Text = $"{PathOf(node)}: {entries.Count} entries. Right-click a cell to mark it.";
    }

    private void AddRow(Entry entry)
    {
        var row = _table.AddRow(entry.Key, entry.Value);
        for (var column = 0; column < entry.Marks.Length; column++)
            ApplyMark(row[column], entry.Marks[column]);
    }

    private void ApplyMark(TableCell cell, Mark mark)
    {
        if (mark == Mark.None)
        {
            cell.ResetStyle();
            return;
        }
        cell.Color = mark switch { Mark.Red => Color.Red, Mark.Green => Color.Green, _ => Color.Blue };
        cell.Font = _table.Font with { Style = FontStyle.Bold };
    }

    /// <summary>
    /// Unmarked cells can be marked red, green or blue; marked ones can be unmarked. The mark is stored in the
    /// selected node's entry, so it comes back when the node is selected again.
    /// </summary>
    private PopupMenu? CreateCellMenu(int? row, int? column)
    {
        if (row is not { } r || column is not { } c || _tree.SelectedNode?.Tag is not List<Entry> entries || r >= entries.Count)
            return null;
        var entry = entries[r];
        var cell = _table[r, c];
        void SetMark(Mark mark)
        {
            entry.Marks[c] = mark;
            ApplyMark(cell, mark);
        }

        var menu = new PopupMenu();
        if (entry.Marks[c] == Mark.None)
        {
            foreach (var mark in new[] { Mark.Red, Mark.Green, Mark.Blue })
            {
                var item = new MenuItem($"Mark it {mark.ToString().ToLowerInvariant()}");
                item.Click += (sender, e) => SetMark(mark);
                menu.Items.Add(item);
            }
        }
        else
        {
            var item = new MenuItem("Unmark it");
            item.Click += (sender, e) => SetMark(Mark.None);
            menu.Items.Add(item);
        }
        return menu;
    }

    private static string PathOf(TreeNode? node)
    {
        var parts = new List<string>();
        for (; node != null; node = node.Parent)
            parts.Insert(0, node.Text);
        return string.Join(" / ", parts);
    }

    /// <summary>A small configuration tree; every node keeps its own key => value pairs in Tag.</summary>
    private static TreeNode[] CreateTestData()
    {
        static TreeNode Node(string text, List<Entry> entries, params TreeNode[] children) =>
            new(text, children) { Tag = entries };

        return
        [
            Node("Application", [new("name", "Doqua Dictionary"), new("version", "1.0.3"), new("language", "en-US")],
                Node("User interface", [new("theme", "Classic"), new("font", "sans-serif 14 px"), new("scale", "100 %")]),
                Node("Logging", [new("level", "Information"), new("file", "/var/log/doqua.log"), new("rotation", "daily")])),
            Node("Server", [new("host", "example.com"), new("port", "8080"), new("timeout", "30 s")],
                Node("TLS", [new("enabled", "true"), new("certificate", "Error: certificate expired", Mark.Red)])),
            Node("Database", [new("url", "postgres://db.example.com/main"), new("status", "Error: connection refused", Mark.Red), new("pool size", "20")],
                Node("Replicas", [new("count", "2")],
                    Node("Replica 1", [new("host", "db1.example.com"), new("lag", "12 ms")]),
                    Node("Replica 2", [new("host", "db2.example.com"), new("status", "Error: unreachable", Mark.Red)]))),
            Node("Cache", [new("size", "256 MB"), new("policy", "LRU"), new("hit rate", "93 %")]),
        ];
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
