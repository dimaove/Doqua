using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.Win32;

[SupportedOSPlatform("windows")]
internal sealed unsafe class Win32WindowImpl : IWindowImpl
{
    private const uint Style = User32.WS_OVERLAPPEDWINDOW;

    // Messages sent during CreateWindowExW (before the handle is known) go to DefWindowProcW.
    private static readonly Dictionary<nint, Win32WindowImpl> s_windows = new();

    private readonly Framebuffer _framebuffer = new();
    private nint _hwnd;
    private bool _trackingMouseLeave;
    private nint _smallIcon;
    private nint _bigIcon;
    private char _pendingHighSurrogate;

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

    public Win32WindowImpl(int width, int height)
    {
        var (outerWidth, outerHeight) = ToOuterSize(width, height);
        _hwnd = User32.CreateWindowExW(
            0, Win32Platform.WindowClassName, "", Style,
            User32.CW_USEDEFAULT, User32.CW_USEDEFAULT, outerWidth, outerHeight,
            0, 0, Win32Platform.Instance, 0);
        if (_hwnd == 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        s_windows[_hwnd] = this;
    }

    public void SetTitle(string title) => User32.SetWindowTextW(_hwnd, title);

    /// <summary>Title bar (small) and Alt+Tab / taskbar (big) icons from the closest sizes.</summary>
    public void SetIcons(IReadOnlyList<Bitmap> icons)
    {
        var small = icons.Count == 0 ? 0 : CreateIcon(Closest(icons, User32.GetSystemMetrics(User32.SM_CXSMICON)));
        var big = icons.Count == 0 ? 0 : CreateIcon(Closest(icons, User32.GetSystemMetrics(User32.SM_CXICON)));
        User32.SendMessageW(_hwnd, User32.WM_SETICON, User32.ICON_SMALL, small);
        User32.SendMessageW(_hwnd, User32.WM_SETICON, User32.ICON_BIG, big);
        DestroyIcons();
        (_smallIcon, _bigIcon) = (small, big);
    }

    private static Bitmap Closest(IReadOnlyList<Bitmap> icons, int size) =>
        icons.MinBy(icon => Math.Abs(icon.Width - size))!;

    /// <summary>Icon from a 32-bit top-down DIB; icons use straight (non-premultiplied) alpha like Bitmap.</summary>
    private static nint CreateIcon(Bitmap bitmap)
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)sizeof(BITMAPINFOHEADER),
            biWidth = bitmap.Width,
            biHeight = -bitmap.Height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = Gdi32.BI_RGB,
        };
        void* bits;
        var color = Gdi32.CreateDIBSection(0, &header, Gdi32.DIB_RGB_COLORS, &bits, 0, 0);
        if (color == 0)
            return 0;
        bitmap.Pixels.CopyTo(new Span<uint>(bits, bitmap.Pixels.Length)); // 0xAARRGGBB = BGRA in memory.

        // The mask is required but ignored when the color bitmap has alpha.
        var mask = Gdi32.CreateBitmap(bitmap.Width, bitmap.Height, 1, 1, null);
        var info = new ICONINFO { fIcon = 1, hbmMask = mask, hbmColor = color };
        var icon = User32.CreateIconIndirect(&info);
        Gdi32.DeleteObject(color); // CreateIconIndirect copies both bitmaps.
        Gdi32.DeleteObject(mask);
        return icon;
    }

    private void DestroyIcons()
    {
        if (_smallIcon != 0)
            User32.DestroyIcon(_smallIcon);
        if (_bigIcon != 0)
            User32.DestroyIcon(_bigIcon);
        (_smallIcon, _bigIcon) = (0, 0);
    }

    public void Resize(int width, int height)
    {
        var (outerWidth, outerHeight) = ToOuterSize(width, height);
        User32.SetWindowPos(_hwnd, 0, 0, 0, outerWidth, outerHeight,
            User32.SWP_NOMOVE | User32.SWP_NOZORDER | User32.SWP_NOACTIVATE);
    }

    public void Show()
    {
        User32.ShowWindow(_hwnd, User32.SW_SHOWNORMAL);
        User32.UpdateWindow(_hwnd);
    }

    public void Invalidate()
    {
        if (_hwnd != 0)
            User32.InvalidateRect(_hwnd, null, 0);
    }

    public void Destroy()
    {
        if (_hwnd != 0)
            User32.DestroyWindow(_hwnd); // WM_DESTROY raises Closed.
    }

    /// <summary>Converts a client-area size into the full window size including frame and title bar.</summary>
    private static (int Width, int Height) ToOuterSize(int width, int height)
    {
        var rect = new RECT { right = width, bottom = height };
        User32.AdjustWindowRectEx(&rect, Style, 0, 0);
        return (rect.right - rect.left, rect.bottom - rect.top);
    }

    private nint HandleMessage(uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case User32.WM_ERASEBKGND:
                return 1; // The whole client area is drawn in WM_PAINT.

            case User32.WM_PAINT:
                OnPaint();
                return 0;

            case User32.WM_SIZE:
                Resized?.Invoke((int)(lParam & 0xFFFF), (int)((lParam >> 16) & 0xFFFF));
                return 0;

            // WM_SYSKEYDOWN (Alt combinations, F10) is left to DefWindowProcW so Alt+F4 keeps working.
            case User32.WM_KEYDOWN:
                var key = ToKey((int)wParam);
                if (key != Key.None)
                    KeyDown?.Invoke(key, GetModifiers());
                return 0;

            // Produced from WM_KEYDOWN by TranslateMessage in the message loop, using the keyboard layout.
            case User32.WM_CHAR:
                OnChar((char)wParam);
                return 0;

            case User32.WM_SETFOCUS:
                ActiveChanged?.Invoke(true);
                return 0;

            case User32.WM_KILLFOCUS:
                ActiveChanged?.Invoke(false);
                return 0;

            case User32.WM_MOUSEMOVE:
                OnMouseMove(wParam, lParam);
                return 0;

            case User32.WM_MOUSELEAVE:
                _trackingMouseLeave = false;
                MouseLeave?.Invoke();
                return 0;

            case User32.WM_LBUTTONDOWN:
                OnButtonDown(MouseButton.Left, wParam, lParam);
                return 0;
            case User32.WM_MBUTTONDOWN:
                OnButtonDown(MouseButton.Middle, wParam, lParam);
                return 0;
            case User32.WM_RBUTTONDOWN:
                OnButtonDown(MouseButton.Right, wParam, lParam);
                return 0;

            case User32.WM_LBUTTONUP:
                OnButtonUp(MouseButton.Left, wParam, lParam);
                return 0;
            case User32.WM_MBUTTONUP:
                OnButtonUp(MouseButton.Middle, wParam, lParam);
                return 0;
            case User32.WM_RBUTTONUP:
                OnButtonUp(MouseButton.Right, wParam, lParam);
                return 0;

            case User32.WM_DESTROY:
                DestroyIcons();
                s_windows.Remove(_hwnd);
                _hwnd = 0;
                Closed?.Invoke();
                return 0;
        }
        // WM_CLOSE (title bar X button) is handled by DefWindowProcW, which calls DestroyWindow.
        return User32.DefWindowProcW(_hwnd, msg, wParam, lParam);
    }

    private void OnPaint()
    {
        PAINTSTRUCT ps;
        var hdc = User32.BeginPaint(_hwnd, &ps);

        RECT client;
        User32.GetClientRect(_hwnd, &client);
        int width = client.right, height = client.bottom;
        if (width > 0 && height > 0) // 0 x 0 when minimized.
        {
            _framebuffer.Resize(width, height);
            Paint?.Invoke(_framebuffer);

            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height, // Negative height: rows go top to bottom.
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Gdi32.BI_RGB,
            };
            fixed (uint* bits = _framebuffer.Pixels)
            {
                Gdi32.SetDIBitsToDevice(hdc, 0, 0, (uint)width, (uint)height, 0, 0,
                    0, (uint)height, bits, &header, Gdi32.DIB_RGB_COLORS);
            }
        }

        User32.EndPaint(_hwnd, &ps);
    }

    private void OnChar(char ch)
    {
        // Characters outside the BMP arrive as two WM_CHAR messages (a surrogate pair).
        if (char.IsHighSurrogate(ch))
        {
            _pendingHighSurrogate = ch;
            return;
        }

        string text;
        if (char.IsLowSurrogate(ch) && _pendingHighSurrogate != 0)
            text = new string([_pendingHighSurrogate, ch]);
        else if (ch < 0x20 || ch == 0x7F) // Backspace, Tab, Enter, Esc, Ctrl+letter: handled as keys.
            text = "";
        else
            text = ch.ToString();
        _pendingHighSurrogate = '\0';

        if (text.Length > 0)
            TextInput?.Invoke(text);
    }

    private static KeyModifiers GetModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (User32.GetKeyState(User32.VK_SHIFT) < 0)
            modifiers |= KeyModifiers.Shift;
        if (User32.GetKeyState(User32.VK_CONTROL) < 0)
            modifiers |= KeyModifiers.Control;
        if (User32.GetKeyState(User32.VK_MENU) < 0)
            modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    /// <summary>Maps a Win32 virtual-key code; letter and digit codes do not depend on the layout.</summary>
    private static Key ToKey(int vk) => vk switch
    {
        0x08 => Key.Backspace,
        0x09 => Key.Tab,
        0x0D => Key.Enter,
        0x1B => Key.Escape,
        0x20 => Key.Space,
        0x21 => Key.PageUp,
        0x22 => Key.PageDown,
        0x23 => Key.End,
        0x24 => Key.Home,
        0x25 => Key.Left,
        0x26 => Key.Up,
        0x27 => Key.Right,
        0x28 => Key.Down,
        0x2D => Key.Insert,
        0x2E => Key.Delete,
        >= 0x30 and <= 0x39 => Key.D0 + (vk - 0x30),
        >= 0x41 and <= 0x5A => Key.A + (vk - 0x41),
        >= 0x70 and <= 0x7B => Key.F1 + (vk - 0x70),
        _ => Key.None,
    };

    private void OnMouseMove(nint wParam, nint lParam)
    {
        // WM_MOUSELEAVE is sent only once per TrackMouseEvent call, so re-arm it after each leave.
        if (!_trackingMouseLeave)
        {
            var track = new TRACKMOUSEEVENT
            {
                cbSize = (uint)sizeof(TRACKMOUSEEVENT),
                dwFlags = User32.TME_LEAVE,
                hwndTrack = _hwnd,
            };
            _trackingMouseLeave = User32.TrackMouseEvent(&track) != 0;
        }
        MouseMove?.Invoke(GetX(lParam), GetY(lParam), GetMouseModifiers(wParam));
    }

    private void OnButtonDown(MouseButton button, nint wParam, nint lParam)
    {
        // Capture the mouse so the release is delivered even outside the window.
        User32.SetCapture(_hwnd);
        MouseDown?.Invoke(button, GetX(lParam), GetY(lParam), GetMouseModifiers(wParam));
    }

    private void OnButtonUp(MouseButton button, nint wParam, nint lParam)
    {
        if ((wParam & (User32.MK_LBUTTON | User32.MK_MBUTTON | User32.MK_RBUTTON)) == 0)
            User32.ReleaseCapture();
        MouseUp?.Invoke(button, GetX(lParam), GetY(lParam), GetMouseModifiers(wParam));
    }

    /// <summary>Mouse messages carry Shift and Ctrl in wParam; Alt has to be queried.</summary>
    private static KeyModifiers GetMouseModifiers(nint wParam)
    {
        var modifiers = KeyModifiers.None;
        if ((wParam & User32.MK_SHIFT) != 0)
            modifiers |= KeyModifiers.Shift;
        if ((wParam & User32.MK_CONTROL) != 0)
            modifiers |= KeyModifiers.Control;
        if (User32.GetKeyState(User32.VK_MENU) < 0)
            modifiers |= KeyModifiers.Alt;
        return modifiers;
    }

    // Coordinates are signed: they are negative when captured outside the client area.
    private static int GetX(nint lParam) => (short)(lParam & 0xFFFF);
    private static int GetY(nint lParam) => (short)((lParam >> 16) & 0xFFFF);

    // Note: an exception escaping an [UnmanagedCallersOnly] method terminates the process.
    [UnmanagedCallersOnly]
    internal static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (s_windows.TryGetValue(hwnd, out var window))
            return window.HandleMessage(msg, wParam, lParam);
        return User32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
