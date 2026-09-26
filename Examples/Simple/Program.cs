using Doqua.Controls;
using Doqua.GUI;

namespace Simple;

class MainWindow : Window
{
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Simple";
        Width = 800;
        Height = 600;

        var captionFont = new Font("sans-serif", 20, FontStyle.Bold);
        _status = new Label
        {
            X = 20, Y = 555,
            Text = "Click a rectangle",
            Font = new Font("serif", 18, FontStyle.Italic),
            Color = Color.Gray,
        };

        Content = new Panel
        {
            Children =
            {
                new Panel
                {
                    X = 20, Y = 20, Width = 370, Height = 520,
                    Background = Color.LightGray,
                    Children =
                    {
                        new Label { X = 30, Y = 15, Text = "Left panel", Font = captionFont },
                        CreateRectangle(30, 60, 200, 150, Color.Red),
                        CreateRectangle(100, 250, 220, 230, Color.Green),
                    },
                },
                new Panel
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
                },
                _status,
            },
        };
    }

    private Rectangle CreateRectangle(int x, int y, int width, int height, Color color)
    {
        var rectangle = new Rectangle { X = x, Y = y, Width = width, Height = height, Color = color };
        rectangle.MouseClick += (sender, e) =>
        {
            _status.Text = $"{rectangle.Color} rectangle: {e.Button} click at ({e.X}, {e.Y})";
            _status.Color = rectangle.Color;
        };
        return rectangle;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
