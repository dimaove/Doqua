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
    static int Main() => Application.Run(new MainWindow());
}
