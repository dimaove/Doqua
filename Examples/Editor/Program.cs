using Doqua.Controls;
using Doqua.GUI;

namespace EditorExample;

class MainWindow : Window
{
    private readonly TextArea _editor;
    private readonly Label _status;

    public MainWindow()
    {
        Title = "Doqua Editor";
        Icons = ExampleIcon.Load();
        Width = 640;
        Height = 480;
        Background = new Color(212, 208, 200);

        _editor = new TextArea
        {
            Anchor = new Anchor(Left: 8, Top: 8, Right: 8, Bottom: 36),
            Text = SampleText(),
        };
        _status = new Label { Anchor = new Anchor(Left: 10, Bottom: 10) };
        _editor.SelectionChanged += (sender, e) => UpdateStatus();
        _editor.TextChanged += (sender, e) => UpdateStatus();

        Content = new Panel { Children = { _editor, _status } };
        _editor.Focus();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var selection = _editor.SelectionLength > 0 ? $", {_editor.SelectionLength} selected" : "";
        _status.Text = $"Line {_editor.CaretLine + 1}, column {_editor.CaretColumn + 1}{selection} · "
            + $"{_editor.LineCount} lines, {_editor.Text.Length} characters";
    }

    private static string SampleText()
    {
        var lines = new List<string>
        {
            "Doqua TextArea",
            "",
            "Enter starts a new line; Tab moves the focus as usual.",
            "Arrows, Home/End, PageUp/PageDown and Ctrl+Home/End move the caret; add Shift to select.",
            "Double-click selects a word, triple-click a line. Right-click for Cut / Copy / Paste.",
            "The mouse wheel and the scroll bar scroll the text.",
            "",
            "Кириллица тоже работает: съешь же ещё этих мягких французских булок.",
            "",
        };
        for (var i = 1; i <= 40; i++)
            lines.Add($"Line {i,2}: the quick brown fox jumps over the lazy dog.");
        return string.Join('\n', lines);
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
