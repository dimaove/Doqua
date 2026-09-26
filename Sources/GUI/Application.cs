using Doqua.GUI.Platform;

namespace Doqua.GUI;

public static class Application
{
    private static IPlatform? s_platform;

    internal static IPlatform Platform => s_platform ??= PlatformFactory.Create();

    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// Shows <paramref name="mainWindow"/> and runs the event loop until it is closed.
    /// Must be called from the thread that created the windows.
    /// </summary>
    public static int Run(Window mainWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);
        if (MainWindow != null)
            throw new InvalidOperationException("Application is already running.");

        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => Platform.Quit(0);
        mainWindow.Show();
        return Platform.RunLoop();
    }
}
