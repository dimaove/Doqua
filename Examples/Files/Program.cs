using Doqua.Controls;
using Doqua.GUI;

namespace FilesExample;

class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Doqua Files";
        Icons = ExampleIcon.Load();
        Width = 720;
        Height = 440;
        Background = new Color(212, 208, 200);

        var root = new Panel();
        var y = 20;

        // Adds a caption, the input and a line under it that shows the input's Error (or the full path).
        void AddRow(string caption, FileInput input)
        {
            var status = new Label { Anchor = new Anchor(Left: 190, Top: y + 30) };
            void UpdateStatus()
            {
                var error = input.Error;
                status.Color = error != null ? new Color(192, 0, 0) : ClassicShadow;
                status.Text = error ?? (input.FullPath.Length > 0 ? input.FullPath : "(empty)");
            }
            input.Anchor = new Anchor(Left: 190, Top: y, Right: 20);
            input.TextChanged += (sender, e) => UpdateStatus();
            UpdateStatus();
            root.Children.Add(new Label { Anchor = new Anchor(Left: 20, Top: y + 4), Text = caption });
            root.Children.Add(input);
            root.Children.Add(status);
            y += 62;
        }

        // Must exist (the default) and must be given; the browser offers two masks.
        AddRow("Private key:", new FileInput
        {
            Required = true,
            Filters = { new FileFilter("Keys", "*.pem", "*.key"), FileFilter.AllFiles },
        });

        // The name must match one of the masks, even when typed.
        AddRow("Certificate:", new FileInput
        {
            MatchFilter = true,
            Filters = { new FileFilter("Certificates", "*.pem", "*.crt", "*.cer") },
        });

        // A new file may be named: only its folder must exist. Select takes the shown folder and the typed name.
        AddRow("Log file (new or existing):", new FileInput
        {
            MustExist = false,
            Filters = { new FileFilter("Log files", "*.log"), FileFilter.AllFiles },
            Text = Path.Combine(Path.GetTempPath(), "files-example.log"),
        });

        AddRow("Folder:", new FileInput { Mode = FileInputMode.Folder, ShowHidden = true });

        // Paths inside the example's folder are written relative to it, and resolved against it.
        AddRow("Relative to the example:", new FileInput
        {
            BaseDirectory = AppContext.BaseDirectory,
            RelativePaths = true,
            Text = "files.dll",
        });

        root.Children.Add(new Label
        {
            Anchor = new Anchor(Left: 20, Top: y + 6),
            Color = ClassicShadow,
            Text = "Type a path, or click \"...\" (or press F4) to browse.\n"
                + "In the browser, double-click opens a folder or chooses a file, Backspace goes up,\n"
                + "and a letter jumps to the next name starting with it.",
        });
        Content = root;
    }

    private static Color ClassicShadow => new(96, 96, 96);
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
