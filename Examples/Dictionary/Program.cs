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
    public string Key { get; set; } = key;
    public string Value { get; set; } = value;
    public Mark[] Marks { get; } = [Mark.None, valueMark]; // Indexed by table column: 0 = key, 1 = value.
}

class MainWindow : Window
{
    private readonly TreeView _tree;
    private readonly Table _table;
    private readonly Label _status;
    private readonly Input _key;
    private readonly Input _value;
    private readonly Button _add;
    private readonly Button _remove;
    private int _editing = -1; // Row being edited in the inputs (Save instead of Add), or -1.

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

        // Rows can be selected (click, arrows); Enter or a double click edits the selected pair.
        _table = new Table
        {
            Anchor = Anchor.Fill(),
            RowSelection = true,
            Columns = { new TableColumn("Key", 200), new TableColumn("Value", 330) },
        };
        _table.RowActivated += (sender, e) => StartEditing(e.Row);
        _table.CellRightClick += (sender, e) => _status.Text = e.Row is { } row && e.Column is { } column
            ? $"Right click: row {row + 1}, column \"{_table.Columns[column].Header}\" ({_table[row, column].Text})"
            : "Right click outside the cells";
        _table.ContextMenuOpening += (sender, e) => e.Menu = CreateCellMenu(e.Row, e.Column);

        // Left: the tree keeps its width when the window is resized; the user drags the bar between the panels
        // to change it. Right: the table takes the rest of the width.
        var split = new SplitContainer
        {
            Anchor = new Anchor(Left: 8, Top: 8, Right: 8, Bottom: 78),
            Panel1MinSize = 120,
            Panel2MinSize = 200,
        };
        split.Panel1.Children.Add(_tree);
        split.Panel2.Children.Add(_table);
        split.SplitterDistance = 230;
        split.SplitterMoved += (sender, e) => _status.Text = $"Tree width: {split.SplitterDistance} px";

        _key = new Input { Anchor = new Anchor(Left: 48, Bottom: 38), Width = 190, Height = 28, MaxLength = 32 };
        _value = new Input { Anchor = new Anchor(Left: 300, Bottom: 38, Right: 210), Height = 28 };
        _add = new Button { Anchor = new Anchor(Right: 108, Bottom: 38), Width = 94, Height = 28, Text = "Add" };
        _remove = new Button { Anchor = new Anchor(Right: 8, Bottom: 38), Width = 94, Height = 28, Text = "Remove", Enabled = false };
        _table.SelectionChanged += (sender, e) => _remove.Enabled = _table.SelectedIndex >= 0;
        _add.Click += (sender, e) => SavePair();
        _remove.Click += (sender, e) => RemovePair();
        _value.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Enter)
            {
                SavePair();
                e.Handled = true;
            }
        };
        _table.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Delete && _table.SelectedIndex >= 0)
            {
                RemovePair();
                e.Handled = true;
            }
        };

        Content = new Panel
        {
            Children =
            {
                split,
                new Label { Anchor = new Anchor(Left: 10, Bottom: 44), Text = "Key:" },
                _key,
                new Label { Anchor = new Anchor(Left: 252, Bottom: 44), Text = "Value:" },
                _value,
                _add,
                _remove,
                _status,
            },
        };
        _tree.SelectedNode = _tree.Nodes[0];
        _tree.Focus();
    }

    /// <summary>Adds the typed pair, or saves the pair being edited; problems are shown in a message box.</summary>
    private void SavePair()
    {
        if (_tree.SelectedNode?.Tag is not List<Entry> entries)
        {
            MessageBox.Show(this, "Select a node in the tree first.", icon: MessageBoxIcon.Information);
            return;
        }
        var key = _key.Text.Trim();
        if (key.Length == 0)
        {
            MessageBox.Show(this, "Type a key first.", icon: MessageBoxIcon.Warning, closed: result => _key.Focus());
            return;
        }
        var existing = entries.FindIndex(entry => entry.Key == key);
        if (existing >= 0 && existing != _editing)
        {
            MessageBox.Show(this, $"The key \"{key}\" already exists in {PathOf(_tree.SelectedNode)}.\nChoose another key.",
                icon: MessageBoxIcon.Error, closed: result => _key.Focus());
            return;
        }

        if (_editing >= 0)
        {
            var entry = entries[_editing];
            (entry.Key, entry.Value) = (key, _value.Text);
            (_table[_editing, 0].Text, _table[_editing, 1].Text) = (entry.Key, entry.Value);
            _table.SelectedIndex = _editing;
            _status.Text = $"Saved: {entry.Key} => {entry.Value}";
        }
        else
        {
            var entry = new Entry(key, _value.Text);
            entries.Add(entry); // Into the node's dictionary, then into the table.
            AddRow(entry);
            _table.SelectedIndex = _table.Rows.Count - 1;
            _status.Text = $"Added to {PathOf(_tree.SelectedNode)}: {entry.Key} => {entry.Value}";
        }
        StopEditing();
        _key.Focus();
    }

    /// <summary>Asks, then removes the selected pair.</summary>
    private void RemovePair()
    {
        if (_tree.SelectedNode?.Tag is not List<Entry> entries || _table.SelectedIndex is var index && index < 0)
            return;
        var entry = entries[index];
        MessageBox.Show(this, $"Remove \"{entry.Key}\" from {PathOf(_tree.SelectedNode)}?", "Remove", MessageBoxButtons.YesNo,
            MessageBoxIcon.Question, result =>
            {
                if (result != MessageBoxResult.Yes)
                {
                    _table.Focus();
                    return;
                }
                entries.RemoveAt(index);
                _table.Rows.RemoveAt(index);
                if (_table.Rows.Count > 0)
                    _table.SelectedIndex = Math.Min(index, _table.Rows.Count - 1);
                StopEditing();
                _status.Text = $"Removed: {entry.Key}";
                _table.Focus();
            });
    }

    /// <summary>Loads a pair into the inputs; Add becomes Save until it is saved or another node is shown.</summary>
    private void StartEditing(int row)
    {
        if (_tree.SelectedNode?.Tag is not List<Entry> entries || row >= entries.Count)
            return;
        _editing = row;
        (_key.Text, _value.Text) = (entries[row].Key, entries[row].Value);
        _value.CaretIndex = _value.Text.Length;
        _add.Text = "Save";
        _status.Text = $"Editing \"{entries[row].Key}\": change it and press Enter or Save.";
        _value.Focus();
    }

    private void StopEditing()
    {
        _editing = -1;
        _add.Text = "Add";
        _key.Text = _value.Text = "";
    }

    /// <summary>Fills the table from the node's dictionary, with the stored cell colors.</summary>
    private void ShowNode(TreeNode? node)
    {
        if (_editing >= 0)
            StopEditing();
        _table.Rows.Clear();
        if (node?.Tag is not List<Entry> entries)
            return;
        foreach (var entry in entries)
            AddRow(entry);
        _status.Text = $"{PathOf(node)}: {entries.Count} entries. Double-click a row to edit it, Delete removes it, right-click marks a cell.";
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
