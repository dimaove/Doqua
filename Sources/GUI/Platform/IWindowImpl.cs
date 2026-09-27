namespace Doqua.GUI.Platform;

/// <summary>Native window owned by a <see cref="Window"/>. Sizes are client-area sizes.</summary>
internal interface IWindowImpl
{
    /// <summary>Raised once after the native window has been destroyed.</summary>
    event Action? Closed;

    /// <summary>Raised when the client area size changes (e.g. the user resized the window).</summary>
    event Action<int, int>? Resized;

    /// <summary>Raised when a mouse button is pressed inside the client area.</summary>
    event Action<MouseButton, int, int>? MouseDown;

    /// <summary>
    /// Raised when a mouse button is released. The window keeps receiving the release
    /// (pointer grab / capture) even if the pointer has left the client area.
    /// </summary>
    event Action<MouseButton, int, int>? MouseUp;

    /// <summary>Raised when the pointer moves over the client area (or anywhere, while a button is held).</summary>
    event Action<int, int>? MouseMove;

    /// <summary>Raised when the pointer leaves the client area.</summary>
    event Action? MouseLeave;

    /// <summary>Raised when a key is pressed (also on auto-repeat) while the window is active.</summary>
    event Action<Key, KeyModifiers>? KeyDown;

    /// <summary>Raised with typed text (no control characters) after the matching <see cref="KeyDown"/>.</summary>
    event Action<string>? TextInput;

    /// <summary>Raised when the window gains (true) or loses (false) the keyboard focus of the OS.</summary>
    event Action<bool>? ActiveChanged;

    /// <summary>
    /// Raised when the window needs to be drawn. The handler fills the framebuffer, which is
    /// already sized to the client area; the backend then copies it to the screen.
    /// </summary>
    event Action<Framebuffer>? Paint;

    void SetTitle(string title);
    void Resize(int width, int height);
    void Show();

    /// <summary>Schedules a <see cref="Paint"/>. Multiple requests are merged into one.</summary>
    void Invalidate();

    void Destroy();
}
