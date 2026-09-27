using Doqua.Controls;
using Doqua.GUI;

namespace TabsExample;

enum PreviewSize
{
    Small,
    Medium,
    Large,
    Huge,
}

class MainWindow : Window
{
    private readonly TabControl _tabs;
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Tabs";
        Icons = ExampleIcon.Load();
        Width = 760;
        Height = 480;
        Background = new Color(212, 208, 200);

        // The input's own Cut / Copy / Paste / Select all come first, then these items.
        var nameInput = new Input { Anchor = new Anchor(Left: 80, Top: 14, Right: 16), Height = 28 };
        var clearItem = new MenuItem("Clear");
        clearItem.Click += (sender, e) => nameInput.Text = "";
        var timeItem = new MenuItem("Insert time");
        timeItem.Click += (sender, e) => nameInput.Text += DateTime.Now.ToString("HH:mm:ss");
        nameInput.ContextMenu = new PopupMenu { Items = { clearItem, timeItem } };
        nameInput.ContextMenu.Closed += (sender, e) =>
            ShowMessage(e.SelectedItem is { } item ? $"Name menu: {item.Text}" : "Name menu closed without a selection");

        var description = new Label { Anchor = new Anchor(Left: 16, Top: 60) };

        // Combo boxes: a list longer than the drop-down (it scrolls), typed values with custom text, a disabled one.
        var language = new ComboBox<string>
        {
            Anchor = new Anchor(Left: 110, Top: 196), Width = 200, PlaceholderText = "Choose a language",
            Items = { "English", "Русский", "Deutsch", "Français", "Español", "Italiano", "Português", "Polski",
                      "Українська", "Čeština", "Nederlands", "Svenska", "Suomi", "Türkçe" },
        };
        language.SelectionChanged += (sender, e) =>
            ShowMessage(language.HasSelection ? $"Language: {language.SelectedItem}" : "No language selected");
        var textSize = new ComboBox<int>
        {
            Anchor = new Anchor(Left: 110, Top: 232), Width = 120,
            Items = { 10, 12, 14, 16, 18 }, ItemText = size => $"{size} px",
        };
        textSize.SelectedItem = 14;
        textSize.SelectionChanged += (sender, e) => description.Font = Font.Default with { Size = textSize.SelectedItem };

        var general = new TabPage("General")
        {
            Children =
            {
                new Label { Anchor = new Anchor(Left: 16, Top: 20), Text = "Name:" },
                nameInput,
                new Label { Anchor = new Anchor(Left: 16, Top: 200), Text = "Language:" },
                language,
                new Label { Anchor = new Anchor(Left: 16, Top: 236), Text = "Text size:" },
                textSize,
                new Label { Anchor = new Anchor(Left: 16, Top: 272), Text = "Disabled:" },
                new ComboBox<string> { Anchor = new Anchor(Left: 110, Top: 268), Width = 200, Items = { "Not available" }, SelectedIndex = 0, Enabled = false },
                description,
            },
        };
        description.Text =
            "Each tab is a TabPage: a Panel with a title.\nOnly the selected page is shown; it fills the area below the tabs.\n\n"
            + "Right-click the name field: its Cut / Copy / Paste are merged with the application's items.\n"
            + "On the Shapes page, right-click a rectangle to change its color.";

        // One menu shared by both rectangles: Owner tells which one was right-clicked.
        var colorMenu = new PopupMenu
        {
            Items =
            {
                new MenuItem("Red") { Tag = Color.Red },
                new MenuItem("Green") { Tag = Color.Green },
                new MenuItem("Blue") { Tag = Color.Blue },
                MenuItem.Separator(),
                new MenuItem("Transparent (not available)") { Enabled = false },
            },
        };
        colorMenu.Closed += (sender, e) =>
        {
            if (e.SelectedItem is { Tag: Color color } && colorMenu.Owner is Rectangle rectangle)
            {
                rectangle.Color = color;
                ShowMessage($"Rectangle color: {e.SelectedItem.Text}");
            }
            else
            {
                ShowMessage("Color menu closed without a selection");
            }
        };
        var shapes = new TabPage("Shapes")
        {
            Children =
            {
                new Rectangle { Anchor = new Anchor(Left: 16, Top: 16), Width = 160, Height = 110, Color = Color.Red, ContextMenu = colorMenu },
                new Rectangle { Anchor = new Anchor(Right: 16, Bottom: 16), Width = 160, Height = 110, Color = Color.Blue, ContextMenu = colorMenu },
            },
        };
        var locked = new TabPage("Locked") { Enabled = false }; // Disabled from the start: grey tab.

        // Scrolling panels: a vertical list taller than its panel, and a grid larger than a stretching panel.
        var options = new Panel
        {
            Anchor = new Anchor(Left: 16, Top: 16, Bottom: 16), Width = 220,
            Background = Color.White, ScrollBars = ScrollBars.Vertical,
        };
        for (var i = 1; i <= 20; i++)
            options.Children.Add(new CheckBox($"Option {i}") { X = 8, Y = 6 + (i - 1) * 24, Checked = i % 3 == 0 });

        var grid = new Panel
        {
            Anchor = new Anchor(Left: 252, Top: 16, Right: 16, Bottom: 16),
            Background = Color.White, ScrollBars = ScrollBars.Both,
        };
        Color[] palette = [Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Purple];
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                grid.Children.Add(new Rectangle
                {
                    X = 10 + column * 90, Y = 10 + row * 70, Width = 80, Height = 60,
                    Color = Color.Lerp(palette[(row + column) % palette.Length], Color.White, 0.35f),
                });
                grid.Children.Add(new Label { X = 18 + column * 90, Y = 30 + row * 70, Text = $"{(char)('A' + column)}{row + 1}" });
            }
        }
        var scrolling = new TabPage("Scrolling") { Children = { options, grid } };

        // Check boxes: one drives the status line, one mirrors the Locked page, one shows the disabled look.
        var showStatus = new CheckBox("Show the status line") { Anchor = new Anchor(Left: 16, Top: 16), Checked = true };
        var lockedEnabled = new CheckBox("Enable the \"Locked\" page") { Anchor = new Anchor(Left: 16, Top: 44) };
        lockedEnabled.CheckedChanged += (sender, e) => locked.Enabled = lockedEnabled.Checked;
        locked.EnabledChanged += (sender, e) => lockedEnabled.Checked = locked.Enabled;
        var disabledChecked = new CheckBox("Disabled and checked") { Anchor = new Anchor(Left: 16, Top: 72), Checked = true, Enabled = false };
        var disabledUnchecked = new CheckBox("Disabled") { Anchor = new Anchor(Left: 16, Top: 100), Enabled = false };

        // Two independent radio groups; each group holds its value, the buttons only show it.
        var previewSize = new RadioGroup<PreviewSize>();
        var previewColor = new RadioGroup<Color>();
        var preview = new Rectangle { Anchor = new Anchor(Left: 300, Top: 150), Height = 40, Color = Color.LightGray };
        var summary = new Label { Anchor = new Anchor(Left: 300, Top: 200) };
        void UpdatePreview()
        {
            preview.Width = previewSize.Value switch { PreviewSize.Small => 40, PreviewSize.Medium => 90, _ => 150 };
            preview.Color = previewColor.HasValue ? previewColor.Value : Color.LightGray;
            summary.Text = $"Size: {previewSize.Value}, color: {(previewColor.HasValue ? previewColor.Value.ToString() : "none")}";
        }
        previewSize.ValueChanged += (sender, e) => UpdatePreview();
        previewColor.ValueChanged += (sender, e) => UpdatePreview();

        var settings = new TabPage("Settings")
        {
            Children =
            {
                showStatus, lockedEnabled, disabledChecked, disabledUnchecked,
                new Button { Anchor = new Anchor(Left: 16, Top: 136), Width = 140, Text = "Apply settings" },

                new Label { Anchor = new Anchor(Left: 300, Top: 16), Text = "Preview size:" },
                new RadioButton<PreviewSize>(previewSize, PreviewSize.Small, "Small") { Anchor = new Anchor(Left: 300, Top: 40) },
                new RadioButton<PreviewSize>(previewSize, PreviewSize.Medium, "Medium") { Anchor = new Anchor(Left: 300, Top: 62) },
                new RadioButton<PreviewSize>(previewSize, PreviewSize.Large, "Large") { Anchor = new Anchor(Left: 300, Top: 84) },
                new RadioButton<PreviewSize>(previewSize, PreviewSize.Huge, "Huge (disabled)") { Anchor = new Anchor(Left: 300, Top: 106), Enabled = false },

                new Label { Anchor = new Anchor(Left: 450, Top: 16), Text = "Preview color:" },
                new RadioButton<Color>(previewColor, Color.Red, "Red") { Anchor = new Anchor(Left: 450, Top: 40) },
                new RadioButton<Color>(previewColor, Color.Green, "Green") { Anchor = new Anchor(Left: 450, Top: 62) },
                new RadioButton<Color>(previewColor, Color.Blue, "Blue") { Anchor = new Anchor(Left: 450, Top: 84) },

                preview, summary,
            },
        };
        previewSize.Value = PreviewSize.Medium; // From code: checks the "Medium" button.
        UpdatePreview();

        _tabs = new TabControl
        {
            Anchor = new Anchor(Left: 10, Top: 10, Right: 10, Bottom: 90),
            Pages = { general, shapes, settings, scrolling, locked },
        };
        _tabs.SelectedPageChanged += (sender, e) => UpdateStatus();

        _status = new Label { Anchor = new Anchor(Left: 12, Bottom: 58) };
        showStatus.CheckedChanged += (sender, e) => _status.Visible = showStatus.Checked;

        // One button per page toggles its Enabled; the last one toggles all pages.
        var buttons = new List<Control>();
        var x = 10;
        foreach (var page in _tabs.Pages)
        {
            var button = new Button { Anchor = new Anchor(Left: x, Bottom: 12), Width = 122, Height = 32 };
            void Refresh() => button.Text = (page.Enabled ? "Disable " : "Enable ") + page.Title;
            Refresh();
            button.Click += (sender, e) =>
            {
                page.Enabled = !page.Enabled;
                Refresh();
                UpdateStatus();
            };
            page.EnabledChanged += (sender, e) => Refresh();
            buttons.Add(button);
            x += 126;
        }
        var allButton = new Button { Anchor = new Anchor(Right: 10, Bottom: 12), Width = 100, Height = 32, Text = "Disable all" };
        allButton.Click += (sender, e) =>
        {
            var enable = _tabs.Pages.All(page => !page.Enabled);
            foreach (var page in _tabs.Pages)
                page.Enabled = enable;
            allButton.Text = enable ? "Disable all" : "Enable all";
            UpdateStatus();
        };

        var root = new Panel { Children = { _tabs, _status, allButton } };
        foreach (var button in buttons)
            root.Children.Add(button);
        Content = root;
        UpdateStatus();
    }

    private void ShowMessage(string text) => _status.Text = text;

    private void UpdateStatus() =>
        _status.Text = _tabs.SelectedPage is { } page
            ? $"Selected: {page.Title} (index {_tabs.SelectedIndex}); Ctrl+Tab switches pages while focus is inside."
            : "No page selected: every page is disabled.";
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
