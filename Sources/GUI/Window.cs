using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>Top-level window decorated by the OS window manager.</summary>
public class Window
{
    private readonly IWindowImpl _impl;
    private string _title = "";
    private int _width = 800;
    private int _height = 600;

    public Window()
    {
        _impl = Application.Platform.CreateWindow(_width, _height);
        _impl.Resized += (w, h) => { _width = w; _height = h; };
        _impl.Closed += () => { IsClosed = true; OnClosed(); };
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
