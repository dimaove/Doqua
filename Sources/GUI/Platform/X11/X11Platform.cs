using System.Runtime.Versioning;
using Doqua.GUI.Platform.FreeType;

namespace Doqua.GUI.Platform.X11;

[SupportedOSPlatform("linux")]
internal sealed unsafe class X11Platform : IPlatform
{
    private readonly Dictionary<nuint, X11WindowImpl> _windows = new();
    private FreeTypeFontBackend? _fonts;
    private X11Clipboard? _clipboard;
    private readonly Dictionary<Cursor, nuint> _cursors = new();
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
    internal nuint NetWmIcon { get; }

    /// <summary>X input method for text input, or 0 if none is available (Latin-1 fallback).</summary>
    internal nint InputMethod { get; }

    public X11Platform()
    {
        Xlib.XInitThreads();
        Display = Xlib.XOpenDisplay(null);
        if (Display == 0)
            throw new InvalidOperationException(Localization.Get("Doqua.Error.X11Display"));

        Screen = Xlib.XDefaultScreen(Display);

        // Framebuffer pixels are 0x00RRGGBB, which matches 24/32-bit TrueColor visuals.
        Depth = Xlib.XDefaultDepth(Display, Screen);
        if (Depth != 24 && Depth != 32)
            throw new PlatformNotSupportedException(Localization.Format("Doqua.Error.X11Depth", Depth));
        Gc = Xlib.XDefaultGC(Display, Screen);
        WmProtocols = Xlib.XInternAtom(Display, "WM_PROTOCOLS", 0);
        WmDeleteWindow = Xlib.XInternAtom(Display, "WM_DELETE_WINDOW", 0);
        NetWmName = Xlib.XInternAtom(Display, "_NET_WM_NAME", 0);
        Utf8String = Xlib.XInternAtom(Display, "UTF8_STRING", 0);
        NetWmIcon = Xlib.XInternAtom(Display, "_NET_WM_ICON", 0);
        InputMethod = OpenInputMethod(Display);
    }

    /// <summary>
    /// Opens the input method named by XMODIFIERS (e.g. IBus), falling back to the built-in one
    /// that still handles the keyboard layout and dead keys. Xlib needs the C locale set for this.
    /// </summary>
    private static nint OpenInputMethod(nint display)
    {
        LibC.setlocale(LibC.LC_CTYPE, "");
        if (Xlib.XSupportsLocale() == 0)
            return 0;

        Xlib.XSetLocaleModifiers("");
        var im = Xlib.XOpenIM(display, 0, 0, 0);
        if (im == 0)
        {
            Xlib.XSetLocaleModifiers("@im=none");
            im = Xlib.XOpenIM(display, 0, 0, 0);
        }
        return im;
    }

    public IFontBackend Fonts => _fonts ??= new FreeTypeFontBackend();

    /// <summary>
    /// X cursor for <paramref name="cursor"/>, created once. Core cursor-font shapes; Xlib shows them from the
    /// desktop's cursor theme when libXcursor is available.
    /// </summary>
    internal nuint GetCursor(Cursor cursor)
    {
        if (!_cursors.TryGetValue(cursor, out var handle))
        {
            uint shape = cursor switch // Values from X11/cursorfont.h.
            {
                Cursor.IBeam => 152,     // XC_xterm
                Cursor.Wait => 150,      // XC_watch
                Cursor.Crosshair => 34,  // XC_crosshair
                Cursor.Hand => 60,       // XC_hand2
                Cursor.SizeWE => 108,    // XC_sb_h_double_arrow
                Cursor.SizeNS => 116,    // XC_sb_v_double_arrow
                Cursor.SizeNWSE => 14,   // XC_bottom_right_corner
                Cursor.SizeNESW => 12,   // XC_bottom_left_corner
                Cursor.SizeAll => 52,    // XC_fleur
                Cursor.No => 0,          // XC_X_cursor
                _ => 68,                 // XC_left_ptr
            };
            handle = Xlib.XCreateFontCursor(Display, shape);
            _cursors[cursor] = handle;
        }
        return handle;
    }

    public IClipboard Clipboard => _clipboard ??= new X11Clipboard(this);

    // X11 has no system setting for these; the values match the GTK defaults.
    public int DoubleClickTime => 400;

    public int DoubleClickDistance => 5;

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
            TimerQueue.RunDue();
            if (!_running)
                break;

            // Draw invalidated windows once all queued events are handled, so that many
            // changes made by event handlers produce a single redraw. Then sleep until an
            // X event arrives or the next timer is due.
            if (Xlib.XPending(Display) == 0)
            {
                foreach (var dirty in _windows.Values.ToArray())
                    dirty.RenderIfDirty();

                // Sending a large image makes Xlib read incoming events into its own queue (so that the
                // connection cannot deadlock); poll() would not see those, so check the queue again first.
                if (Xlib.XPending(Display) != 0)
                    continue;

                var fd = new PollFd { fd = Xlib.XConnectionNumber(Display), events = LibC.POLLIN };
                LibC.poll(&fd, 1, TimerQueue.GetTimeout());
                continue;
            }

            Xlib.XNextEvent(Display, &ev); // An event is pending, so this does not block.
            if (InputMethod != 0 && Xlib.XFilterEvent(&ev, 0) != 0)
                continue;
            if (_clipboard != null && ev.window == _clipboard.Window)
                _clipboard.HandleEvent(&ev);
            else if (_windows.TryGetValue(ev.window, out var window))
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
