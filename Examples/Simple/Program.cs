using Doqua.Controls;
using Doqua.GUI;

namespace Simple;

class MainWindow : Window
{
    private const string InitialStatus = "Click a rectangle";

    private readonly Label _status;
    private readonly Button _resetButton;

    public MainWindow()
    {
        Title = "Doqua Simple";
        Width = 800;
        Height = 600;

        var captionFont = new Font("sans-serif", 20, FontStyle.Bold);

        var leftPanel = new Panel
        {
            X = 20, Y = 20, Width = 370, Height = 520,
            Background = Color.LightGray,
            Children =
            {
                new Label { X = 30, Y = 15, Text = "Left panel", Font = captionFont },
                CreateRectangle(30, 60, 200, 150, Color.Red),
                CreateRectangle(100, 250, 220, 230, Color.Green),
            },
        };

        var rightPanel = new Panel
        {
            X = 410, Y = 20, Width = 370, Height = 520,
            Background = Color.LightBlue,
            Children =
            {
                new Label { X = 30, Y = 15, Text = "Правая панель", Font = captionFont, Color = Color.Blue },
                CreateRectangle(30, 60, 300, 120, Color.Blue),
                CreateRectangle(60, 220, 150, 260, Color.Orange),
                new Label
                {
                    X = 70, Y = 235,
                    Text = "Multi-line\nlabel on top\nof a rectangle",
                    Font = Font.Default with { Size = 16 },
                    Color = Color.White,
                },
            },
        };

        _status = new Label
        {
            X = 20, Y = 555,
            Text = InitialStatus,
            Font = new Font("serif", 18, FontStyle.Italic),
            Color = Color.Gray,
        };

        var hideButton = new Button { X = 450, Y = 552, Width = 110, Height = 34, Text = "Hide left" };
        hideButton.Click += (sender, e) =>
        {
            leftPanel.Visible = !leftPanel.Visible;
            hideButton.Text = leftPanel.Visible ? "Hide left" : "Show left";
        };

        var disableButton = new Button { X = 570, Y = 552, Width = 110, Height = 34, Text = "Disable right" };
        disableButton.Click += (sender, e) =>
        {
            rightPanel.Enabled = !rightPanel.Enabled;
            disableButton.Text = rightPanel.Enabled ? "Disable right" : "Enable right";
        };

        // Nothing to reset until a rectangle is clicked.
        _resetButton = new Button { X = 690, Y = 552, Width = 90, Height = 34, Text = "Reset", Enabled = false };
        _resetButton.Click += (sender, e) => SetStatus(InitialStatus, Color.Gray);

        Content = new Panel
        {
            Children = { leftPanel, rightPanel, _status, hideButton, disableButton, _resetButton },
        };
    }

    private void SetStatus(string text, Color color)
    {
        _status.Text = text;
        _status.Color = color;
        _resetButton.Enabled = text != InitialStatus;
    }

    private Rectangle CreateRectangle(int x, int y, int width, int height, Color color)
    {
        var rectangle = new Rectangle { X = x, Y = y, Width = width, Height = height, Color = color };
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
