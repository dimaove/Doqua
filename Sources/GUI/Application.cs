using System.Collections.Concurrent;
using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Runs the application: shows the main window and processes events until it is closed.
/// <para>
/// Windows, controls and timers belong to the GUI thread: the thread that creates the first <see cref="Window"/> and
/// calls <see cref="Run"/>. Other threads hand work to it with <see cref="Post"/> or <see cref="InvokeAsync(Action)"/>;
/// on the GUI thread, <c>await</c> comes back to the GUI thread by itself (see <see cref="IsGuiThread"/>).
/// </para>
/// </summary>
public static class Application
{
    private static readonly ConcurrentQueue<Action> s_posted = new();
    private static volatile IPlatform? s_platform;
    private static int s_guiThreadId; // 0 until the first window is created.

    internal static IPlatform Platform => s_platform ??= PlatformFactory.Create();

    /// <summary>The window passed to <see cref="Run"/>, or null before it is called.</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// True on the GUI thread, and on any thread before the first window has been created. Code on other threads must
    /// not touch windows, shown controls or timers; it can <see cref="Post"/> the work instead.
    /// </summary>
    public static bool IsGuiThread => s_guiThreadId == 0 || s_guiThreadId == Environment.CurrentManagedThreadId;

    /// <summary>
    /// Shows <paramref name="mainWindow"/> and runs the event loop until it is closed.
    /// Must be called from the thread that created the windows.
    /// </summary>
    public static int Run(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        VerifyAccess();
        if (MainWindow != null)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.AlreadyRunning"));

        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Platform.Quit(0);
        mainWindow.Show();
        return Platform.RunLoop();
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the GUI thread, soon: between events, in the order the actions were posted.
    /// Can be called from any thread (also from the GUI thread) and returns at once. An exception thrown by the action
    /// is treated like one thrown by an event handler. Actions posted after <see cref="Run"/> has returned never run.
    /// <example>
    /// <code>
    /// // On a thread that reads from the network:
    /// var value = socket.ReadValue();
    /// Application.Post(() => valueLabel.Text = value.ToString());
    /// </code>
    /// </example>
    /// </summary>
    public static void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        s_posted.Enqueue(action);
        s_platform?.Wake(); // Before the platform exists, the queue is simply run once the event loop starts.
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the GUI thread (see <see cref="Post"/>); the task completes after it has run, or
    /// fails with its exception.
    /// </summary>
    public static Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(() =>
        {
            action();
            return true;
        });
    }

    /// <summary>
    /// Runs <paramref name="function"/> on the GUI thread (see <see cref="Post"/>) and gives its result, e.g. a value a
    /// background thread needs from a control: <c>var port = await Application.InvokeAsync(() => portInput.Text);</c>
    /// </summary>
    public static Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                completion.SetResult(function());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    /// <summary>
    /// Makes the current thread the GUI thread (when the first window is created), with a synchronization context that
    /// posts continuations to it, so <c>await</c> in GUI code resumes on the GUI thread.
    /// </summary>
    internal static void EnsureGuiThread()
    {
        if (s_guiThreadId != 0)
        {
            VerifyAccess();
            return;
        }
        s_guiThreadId = Environment.CurrentManagedThreadId;
        if (SynchronizationContext.Current == null)
            SynchronizationContext.SetSynchronizationContext(new GuiSynchronizationContext());
    }

    /// <summary>Throws unless called on the GUI thread (or before there is one).</summary>
    internal static void VerifyAccess()
    {
        if (!IsGuiThread)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.WrongThread"));
    }

    /// <summary>
    /// Runs the posted actions; called by the event loop on the GUI thread. Actions posted meanwhile wait for the next
    /// call, so an action that posts itself again cannot block the loop.
    /// </summary>
    internal static void RunPosted()
    {
        for (var count = s_posted.Count; count > 0 && s_posted.TryDequeue(out var action); count--)
            action();
    }
}

/// <summary>
/// Synchronization context of the GUI thread: <see cref="Post"/> queues the callback with <see cref="Application.Post"/>,
/// so the continuation of an <c>await</c> started on the GUI thread (and <see cref="Progress{T}"/> callbacks) run there.
/// </summary>
internal sealed class GuiSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback callback, object? state) => Application.Post(() => callback(state));

    /// <summary>Runs the callback on the GUI thread and waits for it (at once when already there).</summary>
    public override void Send(SendOrPostCallback callback, object? state)
    {
        if (Application.IsGuiThread)
            callback(state);
        else
            Application.InvokeAsync(() => callback(state)).GetAwaiter().GetResult();
    }

    public override SynchronizationContext CreateCopy() => this;
}
