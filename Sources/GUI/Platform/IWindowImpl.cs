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

    void SetTitle(string title);
    void Resize(int width, int height);
    void Show();
    void Destroy();
}
