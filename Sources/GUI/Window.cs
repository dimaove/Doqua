using Doqua.GUI.Platform;

namespace Doqua.GUI;

/// <summary>
/// Top-level window decorated by the OS window manager. Hosts a single <see cref="Content"/>
/// control (usually a <see cref="Doqua.Controls.Panel"/>) that always fills the client area.
/// </summary>
public class Window
{
    private readonly IWindowImpl _impl;
    private string _title = "";
    private int _width = 800;
    private int _height = 600;
    private Color _background = Color.White;
    private Control? _content;
    private int _pressedButtons; // Bit mask of MouseButton values pressed inside the client area.
    private readonly Control?[] _pressedControls = new Control?[3]; // Indexed by MouseButton.

    public Window()
    {
        _impl = Application.Platform.CreateWindow(_width, _height);
        _impl.Resized += UpdateSize;
        _impl.Closed += () => { IsClosed = true; OnClosed(); };
        _impl.Paint += Render;
        _impl.MouseDown += HandleMouseDown;
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

    /// <summary>Color of the client area not covered by <see cref="Content"/>.</summary>
    public Color Background
    {
        get => _background;
        set
        {
            _background = value;
            Invalidate();
        }
    }

    /// <summary>Root control of the window. It is resized to fill the client area.</summary>
    public Control? Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(value, _content))
                return;
            if (value != null && (value.Parent != null || value.Host != null))
                throw new InvalidOperationException("The control already has a parent.");

            if (_content != null)
                _content.Host = null;
            _content = value;
            if (value != null)
            {
                value.Host = this;
                value.Bounds = new Rect(0, 0, _width, _height);
            }
            Invalidate();
        }
    }

    public bool IsClosed { get; private set; }

    public event EventHandler? Closed;

    /// <summary>
    /// Raised when a mouse button is pressed and released inside the client area,
    /// after the <see cref="Control.MouseClick"/> of the control under the pointer.
    /// </summary>
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

    /// <summary>Schedules a redraw. Multiple requests are merged into one.</summary>
    public void Invalidate()
    {
        if (!IsClosed)
            _impl.Invalidate();
    }

    protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

    protected virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);

    private void Render(Framebuffer framebuffer)
    {
        var dc = new DrawingContext(framebuffer);
        dc.Clear(_background);
        _content?.Render(dc);
    }

    private void HandleMouseDown(MouseButton button, int x, int y)
    {
        _pressedButtons |= 1 << (int)button;
        _pressedControls[(int)button] = _content?.HitTest(x, y, out _, out _);
    }

    private void HandleMouseUp(MouseButton button, int x, int y)
    {
        var mask = 1 << (int)button;
        var wasPressed = (_pressedButtons & mask) != 0;
        var pressedControl = _pressedControls[(int)button];
        _pressedButtons &= ~mask;
        _pressedControls[(int)button] = null;

        if (!wasPressed || x < 0 || y < 0 || x >= _width || y >= _height)
            return;

        // A control is clicked only if the button was pressed and released over it.
        if (pressedControl != null && _content != null)
        {
            var target = _content.HitTest(x, y, out var localX, out var localY);
            if (target == pressedControl)
                target.RaiseMouseClick(new MouseEventArgs(button, localX, localY));
        }

        OnMouseClick(new MouseEventArgs(button, x, y));
    }

    private void Resize(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ThrowIfClosed();
        UpdateSize(width, height);
        _impl.Resize(width, height);
    }

    private void UpdateSize(int width, int height)
    {
        _width = width;
        _height = height;
        if (_content != null)
            _content.Bounds = new Rect(0, 0, width, height);
    }

    private void ThrowIfClosed() => ObjectDisposedException.ThrowIf(IsClosed, this);
}
