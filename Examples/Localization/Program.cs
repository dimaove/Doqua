using System.Globalization;
using Doqua.Controls;
using Doqua.GUI;

namespace LocalizationExample;

class MainWindow : Window
{
    private const string HelpText = "Type your name and pick a date.\n"
        + "Right-click the name for Cut / Copy / Paste; the calendar shows\n"
        + "month and weekday names in the chosen language.";

    private readonly Label _languageLabel = new() { Anchor = new Anchor(Left: 20, Top: 22) };
    private readonly Label _nameLabel = new() { Anchor = new Anchor(Left: 20, Top: 66) };
    private readonly Label _birthdayLabel = new() { Anchor = new Anchor(Left: 20, Top: 106) };
    private readonly Input _name = new() { Anchor = new Anchor(Left: 150, Top: 60, Right: 20), Height = 28 };
    private readonly DateInput _birthday = new() { Anchor = new Anchor(Left: 150, Top: 100), Width = 290, Format = "D" };
    private readonly Button _hello = new() { Anchor = new Anchor(Left: 150, Top: 144), Width = 170, Height = 30 };
    private readonly Button _error = new() { Anchor = new Anchor(Left: 330, Top: 144), Width = 170, Height = 30 };
    private readonly Label _help = new() { Anchor = new Anchor(Left: 20, Top: 196), Color = Color.Gray };
    private readonly Label _status = new() { Anchor = new Anchor(Left: 20, Bottom: 16) };

    public MainWindow()
    {
        Icons = ExampleIcon.Load();
        Width = 640;
        Height = 400;
        Background = new Color(212, 208, 200);

        // Nothing to do for English. The Russian texts (Doqua's and ours) are in a JSON file next to the program.
        Localization.LoadJson("ru", Path.Combine(AppContext.BaseDirectory, "ru.json"));

        _birthday.Max = DateTime.Today;
        _birthday.Value = DateTime.Today.AddYears(-30);

        // Language names are shown in their own language, so they are not translated.
        var language = new RadioGroup<string>();
        var english = new RadioButton<string>(language, "en-US", "English") { Anchor = new Anchor(Left: 150, Top: 20) };
        var russian = new RadioButton<string>(language, "ru-RU", "Русский") { Anchor = new Anchor(Left: 250, Top: 20) };
        language.Value = Localization.Culture.Name;
        language.ValueChanged += (sender, e) => Localization.Culture = CultureInfo.GetCultureInfo(language.Value!);

        _hello.Click += (sender, e) => _status.Text = _name.Text.Length > 0
            ? Localization.Format("Hello, {0}! You were born on {1:D}.", _name.Text, _birthday.Value)
            : Localization.Get("Please type your name first.");

        // A real Doqua exception: adding a control that already has a parent. Its message is translated too.
        _error.Click += (sender, e) =>
        {
            try
            {
                var child = new Label();
                _ = new Panel { Children = { child } };
                new Panel().Children.Add(child);
            }
            catch (InvalidOperationException exception)
            {
                _status.Text = Localization.Format("Error: {0}", exception.Message);
            }
        };

        // Texts set on controls are updated by the application when the language changes;
        // Doqua's own texts (menus, calendar, date format) follow by themselves.
        Localization.CultureChanged += (sender, e) => ApplyTexts();
        ApplyTexts();

        Content = new Panel
        {
            Children =
            {
                _languageLabel, english, russian,
                _nameLabel, _name,
                _birthdayLabel, _birthday,
                _hello, _error,
                _help, _status,
            },
        };
        _name.Focus();
    }

    private void ApplyTexts()
    {
        Title = Localization.Get("Doqua Localization");
        _languageLabel.Text = Localization.Get("Language:");
        _nameLabel.Text = Localization.Get("Name:");
        _birthdayLabel.Text = Localization.Get("Birthday:");
        _hello.Text = Localization.Get("Say hello");
        _error.Text = Localization.Get("Cause an error");
        _help.Text = Localization.Get(HelpText);
        _status.Text = "";
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
