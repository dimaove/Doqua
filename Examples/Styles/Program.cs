using Doqua.Controls;
using Doqua.GUI;

namespace StylesExample;

/// <summary>
/// Many controls in one window, and a choice of styles: <see cref="Style.Current"/> changes the look of every control
/// at once, while the program runs. The window and the controls do not set colors of their own, so they follow the style.
/// </summary>
class MainWindow : Window
{
    // The styles offered: the two built-in ones and a flat style with another accent color.
    private static readonly (string Name, Func<Style> Create)[] s_styles =
    [
        ("Classic", () => new ClassicStyle()),
        ("Flat", () => new FlatStyle()),
        ("Flat green", () =>
        {
            var green = Color.FromRgb(0x2E8B57);
            return new FlatStyle { Accent = green, SelectionBackground = green, TextSelectionBackground = green };
        }),
    ];

    private readonly ComboBox<string> _styleCombo = new() { Anchor = new Anchor(Left: 60, Top: 34), Width = 160 };
    private readonly Label _status = new() { Anchor = new Anchor(Left: 12, Bottom: 10) };
    private readonly List<MenuItem> _styleItems = [];
    private bool _showingStyle; // Set while ShowStyle updates the chooser, so that this does not set the style again.

    public MainWindow()
    {
        Title = "Doqua Styles";
        Icons = ExampleIcon.Load();
        Width = 760;
        Height = 560;

        // Menu bar: File, Style (one item per style), Help.
        var exit = new MenuItem("Exit", "Ctrl+Q");
        exit.Click += (sender, e) => Close();
        var styleMenu = new MainMenuItem("&Style");
        for (var i = 0; i < s_styles.Length; i++)
        {
            var index = i;
            var item = new MenuItem(s_styles[i].Name, $"Ctrl+{i + 1}");
            item.Click += (sender, e) => SetStyle(index);
            styleMenu.Items.Add(item);
            _styleItems.Add(item);
        }
        var about = new MenuItem("About...", "F1");
        about.Click += (sender, e) => ShowMessage();
        var menu = new MainMenu
        {
            Items =
            {
                new MainMenuItem("&File") { Items = { exit } },
                styleMenu,
                new MainMenuItem("&Help") { Items = { about } },
            },
        };

        // The style chooser, and a button that opens a message box (a separate window in the same style).
        foreach (var (name, _) in s_styles)
            _styleCombo.Items.Add(name);
        _styleCombo.SelectionChanged += (sender, e) =>
        {
            if (!_showingStyle && _styleCombo.SelectedIndex >= 0)
                SetStyle(_styleCombo.SelectedIndex);
        };
        var message = new Button { Anchor = new Anchor(Top: 32, Right: 8), Width = 150, Height = 30, Text = "Message box..." };
        message.Click += (sender, e) => ShowMessage();

        var tabs = new TabControl
        {
            Anchor = new Anchor(Left: 8, Top: 72, Right: 8, Bottom: 36),
            Pages = { CreateControlsPage(), CreateTextPage(), CreateListsPage(), CreateDisabledPage() },
        };

        Content = new Panel
        {
            Children = { menu, new Label { Anchor = new Anchor(Left: 12, Top: 38), Text = "Style:" }, _styleCombo, message, tabs, _status },
        };

        Style.Changed += (sender, e) => ShowStyle();
        ShowStyle();
    }

    private static void SetStyle(int index) => Style.Current = s_styles[index].Create();

    /// <summary>Keeps the chooser, the menu check marks (as text) and the status line in step with the current style.</summary>
    private void ShowStyle()
    {
        var name = Style.Current switch
        {
            FlatStyle flat when flat.Accent != new FlatStyle().Accent => "Flat green",
            FlatStyle => "Flat",
            _ => "Classic",
        };
        var index = Array.FindIndex(s_styles, s => s.Name == name);
        _showingStyle = true;
        _styleCombo.SelectedIndex = index;
        _showingStyle = false;
        for (var i = 0; i < _styleItems.Count; i++)
            _styleItems[i].Text = (i == index ? "• " : "   ") + s_styles[i].Name;
        _status.Text = $"Current style: {name}. Change it with the list above, the Style menu or Ctrl+1 / Ctrl+2 / Ctrl+3.";
    }

    private void ShowMessage() =>
        MessageBox.Show(this, $"Message boxes are windows of their own; they use the current style too.\nNow: {Style.Current.Name}.",
            "Styles", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);

    /// <summary>Buttons, check boxes, radio buttons and value inputs, enabled and disabled.</summary>
    private TabPage CreateControlsPage()
    {
        var clicks = 0;
        var clickMe = new Button { X = 12, Y = 28, Width = 196, Height = 30, Text = "Click me" };
        clickMe.Click += (sender, e) => clickMe.Text = $"Clicked {++clicks} time{(clicks == 1 ? "" : "s")}";
        var buttons = new GroupBox("Buttons")
        {
            X = 12, Y = 10, Width = 220, Height = 140,
            Children =
            {
                clickMe,
                new Button { X = 12, Y = 64, Width = 196, Height = 30, Text = "Another button" },
                new Button { X = 12, Y = 100, Width = 196, Height = 30, Text = "Disabled", Enabled = false },
            },
        };

        var checks = new GroupBox("Check boxes")
        {
            X = 12, Y = 160, Width = 220, Height = 130,
            Children =
            {
                new CheckBox("Checked") { X = 12, Y = 28, Checked = true },
                new CheckBox("Not checked") { X = 12, Y = 52 },
                new CheckBox("Disabled, checked") { X = 12, Y = 76, Checked = true, Enabled = false },
                new CheckBox("Disabled") { X = 12, Y = 100, Enabled = false },
            },
        };

        var size = new RadioGroup<string>();
        var radios = new GroupBox("Radio buttons")
        {
            X = 244, Y = 10, Width = 190, Height = 140,
            Children =
            {
                new RadioButton<string>(size, "S", "Small") { X = 12, Y = 28 },
                new RadioButton<string>(size, "M", "Medium") { X = 12, Y = 52 },
                new RadioButton<string>(size, "L", "Large") { X = 12, Y = 76 },
                new RadioButton<string>(size, "XL", "Huge (disabled)") { X = 12, Y = 100, Enabled = false },
            },
        };
        size.Value = "M";

        var fruit = new ComboBox<string> { X = 100, Y = 26, Width = 170, Items = { "Apple", "Banana", "Cherry", "Grape", "Orange" }, SelectedIndex = 2 };
        var values = new GroupBox("Values")
        {
            X = 446, Y = 10, Width = 284, Height = 280,
            Children =
            {
                new Label { X = 12, Y = 30, Text = "Combo box:" }, fruit,
                new Label { X = 12, Y = 66, Text = "Number:" }, new NumberInput { X = 100, Y = 62, Width = 90, Value = 42 },
                new Label { X = 12, Y = 102, Text = "Date:" }, new DateInput { X = 100, Y = 98, Width = 170 },
                new Label { X = 12, Y = 138, Text = "Input:" }, new Input { X = 100, Y = 134, Width = 170, Height = 26, Text = "Some text" },
                new Label { X = 12, Y = 174, Text = "Disabled:" }, new Input { X = 100, Y = 170, Width = 170, Height = 26, Text = "Read only", Enabled = false },
                new Label { X = 12, Y = 210, Text = "File:" },
                new FileInput { X = 100, Y = 206, Width = 170, Filters = { new FileFilter("Text files", "*.txt", "*.md"), FileFilter.AllFiles } },
                new Label { X = 12, Y = 246, Text = "Labels follow the style too." },
            },
        };

        return new TabPage("Controls") { Children = { buttons, checks, radios, values } };
    }

    private static TabPage CreateTextPage() => new("Text")
    {
        Children =
        {
            new TextArea
            {
                Anchor = Anchor.Fill(10),
                Text = "A TextArea: select text with the mouse or Shift + arrows, right-click for Cut / Copy / Paste.\n\n"
                    + string.Join("\n", Enumerable.Range(1, 30).Select(i => $"Line {i}: the quick brown fox jumps over the lazy dog.")),
            },
        },
    };

    /// <summary>A tree and a table side by side; the bar between them can be dragged.</summary>
    private static TabPage CreateListsPage()
    {
        var tree = new TreeView { Anchor = Anchor.Fill() };
        tree.Nodes.Add(new TreeNode("Fruit", new TreeNode("Apple"), new TreeNode("Banana"), new TreeNode("Cherry")));
        tree.Nodes.Add(new TreeNode("Vegetables", new TreeNode("Carrot"), new TreeNode("Potato")));
        tree.Nodes.Add(new TreeNode("Drinks", new TreeNode("Tea", new TreeNode("Green"), new TreeNode("Black")), new TreeNode("Coffee")));
        tree.Nodes[0].Expand();
        tree.Nodes[2].ExpandAll();
        tree.SelectedNode = tree.Nodes[0].Nodes[1];

        var table = new Table
        {
            Anchor = Anchor.Fill(),
            RowSelection = true,
            Columns = { new TableColumn("Item", 140), new TableColumn("Price", 80), new TableColumn("Stock", 80) },
        };
        string[][] rows = [["Apple", "1.20", "34"], ["Banana", "0.80", "120"], ["Cherry", "4.50", "8"], ["Grape", "3.10", "0"], ["Orange", "1.00", "57"]];
        foreach (var row in rows)
            table.AddRow(row);
        table[3, 2].Color = new Color(192, 0, 0); // Cells can still have colors of their own.
        table.SelectedIndex = 1;

        var split = new SplitContainer { Anchor = Anchor.Fill(10) };
        split.Panel1.Children.Add(tree);
        split.Panel2.Children.Add(table);
        split.SplitterDistance = 220;
        return new TabPage("Lists") { Children = { split } };
    }

    private static TabPage CreateDisabledPage() => new("Disabled") { Enabled = false };
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
