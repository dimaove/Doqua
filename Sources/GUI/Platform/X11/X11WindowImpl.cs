using System.Runtime.Versioning;
using System.Text;

namespace Doqua.GUI.Platform.X11;

[SupportedOSPlatform("linux")]
internal sealed unsafe class X11WindowImpl : IWindowImpl
{
    private readonly X11Platform _platform;
    private readonly Framebuffer _framebuffer = new();
    private readonly nint _inputContext;
    private bool _dirty = true;
    private int _width;
    private int _height;

    public event Action? Closed;
    public event Action<int, int>? Resized;
    public event Action<MouseButton, int, int, KeyModifiers>? MouseDown;
    public event Action<MouseButton, int, int, KeyModifiers>? MouseUp;
    public event Action<int, int, KeyModifiers>? MouseMove;
    public event Action? MouseLeave;
    public event Action<Key, KeyModifiers>? KeyDown;
    public event Action<string>? TextInput;
    public event Action<bool>? ActiveChanged;
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
            Xlib.PointerMotionMask | Xlib.LeaveWindowMask |
            Xlib.KeyPressMask | Xlib.KeyReleaseMask | Xlib.FocusChangeMask);

        if (platform.InputMethod != 0)
        {
            _inputContext = Xlib.XCreateIC(platform.InputMethod,
                "inputStyle", Xlib.XIMPreeditNothing | Xlib.XIMStatusNothing,
                "clientWindow", Handle,
                "focusWindow", Handle,
                0);
        }

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

    /// <summary>
    /// _NET_WM_ICON: for each size, width, height and then the pixels as non-premultiplied ARGB, one
    /// CARDINAL each. Format-32 properties hold C longs, so every value takes 64 bits on LP64.
    /// </summary>
    public void SetIcons(IReadOnlyList<Bitmap> icons)
    {
        if (icons.Count == 0)
        {
            Xlib.XDeleteProperty(_platform.Display, Handle, _platform.NetWmIcon);
            return;
        }

        var data = new nuint[icons.Sum(icon => 2 + icon.Width * icon.Height)];
        var index = 0;
        foreach (var icon in icons)
        {
            data[index++] = (nuint)icon.Width;
            data[index++] = (nuint)icon.Height;
            foreach (var pixel in icon.Pixels)
                data[index++] = pixel;
        }
        fixed (nuint* values = data)
        {
            Xlib.XChangeProperty(_platform.Display, Handle, _platform.NetWmIcon, Xlib.XA_CARDINAL, 32,
                Xlib.PropModeReplace, (byte*)values, data.Length);
        }
        Xlib.XFlush(_platform.Display);
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
        if (_inputContext != 0)
            Xlib.XDestroyIC(_inputContext);
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
                    MouseDown?.Invoke(pressed, ev.pointerX, ev.pointerY, ToModifiers(ev.state));
                break;

            case Xlib.ButtonRelease:
                if (ToMouseButton(ev.button) is { } released)
                    MouseUp?.Invoke(released, ev.pointerX, ev.pointerY, ToModifiers(ev.state));
                break;

            case Xlib.MotionNotify:
                MouseMove?.Invoke(ev.pointerX, ev.pointerY, ToModifiers(ev.state));
                break;

            // Leaving because another client grabbed the pointer (e.g. a window manager
            // shortcut) is not a real leave; motion events will resume afterwards.
            case Xlib.LeaveNotify:
                if (ev.crossingMode != Xlib.NotifyGrab)
                    MouseLeave?.Invoke();
                break;

            case Xlib.KeyPress:
                HandleKeyPress(ev);
                break;

            // Focus moving because of a keyboard grab (e.g. a window manager shortcut) or into
            // the window under the pointer is not a real activation change.
            case Xlib.FocusIn:
                if (ev.focusMode is not (Xlib.NotifyGrab or Xlib.NotifyUngrab) && ev.focusDetail != Xlib.NotifyPointer)
                {
                    if (_inputContext != 0)
                        Xlib.XSetICFocus(_inputContext);
                    ActiveChanged?.Invoke(true);
                }
                break;

            case Xlib.FocusOut:
                if (ev.focusMode is not (Xlib.NotifyGrab or Xlib.NotifyUngrab) && ev.focusDetail != Xlib.NotifyPointer)
                {
                    if (_inputContext != 0)
                        Xlib.XUnsetICFocus(_inputContext);
                    ActiveChanged?.Invoke(false);
                }
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

    private void HandleKeyPress(XEvent ev)
    {
        const int bufferSize = 64;
        var buffer = stackalloc byte[bufferSize];
        nuint keysym;
        string text;
        if (_inputContext != 0)
        {
            int status;
            var length = Xlib.Xutf8LookupString(_inputContext, &ev, buffer, bufferSize, &keysym, &status);
            text = status == Xlib.XBufferOverflow ? "" : Encoding.UTF8.GetString(buffer, length);
        }
        else
        {
            var length = Xlib.XLookupString(&ev, buffer, bufferSize, &keysym, 0);
            text = Encoding.Latin1.GetString(buffer, length);
        }

        // Keys come from the first layout, so shortcuts like Ctrl+A work with any active layout.
        var key = ToKey(Xlib.XkbKeycodeToKeysym(_platform.Display, ev.keycode, 0, 0));
        if (key != Key.None)
            KeyDown?.Invoke(key, ToModifiers(ev.state));

        // Backspace, Tab, Enter, Esc and Ctrl+letter produce control characters: they are keys, not text.
        if (text.Length > 0 && !text.Any(ch => ch < 0x20 || ch == 0x7F))
            TextInput?.Invoke(text);
    }

    private static KeyModifiers ToModifiers(uint state)
    {
        var modifiers = KeyModifiers.None;
        if ((state & Xlib.ShiftMask) != 0)
            modifiers |= KeyModifiers.Shift;
        if ((state & Xlib.ControlMask) != 0)
            modifiers |= KeyModifiers.Control;
        if ((state & Xlib.Mod1Mask) != 0)
            modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    private static Key ToKey(nuint keysym) => keysym switch
    {
        0xFF08 => Key.Backspace,
        0xFF09 or 0xFE20 => Key.Tab, // Tab, ISO_Left_Tab (Shift+Tab)
        0xFF0D or 0xFF8D => Key.Enter, // Return, KP_Enter
        0xFF1B => Key.Escape,
        0x0020 => Key.Space,
        0xFF55 or 0xFF9A => Key.PageUp,
        0xFF56 or 0xFF9B => Key.PageDown,
        0xFF57 or 0xFF9C => Key.End,
        0xFF50 or 0xFF95 => Key.Home,
        0xFF51 or 0xFF96 => Key.Left,
        0xFF52 or 0xFF97 => Key.Up,
        0xFF53 or 0xFF98 => Key.Right,
        0xFF54 or 0xFF99 => Key.Down,
        0xFF63 or 0xFF9E => Key.Insert,
        0xFFFF or 0xFF9F => Key.Delete,
        >= 0x30 and <= 0x39 => Key.D0 + (int)(keysym - 0x30),
        >= 0x61 and <= 0x7A => Key.A + (int)(keysym - 0x61),
        >= 0x41 and <= 0x5A => Key.A + (int)(keysym - 0x41),
        >= 0xFFBE and <= 0xFFC9 => Key.F1 + (int)(keysym - 0xFFBE),
        _ => Key.None,
    };

    // X11 buttons: 1 = left, 2 = middle, 3 = right, 4-7 = scroll wheel (not clicks).
    private static MouseButton? ToMouseButton(uint button) => button switch
    {
        1 => MouseButton.Left,
        2 => MouseButton.Middle,
        3 => MouseButton.Right,
        _ => null,
    };
}
