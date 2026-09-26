using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.Win32;

[SupportedOSPlatform("windows")]
internal sealed unsafe class Win32Platform : IPlatform
{
    internal const string WindowClassName = "DoquaWindow";

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
    }

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
}
