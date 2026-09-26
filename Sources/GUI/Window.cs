using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>Top-level window decorated by the OS window manager.</summary>
public class Window
{
    private readonly IWindowImpl _impl;
    private string _title = "";
    private int _width = 800;
    private int _height = 600;
    private int _pressedButtons; // Bit mask of MouseButton values pressed inside the client area.

    public Window()
    {
        _impl = Application.Platform.CreateWindow(_width, _height);
        _impl.Resized += (w, h) => { _width = w; _height = h; };
        _impl.Closed += () => { IsClosed = true; OnClosed(); };
        _impl.MouseDown += (button, _, _) => _pressedButtons |= 1 << (int)button;
        _impl.MouseUp += HandleMouseUp;
    }

    public string Title
    {
        get => _title;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ThrowIfClosed();
            _title = value;
            _impl.SetTitle(value);
        }
    }

    /// <summary>Client area width in pixels.</summary>
    public int Width
    {
        get => _width;
        set => Resize(value, _height);
    }

    /// <summary>Client area height in pixels.</summary>
    public int Height
    {
        get => _height;
        set => Resize(_width, value);
    }

    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    /// <summary>Raised when a mouse button is pressed and released inside the client area.</summary>
    public event EventHandler<MouseEventArgs>? MouseClick;

    public void Show()
    {
        ThrowIfClosed();
        _impl.Show();
    }

    public void Close()
    {
        if (!IsClosed)
            _impl.Destroy();
    }

    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);

    private void HandleMouseUp(MouseButton button, int x, int y)
    {
        var mask = 1 << (int)button;
        var wasPressed = (_pressedButtons & mask) != 0;
        _pressedButtons &= ~mask;

        if (wasPressed && x >= 0 && y >= 0 && x < _width && y < _height)
            OnMouseClick(new MouseEventArgs(button, x, y));
    }

    private void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ThrowIfClosed();
        _width = width;
        _height = height;
        _impl.Resize(width, height);
    }

    private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(IsClosed, this);
}
