using System.Runtime.InteropServices;

namespace Doqua.GUI.Platform.Win32;

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct WNDCLASSEXW
{
    public uint cbSize;
    public uint style;
    public delegate* unmanaged<nint, uint, nint, nint, nint> lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public nint hInstance;
    public nint hIcon;
    public nint hCursor;
    public nint hbrBackground;
    public char* lpszMenuName;
    public char* lpszClassName;
    public nint hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int x;
    public int y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int left;
    public int top;
    public int right;
    public int bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd;
    public uint message;
    public nint wParam;
    public nint lParam;
    public uint time;
    public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PAINTSTRUCT
{
    public nint hdc;
    public int fErase;
    public RECT rcPaint;
    public int fRestore;
    public int fIncUpdate;
    public fixed byte rgbReserved[32];
}

// BOOL results are returned as int to avoid bool marshalling.
internal static unsafe partial class User32
{
    private const string Lib = "user32.dll";

    public const uint CS_VREDRAW = 0x0001;
    public const uint CS_HREDRAW = 0x0002;

    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const int SW_SHOWNORMAL = 1;

    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;

    // wParam flags of mouse messages: buttons that are still down.
    public const nint MK_LBUTTON = 0x0001;
    public const nint MK_RBUTTON = 0x0002;
    public const nint MK_MBUTTON = 0x0010;

    public const nint IDC_ARROW = 32512;

    [LibraryImport(Lib, SetLastError = true)]
    public static partial ushort RegisterClassExW(WNDCLASSEXW* wndClass);

    [LibraryImport(Lib, SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateWindowExW(
        uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [LibraryImport(Lib)]
    public static partial nint DefWindowProcW(nint hwnd, uint msg, nint wParam, nint lParam);

    [LibraryImport(Lib)]
    public static partial int DestroyWindow(nint hwnd);

    [LibraryImport(Lib)]
    public static partial int ShowWindow(nint hwnd, int cmdShow);

    [LibraryImport(Lib)]
    public static partial int UpdateWindow(nint hwnd);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf16)]
    public static partial int SetWindowTextW(nint hwnd, string text);

    [LibraryImport(Lib)]
    public static partial int SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport(Lib)]
    public static partial int AdjustWindowRectEx(RECT* rect, uint style, int menu, uint exStyle);

    [LibraryImport(Lib)]
    public static partial int GetMessageW(MSG* msg, nint hwnd, uint filterMin, uint filterMax);

    [LibraryImport(Lib)]
    public static partial int TranslateMessage(MSG* msg);

    [LibraryImport(Lib)]
    public static partial nint DispatchMessageW(MSG* msg);

    [LibraryImport(Lib)]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport(Lib)]
    public static partial nint SetCapture(nint hwnd);

    [LibraryImport(Lib)]
    public static partial int ReleaseCapture();

    [LibraryImport(Lib)]
    public static partial nint LoadCursorW(nint instance, nint cursorName);

    [LibraryImport(Lib)]
    public static partial nint BeginPaint(nint hwnd, PAINTSTRUCT* paint);

    [LibraryImport(Lib)]
    public static partial int EndPaint(nint hwnd, PAINTSTRUCT* paint);

    [LibraryImport(Lib)]
    public static partial int FillRect(nint hdc, RECT* rect, nint brush);
}

internal static partial class Gdi32
{
    [LibraryImport("gdi32.dll")]
    public static partial nint CreateSolidBrush(uint colorRef);
}

internal static partial class Kernel32
{
    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? moduleName);
}
