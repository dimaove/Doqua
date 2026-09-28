using Doqua.Controls;
using Doqua.GUI;

namespace ThreadsExample;

/// <summary>
/// Doqua's windows and controls belong to the GUI thread. This example changes them from work that runs elsewhere:
/// a background loop that posts readings, an async button handler, progress reports, and what happens when a control
/// is touched from the wrong thread.
/// </summary>
class MainWindow : Window
{
    private readonly Label _latest = new() { Anchor = new Anchor(Left: 16, Top: 64), Font = Font.Default with { Size = 28, Style = FontStyle.Bold }, Text = "–" };
    private readonly Label _count = new() { Anchor = new Anchor(Left: 180, Top: 78), Color = new Color(96, 96, 96) };
    private readonly Table _readings = new()
    {
        Anchor = new Anchor(Left: 16, Top: 116, Bottom: 16), Width = 280,
        Columns = { new TableColumn("Time", 110), new TableColumn("Value", 80), new TableColumn("Thread", 70) },
    };
    private readonly NumberInput _interval = new() { Anchor = new Anchor(Left: 190, Top: 16), Min = 50, Max = 2000, Step = 50, Value = 300 };
    private readonly Label _status = new() { Anchor = new Anchor(Left: 320, Top: 300) };
    private readonly Label _progress = new() { Anchor = new Anchor(Left: 320, Top: 196) };
    private CancellationTokenSource? _worker;
    private int _received;

    public MainWindow()
    {
        Title = "Doqua Threads";
        Icons = ExampleIcon.Load();
        Width = 760;
        Height = 460;
        Background = new Color(212, 208, 200);

        var start = new Button { Anchor = new Anchor(Left: 16, Top: 12), Width = 80, Text = "Start" };
        var stop = new Button { Anchor = new Anchor(Left: 100, Top: 12), Width = 80, Text = "Stop", Enabled = false };
        start.Click += (sender, e) =>
        {
            StartWorker();
            (start.Enabled, stop.Enabled) = (false, true);
        };
        stop.Click += (sender, e) =>
        {
            _worker?.Cancel();
            (start.Enabled, stop.Enabled) = (true, false);
        };

        var compute = new Button { Anchor = new Anchor(Left: 320, Top: 12), Width = 200, Text = "Compute with await" };
        compute.Click += async (sender, e) =>
        {
            // The handler starts on the GUI thread; the work runs on the thread pool; after the await the handler is
            // back on the GUI thread (Doqua's synchronization context), so it can change controls directly.
            compute.Enabled = false;
            _status.Text = $"Computing on a pool thread (clicked on thread {Environment.CurrentManagedThreadId})...";
            var (primes, worker) = await Task.Run(() => (CountPrimes(3_000_000), Environment.CurrentManagedThreadId));
            _status.Text = $"{primes:N0} primes below 3,000,000.\nComputed on thread {worker}, shown on thread "
                + $"{Environment.CurrentManagedThreadId} (the GUI thread: {Application.IsGuiThread}).";
            compute.Enabled = true;
        };

        var copy = new Button { Anchor = new Anchor(Left: 320, Top: 152), Width = 200, Text = "Copy with progress" };
        copy.Click += async (sender, e) =>
        {
            // Progress<T> made on the GUI thread calls its handler there, whichever thread reports.
            copy.Enabled = false;
            var progress = new Progress<int>(percent => _progress.Text = $"Copied {percent} %");
            await Task.Run(() =>
            {
                for (var percent = 0; percent <= 100; percent += 5)
                {
                    Thread.Sleep(80);
                    ((IProgress<int>)progress).Report(percent);
                }
            });
            _progress.Text += " – done";
            copy.Enabled = true;
        };

        var wrong = new Button { Anchor = new Anchor(Left: 320, Top: 256), Width = 200, Text = "Touch a label wrongly" };
        wrong.Click += (sender, e) => Task.Run(() =>
        {
            // Changing a shown control from another thread is detected and throws; the message is posted back.
            try
            {
                _status.Text = "This never appears";
            }
            catch (InvalidOperationException ex)
            {
                var thread = Environment.CurrentManagedThreadId;
                Application.Post(() => _status.Text = $"Caught on thread {thread}:\n{ex.Message.Replace("; ", ";\n")}");
            }
        });

        Content = new Panel
        {
            Children =
            {
                start, stop, new Label { Anchor = new Anchor(Left: 276, Top: 18), Text = "ms" },
                _interval, _latest, _count, _readings,
                compute, new Label { Anchor = new Anchor(Left: 320, Top: 52), Color = new Color(96, 96, 96),
                    Text = "Counts primes on the thread pool;\nthe result is shown after await." },
                copy, _progress, wrong, _status,
            },
        };
        _status.Text = $"GUI thread: {Environment.CurrentManagedThreadId}. Start the reader, or try the buttons.";
    }

    /// <summary>
    /// A background loop that "reads from the network": it asks the GUI for the interval (InvokeAsync), waits, then
    /// posts each reading to the GUI thread (Post).
    /// </summary>
    private void StartWorker()
    {
        _worker = new CancellationTokenSource();
        var token = _worker.Token;
        Task.Run(async () =>
        {
            var random = new Random();
            var value = 20.0;
            while (!token.IsCancellationRequested)
            {
                var interval = await Application.InvokeAsync(() => _interval.Value); // A value owned by the GUI.
                try
                {
                    await Task.Delay(interval, token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                value += random.NextDouble() - 0.5;
                var reading = value;
                var thread = Environment.CurrentManagedThreadId;
                Application.Post(() => ShowReading(reading, thread));
            }
        });
    }

    /// <summary>Runs on the GUI thread (posted by the worker).</summary>
    private void ShowReading(double value, int thread)
    {
        _received++;
        _latest.Text = $"{value:F2} °C";
        _count.Text = $"{_received} readings";
        var row = _readings.AddRow(DateTime.Now.ToString("HH:mm:ss.fff"), value.ToString("F2"), thread.ToString());
        if (_readings.Rows.Count > 200)
            _readings.Rows.RemoveAt(0);
        _readings.ScrollToRow(_readings.Rows.Count - 1);
        row[1].Color = value >= 20 ? new Color(192, 0, 0) : new Color(0, 0, 192);
    }

    private static int CountPrimes(int limit)
    {
        var composite = new bool[limit];
        var count = 0;
        for (var i = 2; i < limit; i++)
        {
            if (composite[i])
                continue;
            count++;
            for (long j = (long)i * i; j < limit; j += i)
                composite[j] = true;
        }
        return count;
    }
}

static class Program
{
    [STAThread]
    static int Main() => Application.Run(new MainWindow());
}
