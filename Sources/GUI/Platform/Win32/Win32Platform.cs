using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.Win32;

[SupportedOSPlatform("windows")]
internal sealed unsafe class Win32Platform : IPlatform
{
    internal const string WindowClassName = "DoquaWindow";

    // Id of the thread timer that wakes the message loop for the next Doqua.GUI.Timer, or 0.
    private static nuint s_timerId;

    private GdiFontBackend? _fonts;
    private Win32Clipboard? _clipboard;

    internal static nint Instance { get; private set; }

    public Win32Platform()
    {
        Instance = Kernel32.GetModuleHandleW(null);

        fixed (char* className = WindowClassName)
        {
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                style = User32.CS_HREDRAW | User32.CS_VREDRAW,
                lpfnWndProc = &Win32WindowImpl.WndProc,
                hInstance = Instance,
                hCursor = User32.LoadCursorW(0, User32.IDC_ARROW),
                lpszClassName = className,
            };
            if (User32.RegisterClassExW(&wc) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        TimerQueue.Changed += ArmTimer;
        ArmTimer();
    }

    public IFontBackend Fonts => _fonts ??= new GdiFontBackend();

    public IClipboard Clipboard => _clipboard ??= new Win32Clipboard();

    public int DoubleClickTime => (int)User32.GetDoubleClickTime();

    // SM_CXDOUBLECLK is the width of the whole rectangle around the first click.
    public int DoubleClickDistance => User32.GetSystemMetrics(User32.SM_CXDOUBLECLK) / 2;

    public IWindowImpl CreateWindow(int width, int height) => new Win32WindowImpl(width, height);

    public int RunLoop()
    {
        MSG msg;
        // GetMessageW returns 0 on WM_QUIT and -1 on error.
        while (User32.GetMessageW(&msg, 0, 0, 0) > 0)
        {
            User32.TranslateMessage(&msg);
            User32.DispatchMessageW(&msg);
        }
        return (int)msg.wParam;
    }

    public void Quit(int exitCode) => User32.PostQuitMessage(exitCode);

    /// <summary>
    /// Points the thread timer at the next due Doqua timer. WM_TIMER is also dispatched by the
    /// modal loops Windows runs while a window is moved or resized, so timers keep firing then.
    /// </summary>
    private static void ArmTimer()
    {
        var timeout = TimerQueue.GetTimeout();
        if (timeout < 0)
        {
            if (s_timerId != 0)
                User32.KillTimer(0, s_timerId);
            s_timerId = 0;
            return;
        }
        // Passing the existing id replaces that timer instead of creating another one.
        s_timerId = User32.SetTimer(0, s_timerId, Math.Max((uint)timeout, User32.USER_TIMER_MINIMUM), &OnTimer);
    }

    // Note: an exception escaping an [UnmanagedCallersOnly] method terminates the process.
    [UnmanagedCallersOnly]
    private static void OnTimer(nint hwnd, uint message, nuint id, uint time)
    {
        TimerQueue.RunDue();
        ArmTimer();
    }
}
