using System.Runtime.Versioning;
using System.Text;

namespace Doqua.GUI.Platform.X11;

[SupportedOSPlatform("linux")]
internal sealed unsafe class X11WindowImpl : IWindowImpl
{
    private readonly X11Platform _platform;
    private readonly Framebuffer _framebuffer = new();
    private bool _dirty = true;
    private int _width;
    private int _height;

    public event Action? Closed;
    public event Action<int, int>? Resized;
    public event Action<MouseButton, int, int>? MouseDown;
    public event Action<MouseButton, int, int>? MouseUp;
    public event Action<int, int>? MouseMove;
    public event Action? MouseLeave;
    public event Action<Framebuffer>? Paint;

    internal nuint Handle { get; private set; }

    public X11WindowImpl(X11Platform platform, int width, int height)
    {
        _platform = platform;
        _width = width;
        _height = height;

        var display = platform.Display;
        var screen = platform.Screen;

        var black = Xlib.XBlackPixel(display, screen);
        Handle = Xlib.XCreateSimpleWindow(
            display, Xlib.XRootWindow(display, screen), 0, 0, (uint)width, (uint)height, 0, black, black);

        // No server-side background: the whole window is drawn from the framebuffer,
        // so the server clearing it first would only cause flicker.
        Xlib.XSetWindowBackgroundPixmap(display, Handle, 0);

        Xlib.XSelectInput(display, Handle,
            Xlib.ExposureMask | Xlib.StructureNotifyMask | Xlib.ButtonPressMask | Xlib.ButtonReleaseMask |
            Xlib.PointerMotionMask | Xlib.LeaveWindowMask);

        // Ask the window manager to send WM_DELETE_WINDOW instead of killing the connection
        // when the user clicks the close button.
        var deleteWindow = platform.WmDeleteWindow;
        Xlib.XSetWMProtocols(display, Handle, &deleteWindow, 1);
    }

    public void SetTitle(string title)
    {
        // WM_NAME for legacy window managers, _NET_WM_NAME for proper UTF-8 titles.
        Xlib.XStoreName(_platform.Display, Handle, title);
        var bytes = Encoding.UTF8.GetBytes(title);
        fixed (byte* data = bytes)
        {
            Xlib.XChangeProperty(_platform.Display, Handle, _platform.NetWmName, _platform.Utf8String,
                8, Xlib.PropModeReplace, data, bytes.Length);
        }
    }

    public void Resize(int width, int height) =>
        Xlib.XResizeWindow(_platform.Display, Handle, (uint)width, (uint)height);

    public void Show()
    {
        Xlib.XMapWindow(_platform.Display, Handle);
        Xlib.XFlush(_platform.Display);
    }

    public void Invalidate() => _dirty = true;

    public void Destroy()
    {
        if (Handle == 0)
            return;
        _platform.Unregister(Handle);
        Xlib.XDestroyWindow(_platform.Display, Handle);
        Xlib.XFlush(_platform.Display);
        Handle = 0;
        Closed?.Invoke();
    }

    internal void HandleEvent(in XEvent ev)
    {
        switch (ev.type)
        {
            case Xlib.Expose:
                _dirty = true;
                break;

            case Xlib.ConfigureNotify:
                if (ev.configureWidth != _width || ev.configureHeight != _height)
                {
                    _width = ev.configureWidth;
                    _height = ev.configureHeight;
                    _dirty = true;
                    Resized?.Invoke(_width, _height);
                }
                break;

            // The X server grabs the pointer on press, so the release comes here even outside the window.
            case Xlib.ButtonPress:
                if (ToMouseButton(ev.button) is { } pressed)
                    MouseDown?.Invoke(pressed, ev.pointerX, ev.pointerY);
                break;

            case Xlib.ButtonRelease:
                if (ToMouseButton(ev.button) is { } released)
                    MouseUp?.Invoke(released, ev.pointerX, ev.pointerY);
                break;

            case Xlib.MotionNotify:
                MouseMove?.Invoke(ev.pointerX, ev.pointerY);
                break;

            // Leaving because another client grabbed the pointer (e.g. a window manager
            // shortcut) is not a real leave; motion events will resume afterwards.
            case Xlib.LeaveNotify:
                if (ev.crossingMode != Xlib.NotifyGrab)
                    MouseLeave?.Invoke();
                break;

            case Xlib.ClientMessage:
                if (ev.clientMessageType == _platform.WmProtocols && (nuint)ev.clientData0 == _platform.WmDeleteWindow)
                    Destroy();
                break;
        }
    }

    /// <summary>Called by the event loop when the queue is empty.</summary>
    internal void RenderIfDirty()
    {
        if (!_dirty || Handle == 0 || _width <= 0 || _height <= 0)
            return;
        _dirty = false;

        _framebuffer.Resize(_width, _height);
        Paint?.Invoke(_framebuffer);

        fixed (uint* pixels = _framebuffer.Pixels)
        {
            var byteOrder = BitConverter.IsLittleEndian ? Xlib.LSBFirst : Xlib.MSBFirst;
            var image = new XImage
            {
                width = _width,
                height = _height,
                format = Xlib.ZPixmap,
                data = (byte*)pixels,
                byte_order = byteOrder,
                bitmap_unit = 32,
                bitmap_bit_order = byteOrder,
                bitmap_pad = 32,
                depth = _platform.Depth,
                bytes_per_line = _width * 4,
                bits_per_pixel = 32,
                red_mask = 0xFF0000,
                green_mask = 0x00FF00,
                blue_mask = 0x0000FF,
            };
            Xlib.XInitImage(&image);
            Xlib.XPutImage(_platform.Display, Handle, _platform.Gc, &image, 0, 0, 0, 0, (uint)_width, (uint)_height);
        }
    }

    // X11 buttons: 1 = left, 2 = middle, 3 = right, 4-7 = scroll wheel (not clicks).
    private static MouseButton? ToMouseButton(uint button) => button switch
    {
        1 => MouseButton.Left,
        2 => MouseButton.Middle,
        3 => MouseButton.Right,
        _ => null,
    };
}
