using Doqua.Controls;
using Doqua.GUI;

namespace TabsExample;

class MainWindow : Window
{
    private readonly TabControl _tabs;
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Tabs";
        Icons = ExampleIcon.Load();
        Width = 640;
        Height = 480;
        Background = new Color(212, 208, 200);

        var general = new TabPage("General")
        {
            Children =
            {
                new Label { Anchor = new Anchor(Left: 16, Top: 20), Text = "Name:" },
                new Input { Anchor = new Anchor(Left: 80, Top: 14, Right: 16), Height = 28 },
                new Label
                {
                    Anchor = new Anchor(Left: 16, Top: 60),
                    Text = "Each tab is a TabPage: a Panel with a title.\nOnly the selected page is shown; it fills the area below the tabs.",
                },
            },
        };
        var shapes = new TabPage("Shapes")
        {
            Children =
            {
                new Rectangle { Anchor = new Anchor(Left: 16, Top: 16), Width = 160, Height = 110, Color = Color.Red },
                new Rectangle { Anchor = new Anchor(Right: 16, Bottom: 16), Width = 160, Height = 110, Color = Color.Blue },
            },
        };
        var locked = new TabPage("Locked") { Enabled = false }; // Disabled from the start: grey tab.

        // Check boxes: one drives the status line, one mirrors the Locked page, one shows the disabled look.
        var showStatus = new CheckBox("Show the status line") { Anchor = new Anchor(Left: 16, Top: 16), Checked = true };
        var lockedEnabled = new CheckBox("Enable the \"Locked\" page") { Anchor = new Anchor(Left: 16, Top: 44) };
        lockedEnabled.CheckedChanged += (sender, e) => locked.Enabled = lockedEnabled.Checked;
        locked.EnabledChanged += (sender, e) => lockedEnabled.Checked = locked.Enabled;
        var disabledChecked = new CheckBox("Disabled and checked") { Anchor = new Anchor(Left: 16, Top: 72), Checked = true, Enabled = false };
        var disabledUnchecked = new CheckBox("Disabled") { Anchor = new Anchor(Left: 16, Top: 100), Enabled = false };

        var settings = new TabPage("Settings")
        {
            Children =
            {
                showStatus, lockedEnabled, disabledChecked, disabledUnchecked,
                new Button { Anchor = new Anchor(Left: 16, Top: 136), Width = 140, Text = "Apply settings" },
            },
        };

        _tabs = new TabControl
        {
            Anchor = new Anchor(Left: 10, Top: 10, Right: 10, Bottom: 90),
            Pages = { general, shapes, settings, locked },
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
