namespace Doqua.GUI.Platform;

/// <summary>Native windowing backend: creates windows and runs the event loop.</summary>
internal interface IPlatform
{
    IFontBackend Fonts { get; }

    IClipboard Clipboard { get; }

    /// <summary>Maximum time between presses of a double click, in milliseconds.</summary>
    int DoubleClickTime { get; }

    /// <summary>Maximum pointer movement between presses of a double click, in pixels.</summary>
    int DoubleClickDistance { get; }

    IWindowImpl CreateWindow(int width, int height);

    /// <summary>Processes native events until <see cref="Quit"/> is called. Returns the exit code.</summary>
    int RunLoop();

    void Quit(int exitCode);
}
