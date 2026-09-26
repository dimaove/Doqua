using System.Runtime.Versioning;
using System.Text;

namespace Doqua.GUI.Platform.X11;

[SupportedOSPlatform("linux")]
internal sealed unsafe class X11WindowImpl : IWindowImpl
{
    private readonly X11Platform _platform;
    private int _width;
    private int _height;

    public event Action? Closed;
    public event Action<int, int>? Resized;
    public event Action<MouseButton, int, int>? MouseDown;
    public event Action<MouseButton, int, int>? MouseUp;

    internal nuint Handle { get; private set; }

    public X11WindowImpl(X11Platform platform, int width, int height)
    {
        _platform = platform;
        _width = width;
        _height = height;

        var display = platform.Display;
        var screen = platform.Screen;

        // The X server fills the window with the background pixel on every Expose,
        // so an empty window needs no drawing code.
        Handle = Xlib.XCreateSimpleWindow(
            display, Xlib.XRootWindow(display, screen), 0, 0, (uint)width, (uint)height, 0,
            Xlib.XBlackPixel(display, screen), Xlib.XWhitePixel(display, screen));

        Xlib.XSelectInput(display, Handle,
            Xlib.ExposureMask | Xlib.StructureNotifyMask | Xlib.ButtonPressMask | Xlib.ButtonReleaseMask);

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
                // Background is cleared by the server; custom rendering will go here.
                break;

            case Xlib.ConfigureNotify:
                if (ev.configureWidth != _width || ev.configureHeight != _height)
                {
                    _width = ev.configureWidth;
                    _height = ev.configureHeight;
                    Resized?.Invoke(_width, _height);
                }
                break;

            // The X server grabs the pointer on press, so the release comes here even outside the window.
            case Xlib.ButtonPress:
                if (ToMouseButton(ev.button) is { } pressed)
                    MouseDown?.Invoke(pressed, ev.buttonX, ev.buttonY);
                break;

            case Xlib.ButtonRelease:
                if (ToMouseButton(ev.button) is { } released)
                    MouseUp?.Invoke(released, ev.buttonX, ev.buttonY);
                break;

            case Xlib.ClientMessage:
                if (ev.clientMessageType == _platform.WmProtocols && (nuint)ev.clientData0 == _platform.WmDeleteWindow)
                    Destroy();
                break;
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
