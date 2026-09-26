using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.X11;

[SupportedOSPlatform("linux")]
internal sealed unsafe class X11Platform : IPlatform
{
    private readonly Dictionary<nuint, X11WindowImpl> _windows = new();
    private bool _running;
    private int _exitCode;

    internal nint Display { get; }
    internal int Screen { get; }
    internal int Depth { get; }
    internal nint Gc { get; }

    internal nuint WmProtocols { get; }
    internal nuint WmDeleteWindow { get; }
    internal nuint NetWmName { get; }
    internal nuint Utf8String { get; }

    public X11Platform()
    {
        Xlib.XInitThreads();
        Display = Xlib.XOpenDisplay(null);
        if (Display == 0)
            throw new InvalidOperationException("Cannot open X11 display. Is the DISPLAY environment variable set?");

        Screen = Xlib.XDefaultScreen(Display);

        // Framebuffer pixels are 0x00RRGGBB, which matches 24/32-bit TrueColor visuals.
        Depth = Xlib.XDefaultDepth(Display, Screen);
        if (Depth != 24 && Depth != 32)
            throw new PlatformNotSupportedException($"X11 display depth {Depth} is not supported (24 or 32 required).");
        Gc = Xlib.XDefaultGC(Display, Screen);
        WmProtocols = Xlib.XInternAtom(Display, "WM_PROTOCOLS", 0);
        WmDeleteWindow = Xlib.XInternAtom(Display, "WM_DELETE_WINDOW", 0);
        NetWmName = Xlib.XInternAtom(Display, "_NET_WM_NAME", 0);
        Utf8String = Xlib.XInternAtom(Display, "UTF8_STRING", 0);
    }

    public IWindowImpl CreateWindow(int width, int height)
    {
        var window = new X11WindowImpl(this, width, height);
        _windows[window.Handle] = window;
        return window;
    }

    internal void Unregister(nuint handle) => _windows.Remove(handle);

    public int RunLoop()
    {
        _running = true;
        XEvent ev;
        while (_running)
        {
            // Draw invalidated windows once all queued events are handled, so that many
            // changes made by event handlers produce a single redraw.
            if (Xlib.XPending(Display) == 0)
            {
                foreach (var dirty in _windows.Values.ToArray())
                    dirty.RenderIfDirty();
                Xlib.XFlush(Display);
            }

            Xlib.XNextEvent(Display, &ev); // Blocks until an event arrives.
            if (_windows.TryGetValue(ev.window, out var window))
                window.HandleEvent(in ev);
        }
        return _exitCode;
    }

    public void Quit(int exitCode)
    {
        _exitCode = exitCode;
        _running = false;
    }
}
