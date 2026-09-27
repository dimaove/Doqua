namespace Doqua.GUI;

/// <summary>
/// JavaScript-style timers. Callbacks run on the UI thread from the event loop, never while
/// another callback or event handler is running. Call these methods from the UI thread.
/// </summary>
/// <remarks>
/// With implicit usings, System.Threading.Timer is also in scope; outside the Doqua.GUI
/// namespace add <c>using Timer = Doqua.GUI.Timer;</c>.
/// </remarks>
public static class Timer
{
    /// <summary>Runs <paramref name="callback"/> once after <paramref name="milliseconds"/>. Returns the timer id.</summary>
    public static int SetTimeout(Action callback, int milliseconds = 0)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return TimerQueue.Add(callback, Math.Max(0, milliseconds), repeat: false);
    }

    /// <summary>Runs <paramref name="callback"/> every <paramref name="milliseconds"/> until cleared. Returns the timer id.</summary>
    public static int SetInterval(Action callback, int milliseconds)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return TimerQueue.Add(callback, Math.Max(1, milliseconds), repeat: true);
    }

    /// <summary>Cancels a timer. Unknown or already finished ids are ignored.</summary>
    public static void ClearTimeout(int id) => TimerQueue.Remove(id);

    /// <inheritdoc cref="ClearTimeout"/>
    public static void ClearInterval(int id) => TimerQueue.Remove(id);
}

/// <summary>Pending timers ordered by due time; the platform event loop calls <see cref="RunDue"/>.</summary>
internal static class TimerQueue
{
    private sealed class Entry(int id, Action callback, int interval, bool repeat)
    {
        public int Id { get; } = id;
        public Action Callback { get; } = callback;
        public int Interval { get; } = interval;
        public bool Repeat { get; } = repeat;
        public long Due { get; set; }
    }

    private static readonly PriorityQueue<Entry, (long Due, long Sequence)> s_queue = new();
    private static readonly Dictionary<int, Entry> s_active = new();
    private static int s_lastId;
    private static long s_sequence; // Keeps timers with the same due time in creation order.

    /// <summary>Raised when the next due time may have changed (a timer was added or removed).</summary>
    public static event Action? Changed;

    public static int Add(Action callback, int milliseconds, bool repeat)
    {
        var entry = new Entry(++s_lastId, callback, milliseconds, repeat);
        s_active[entry.Id] = entry;
        Schedule(entry, Environment.TickCount64 + milliseconds);
        Changed?.Invoke();
        return entry.Id;
    }

    public static void Remove(int id)
    {
        // The queue entry is dropped lazily when it reaches the front.
        if (s_active.Remove(id))
            Changed?.Invoke();
    }

    /// <summary>Milliseconds until the next timer is due: 0 if one is due now, -1 if there are none.</summary>
    public static int GetTimeout()
    {
        DropCancelled();
        if (!s_queue.TryPeek(out var entry, out _))
            return -1;
        return (int)Math.Clamp(entry.Due - Environment.TickCount64, 0, int.MaxValue);
    }

    /// <summary>
    /// Runs the timers that are due. Timers created by these callbacks wait for the next call,
    /// so a timeout that re-schedules itself with 0 ms cannot block the event loop.
    /// </summary>
    public static void RunDue()
    {
        var now = Environment.TickCount64;
        var firstNew = s_sequence;
        while (true)
        {
            DropCancelled();
            if (!s_queue.TryPeek(out var entry, out var priority) || entry.Due > now || priority.Sequence >= firstNew)
                return;
            s_queue.Dequeue();

            if (entry.Repeat)
            {
                // Keep a steady rhythm, but do not try to catch up after a long pause.
                var next = entry.Due + entry.Interval;
                Schedule(entry, next > now ? next : now + entry.Interval);
            }
            else
            {
                s_active.Remove(entry.Id);
            }
            entry.Callback();
        }
    }

    private static void Schedule(Entry entry, long due)
    {
        entry.Due = due;
        s_queue.Enqueue(entry, (due, s_sequence++));
    }

    private static void DropCancelled()
    {
        while (s_queue.TryPeek(out var entry, out _) && !s_active.ContainsKey(entry.Id))
            s_queue.Dequeue();
    }
}
