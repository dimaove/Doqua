namespace Doqua.GUI.Platform;

/// <summary>Native windowing backend: creates windows and runs the event loop.</summary>
internal interface IPlatform
{
    IFontBackend Fonts { get; }

    IWindowImpl CreateWindow(int width, int height);

    /// <summary>Processes native events until <see cref="Quit"/> is called. Returns the exit code.</summary>
    int RunLoop();

    void Quit(int exitCode);
}
