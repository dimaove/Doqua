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

    private nint _hwnd;

    public event Action? Closed;
    public event Action<int, int>? Resized;

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
                return 1; // The whole client area is filled in WM_PAINT.

            case User32.WM_PAINT:
            {
                PAINTSTRUCT ps;
                var hdc = User32.BeginPaint(_hwnd, &ps);
                User32.FillRect(hdc, &ps.rcPaint, Win32Platform.BackgroundBrush);
                User32.EndPaint(_hwnd, &ps);
                return 0;
            }

            case User32.WM_SIZE:
                Resized?.Invoke((int)(lParam & 0xFFFF), (int)((lParam >> 16) & 0xFFFF));
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

    // Note: an exception escaping an [UnmanagedCallersOnly] method terminates the process.
    [UnmanagedCallersOnly]
    internal static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (s_windows.TryGetValue(hwnd, out var window))
            return window.HandleMessage(msg, wParam, lParam);
        return User32.DefWindowProcW(hwnd, msg, wParam, lParam);
    }
}
