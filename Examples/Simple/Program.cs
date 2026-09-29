using Doqua.Controls;
using Doqua.GUI;
using Timer = Doqua.GUI.Timer;

namespace Simple;

class MainWindow : Window
{
    private const string InitialStatus = "Click a rectangle";

    private readonly Label _status;
    private readonly Button _resetButton;

    public MainWindow()
    {
        Title = "Doqua Simple";
        Icons = ExampleIcon.Load();
        Width = 800;
        Height = 600;

        const int bottomHeight = 100;

        // The menu bar anchors itself to the top edge; its items are filled in below, once the actions exist.
        var menu = new MainMenu();

        // Fills everything between the menu bar and the bottom panel and follows the window size.
        var rectanglesPanel = new Panel
        {
            Anchor = new Anchor(Left: 0, Top: menu.Height, Right: 0, Bottom: bottomHeight),
            Background = Color.LightBlue,
            Children =
            {
                new Label
                {
                    Anchor = new Anchor(Left: 20, Top: 15),
                    Text = "Resize the window",
                    Font = new Font("sans-serif", 20, FontStyle.Bold),
                    Color = Color.Blue,
                },
                // Stays in the top-left corner.
                CreateRectangle(new Anchor(Left: 20, Top: 60), 220, 150, Color.Red),
                // Stays in the bottom-right corner.
                CreateRectangle(new Anchor(Right: 20, Bottom: 20), 220, 150, Color.Green),
            },
        };

        _status = new Label
        {
            Anchor = new Anchor(Left: 20, Top: 14),
            Text = InitialStatus,
            Font = new Font("serif", 18, FontStyle.Italic),
            Color = Color.Gray,
        };

        // Buttons keep their size and stick to the right edge.
        var hideButton = new Button { Anchor = new Anchor(Top: 10, Right: 220), Width = 90, Height = 34, Text = "Hide" };
        var disableButton = new Button { Anchor = new Anchor(Top: 10, Right: 120), Width = 90, Height = 34, Text = "Disable" };
        // The buttons and the View menu share these actions (the menu items are declared further down).
        MenuItem hideItem = new("Hide rectangles", "Ctrl+H"), disableItem = new("Disable rectangles", "Ctrl+D");
        void ToggleVisible()
        {
            rectanglesPanel.Visible = !rectanglesPanel.Visible;
            hideButton.Text = rectanglesPanel.Visible ? "Hide" : "Show";
            hideItem.Text = rectanglesPanel.Visible ? "Hide rectangles" : "Show rectangles";
            disableItem.Enabled = rectanglesPanel.Visible; // Nothing to disable while hidden.
        }
        void ToggleEnabled()
        {
            rectanglesPanel.Enabled = !rectanglesPanel.Enabled;
            disableButton.Text = rectanglesPanel.Enabled ? "Disable" : "Enable";
            disableItem.Text = rectanglesPanel.Enabled ? "Disable rectangles" : "Enable rectangles";
        }
        hideButton.Click += (sender, e) => ToggleVisible();
        disableButton.Click += (sender, e) => ToggleEnabled();

        // Nothing to reset until a rectangle is clicked.
        _resetButton = new Button { Anchor = new Anchor(Top: 10, Right: 20), Width = 90, Height = 34, Text = "Reset", Enabled = false };
        _resetButton.Click += (sender, e) => SetStatus(InitialStatus, Color.Gray);

        var nameLabel = new Label { Anchor = new Anchor(Left: 20, Top: 62), Text = "Your name:" };
        // Left and Right: the input stretches with the window.
        var nameInput = new Input { Anchor = new Anchor(Left: 110, Top: 56, Right: 220), Height = 32 };
        var greetButton = new Button { Anchor = new Anchor(Top: 56, Right: 120), Width = 90, Height = 32, Text = "Greet" };

        void Greet()
        {
            if (nameInput.Text.Length > 0)
            {
                SetStatus($"Hello, {nameInput.Text}!", Color.Purple);
                return;
            }
            // Show a hint for two seconds, then restore what was there.
            var (previousText, previousColor) = (_status.Text, _status.Color);
            SetStatus("Type your name first", Color.Red);
            Timer.SetTimeout(() => SetStatus(previousText, previousColor), 2000);
        }

        greetButton.Click += (sender, e) => Greet();
        nameInput.KeyDown += (sender, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Greet();
                e.Handled = true;
            }
        };
        nameInput.TextChanged += (sender, e) =>
            Title = nameInput.Text.Length > 0 ? $"Doqua Simple – {nameInput.Text}" : "Doqua Simple";

        var clock = new Label { Anchor = new Anchor(Top: 60, Right: 20), Font = Font.Default with { Size = 18 }, Color = Color.Gray };
        void UpdateClock() => clock.Text = DateTime.Now.ToString("HH:mm:ss");
        UpdateClock();
        Timer.SetInterval(UpdateClock, 1000);

        // Fixed height, full width, at the bottom. Children order is also the Tab order.
        var bottomPanel = new Panel
        {
            Anchor = new Anchor(Left: 0, Right: 0, Bottom: 0),
            Height = bottomHeight,
            Background = Color.LightGray,
            Children =
            {
                _status, hideButton, disableButton, _resetButton,
                nameLabel, nameInput, greetButton, clock,
            },
        };

        // File, View and Help. Each item's shortcut text is also its keyboard shortcut; Alt + the underlined letter opens a
        // menu (Alt+F, Alt+V, Alt+H), F10 opens the first one.
        var newItem = new MenuItem("New", "Ctrl+N");
        newItem.Click += (sender, e) =>
        {
            nameInput.Text = "";
            SetStatus(InitialStatus, Color.Gray);
            nameInput.Focus();
        };
        var exitItem = new MenuItem("Exit", "Ctrl+Q");
        exitItem.Click += (sender, e) => Close();
        hideItem.Click += (sender, e) => ToggleVisible();
        disableItem.Click += (sender, e) => ToggleEnabled();
        var greetItem = new MenuItem("Greet", "F5");
        greetItem.Click += (sender, e) => Greet();
        var aboutItem = new MenuItem("About Doqua...", "F1");
        aboutItem.Click += (sender, e) => MessageBox.Show(this,
            $"{Doqua.Core.DoquaInfo.Name} {Doqua.Core.DoquaInfo.Version.ToString(2)}\nA small cross-platform GUI library for .NET.",
            "About", icon: MessageBoxIcon.Information);
        menu.Items.Add(new MainMenuItem("&File") { Items = { newItem, MenuItem.Separator(), exitItem } });
        menu.Items.Add(new MainMenuItem("&View") { Items = { hideItem, disableItem, MenuItem.Separator(), greetItem } });
        menu.Items.Add(new MainMenuItem("&Help") { Items = { aboutItem } });
        menu.ItemClick += (sender, e) => SetStatus($"Menu: {e.Item.Text}", Color.Blue);

        Content = new Panel { Children = { menu, rectanglesPanel, bottomPanel } };
        nameInput.Focus();
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.Color = color;
        _resetButton.Enabled = text != InitialStatus;
    }

    private Rectangle CreateRectangle(Anchor anchor, int width, int height, Color color)
    {
        var rectangle = new Rectangle { Anchor = anchor, Width = width, Height = height, Color = color };
        rectangle.MouseClick += (sender, e) =>
            SetStatus($"{rectangle.Color} rectangle: {e.Button} click at ({e.X}, {e.Y})", rectangle.Color);
        return rectangle;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
