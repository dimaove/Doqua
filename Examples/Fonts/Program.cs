using Doqua.Controls;
using Doqua.GUI;

namespace FontsExample;

class MainWindow : Window
{
    private readonly ComboBox<string> _family;
    private readonly NumberInput _size;
    private readonly CheckBox _bold;
    private readonly CheckBox _italic;
    private readonly Label _sample;

    public MainWindow()
    {
        Title = "Doqua Fonts";
        Icons = ExampleIcon.Load();
        Width = 720;
        Height = 480;
        Background = new Color(212, 208, 200);

        var families = Font.GetInstalledFamilies();
        _family = new ComboBox<string> { Anchor = new Anchor(Left: 60, Top: 12), Width = 260, MaxDropDownItems = 14 };
        foreach (var family in families)
            _family.Items.Add(family);
        // Start on a common sans-serif family if one is installed.
        var preferred = new[] { "Segoe UI", "Noto Sans", "DejaVu Sans", "Liberation Sans", "Arial" }.FirstOrDefault(families.Contains);
        if (preferred != null)
            _family.SelectedItem = preferred;
        else if (families.Count > 0)
            _family.SelectedIndex = 0;

        _size = new NumberInput { Anchor = new Anchor(Left: 380, Top: 12), Width = 70, Min = 6, Max = 120, Value = 28 };
        _bold = new CheckBox("Bold") { Anchor = new Anchor(Left: 470, Top: 15) };
        _italic = new CheckBox("Italic") { Anchor = new Anchor(Left: 540, Top: 15) };

        _sample = new Label
        {
            X = 12, Y = 8,
            Text = "The quick brown fox jumps over the lazy dog.\n"
                + "Съешь же ещё этих мягких французских булок, да выпей чаю.\n"
                + "0123456789  !?.,;:()[]{}<>@#$%&*+-=/\\|\"'",
        };

        // Large sizes do not fit: the sample scrolls both ways.
        var preview = new Panel
        {
            Anchor = new Anchor(Left: 12, Top: 48, Right: 12, Bottom: 36),
            Background = Color.White,
            ScrollBars = ScrollBars.Both,
            Children = { _sample },
        };
        var status = new Label { Anchor = new Anchor(Left: 12, Bottom: 10), Color = Color.Gray };

        void UpdateFont()
        {
            if (!_family.HasSelection)
                return;
            var style = (_bold.Checked ? FontStyle.Bold : FontStyle.Regular) | (_italic.Checked ? FontStyle.Italic : FontStyle.Regular);
            _sample.Font = new Font(_family.SelectedItem!, _size.Value, style);
            status.Text = $"{families.Count} installed families · {_sample.Font}";
        }
        _family.SelectionChanged += (sender, e) => UpdateFont();
        _size.ValueChanged += (sender, e) => UpdateFont();
        _bold.CheckedChanged += (sender, e) => UpdateFont();
        _italic.CheckedChanged += (sender, e) => UpdateFont();

        Content = new Panel
        {
            Children =
            {
                new Label { Anchor = new Anchor(Left: 12, Top: 16), Text = "Font:" },
                _family,
                new Label { Anchor = new Anchor(Left: 336, Top: 16), Text = "Size:" },
                _size,
                _bold,
                _italic,
                preview,
                status,
            },
        };
        UpdateFont();
        _family.Focus();
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
