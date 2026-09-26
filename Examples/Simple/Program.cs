using Doqua.Controls;
using Doqua.GUI;

namespace Simple;

class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Doqua Simple";
        Width = 800;
        Height = 600;

        Content = new Panel
        {
            Children =
            {
                new Panel
                {
                    X = 20, Y = 20, Width = 370, Height = 560,
                    Background = Color.LightGray,
                    Children =
                    {
                        CreateRectangle(30, 30, 200, 150, Color.Red),
                        CreateRectangle(100, 250, 220, 250, Color.Green),
                    },
                },
                new Panel
                {
                    X = 410, Y = 20, Width = 370, Height = 560,
                    Background = Color.LightBlue,
                    Children =
                    {
                        CreateRectangle(30, 30, 300, 120, Color.Blue),
                        CreateRectangle(60, 200, 150, 300, Color.Orange),
                    },
                },
            },
        };
    }

    private static Rectangle CreateRectangle(int x, int y, int width, int height, Color color)
    {
        var rectangle = new Rectangle { X = x, Y = y, Width = width, Height = height, Color = color };
        rectangle.MouseClick += (sender, e) =>
            Console.WriteLine($"{rectangle.Color} rectangle: {e.Button} click at ({e.X}, {e.Y})");
        return rectangle;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
