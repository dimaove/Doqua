using Doqua.Controls;
using Doqua.GUI;

namespace DateTimeExample;

class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Doqua DateTime";
        Icons = ExampleIcon.Load();
        Width = 560;
        Height = 340;
        Background = new Color(212, 208, 200);

        var today = DateTime.Today;

        // The whole period may lie within a year back and a year ahead: other dates are greyed out.
        var from = new DateInput { Anchor = new Anchor(Left: 70, Top: 20), Width = 190, Min = today.AddYears(-1), Value = today };
        var to = new DateInput { Anchor = new Anchor(Left: 340, Top: 20), Width = 190, Max = today.AddYears(1), Value = today.AddDays(7) };

        // "From" stays before "To": each one limits the other. Changing a limit never moves a value here,
        // because a value is always inside the range the other one allows.
        void Link()
        {
            to.Min = from.Value.AddDays(1);
            from.Max = to.Value.AddDays(-1);
        }
        Link();

        var status = new Label { Anchor = new Anchor(Left: 20, Top: 110) };
        void UpdateStatus()
        {
            var days = (to.Value - from.Value).Days;
            status.Text = $"{from.Text} – {to.Text}: {days} day{(days == 1 ? "" : "s")}\n"
                + $"\"From\" can go up to {from.Max:d}, \"To\" can start from {to.Min:d}.";
        }
        from.ValueChanged += (sender, e) => { Link(); UpdateStatus(); };
        to.ValueChanged += (sender, e) => { Link(); UpdateStatus(); };

        var format = new ComboBox<string>
        {
            Anchor = new Anchor(Left: 70, Top: 64), Width = 250,
            Items = { "d", "D", "yyyy-MM-dd", "dd MMM yyyy", "dddd, d MMMM yyyy" },
            ItemText = f => $"{f}   ({today.ToString(f, from.Culture)})",
        };
        format.SelectedItem = "d";
        format.SelectionChanged += (sender, e) =>
        {
            from.Format = to.Format = format.SelectedItem!;
            UpdateStatus();
        };

        Content = new Panel
        {
            Children =
            {
                new Label { Anchor = new Anchor(Left: 20, Top: 24), Text = "From:" },
                from,
                new Label { Anchor = new Anchor(Left: 290, Top: 24), Text = "To:" },
                to,
                new Label { Anchor = new Anchor(Left: 20, Top: 68), Text = "Format:" },
                format,
                status,
                new Label
                {
                    Anchor = new Anchor(Left: 20, Bottom: 16), Color = Color.Gray,
                    Text = "Click a field or its button (or press F4 / Space) to open the calendar;\n"
                        + "click the month title to pick a month, then again to pick a year.",
                },
            },
        };
        UpdateStatus();
        from.Focus();
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
