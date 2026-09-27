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

    public event Action? Closed;
    public event Action<int, int>? Resized;
    public event Action<MouseButton, int, int>? MouseDown;
    public event Action<MouseButton, int, int>? MouseUp;
    public event Action<int, int>? MouseMove;
    public event Action? MouseLeave;
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

            case User32.WM_MOUSEMOVE:
                OnMouseMove(lParam);
                return 0;

            case User32.WM_MOUSELEAVE:
                _trackingMouseLeave = false;
                MouseLeave?.Invoke();
                return 0;

            case User32.WM_LBUTTONDOWN:
                OnButtonDown(MouseButton.Left, lParam);
                return 0;
            case User32.WM_MBUTTONDOWN:
                OnButtonDown(MouseButton.Middle, lParam);
                return 0;
            case User32.WM_RBUTTONDOWN:
                OnButtonDown(MouseButton.Right, lParam);
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

    private void OnMouseMove(nint lParam)
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
        MouseMove?.Invoke(GetX(lParam), GetY(lParam));
    }

    private void OnButtonDown(MouseButton button, nint lParam)
    {
        // Capture the mouse so the release is delivered even outside the window.
        User32.SetCapture(_hwnd);
        MouseDown?.Invoke(button, GetX(lParam), GetY(lParam));
    }

    private void OnButtonUp(MouseButton button, nint wParam, nint lParam)
    {
        if ((wParam & (User32.MK_LBUTTON | User32.MK_MBUTTON | User32.MK_RBUTTON)) == 0)
            User32.ReleaseCapture();
        MouseUp?.Invoke(button, GetX(lParam), GetY(lParam));
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
