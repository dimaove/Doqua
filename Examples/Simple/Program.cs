using Doqua.GUI;

namespace Simple;

class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Doqua Simple";
        Width = 800;
        Height = 600;
    }
}

static class Program
{
    [STAThread]
    static int Main()
    {
        var mainWindow = new MainWindow();
        mainWindow.MouseClick += (sender, e) => Console.WriteLine($"{e.Button} click at ({e.X}, {e.Y})");
        return Application.Run(mainWindow);
    }
}
