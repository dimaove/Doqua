using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>Runs the application: shows the main window and processes events until it is closed.</summary>
public static class Application
{
    private static IPlatform? s_platform;

    internal static IPlatform Platform => s_platform ??= PlatformFactory.Create();

    /// <summary>The window passed to <see cref="Run"/>, or null before it is called.</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Shows <paramref name="mainWindow"/> and runs the event loop until it is closed.
    /// Must be called from the thread that created the windows.
    /// </summary>
    public static int Run(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        if (MainWindow != null)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.AlreadyRunning"));

        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Platform.Quit(0);
        mainWindow.Show();
        return Platform.RunLoop();
    }
}
