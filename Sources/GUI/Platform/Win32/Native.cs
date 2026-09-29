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
internal struct MONITORINFO
{
    public uint cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
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

[StructLayout(LayoutKind.Sequential)]
internal struct BITMAPINFOHEADER
{
    public uint biSize;
    public int biWidth;
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TEXTMETRICW
{
    public int tmHeight;
    public int tmAscent;
    public int tmDescent;
    public int tmInternalLeading;
    public int tmExternalLeading;
    public int tmAveCharWidth;
    public int tmMaxCharWidth;
    public int tmWeight;
    public int tmOverhang;
    public int tmDigitizedAspectX;
    public int tmDigitizedAspectY;
    public char tmFirstChar;
    public char tmLastChar;
    public char tmDefaultChar;
    public char tmBreakChar;
    public byte tmItalic;
    public byte tmUnderlined;
    public byte tmStruckOut;
    public byte tmPitchAndFamily;
    public byte tmCharSet;
}

[StructLayout(LayoutKind.Sequential)]
internal struct GLYPHMETRICS
{
    public uint gmBlackBoxX;
    public uint gmBlackBoxY;
    public POINT gmptGlyphOrigin;
    public short gmCellIncX;
    public short gmCellIncY;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct LOGFONTW
{
    public int lfHeight;
    public int lfWidth;
    public int lfEscapement;
    public int lfOrientation;
    public int lfWeight;
    public byte lfItalic;
    public byte lfUnderline;
    public byte lfStrikeOut;
    public byte lfCharSet;
    public byte lfOutPrecision;
    public byte lfClipPrecision;
    public byte lfQuality;
    public byte lfPitchAndFamily;
    public fixed char lfFaceName[32];
}

/// <summary>16.16 fixed-point number.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FIXED
{
    public ushort fract;
    public short value;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MAT2
{
    public FIXED eM11;
    public FIXED eM12;
    public FIXED eM21;
    public FIXED eM22;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TRACKMOUSEEVENT
{
    public uint cbSize;
    public uint dwFlags;
    public nint hwndTrack;
    public uint dwHoverTime;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ICONINFO
{
    public int fIcon;
    public uint xHotspot;
    public uint yHotspot;
    public nint hbmMask;
    public nint hbmColor;
}

// BOOL results are returned as int to avoid bool marshalling.
internal static unsafe partial class User32
{
    private const string Lib = "user32.dll";

    public const uint CS_VREDRAW = 0x0001;
    public const uint CS_HREDRAW = 0x0002;

    public const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    public const uint WS_THICKFRAME = 0x00040000;
    public const uint WS_MAXIMIZEBOX = 0x00010000;
    public const int GWL_STYLE = -16;
    public const int GWLP_HWNDPARENT = -8;
    public const uint MONITOR_DEFAULTTONEAREST = 2;
    public const int CW_USEDEFAULT = unchecked((int)0x80000000);
    public const int SW_SHOWNORMAL = 1;

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    public const uint WM_DESTROY = 0x0002;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_APP = 0x8000;
    public const uint WM_SIZE = 0x0005;
    public const uint WM_SETFOCUS = 0x0007;
    public const uint WM_KILLFOCUS = 0x0008;
    public const uint WM_PAINT = 0x000F;
    public const uint WM_SETCURSOR = 0x0020;
    public const nint HTCLIENT = 1;
    public const uint WM_ERASEBKGND = 0x0014;
    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSCHAR = 0x0106;
    public const uint WM_CHAR = 0x0102;
    public const uint WM_MOUSEMOVE = 0x0200;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_MOUSELEAVE = 0x02A3;
    public const uint WM_MOUSEWHEEL = 0x020A;
    public const int WHEEL_DELTA = 120;

    public const uint TME_LEAVE = 0x00000002;

    public const int VK_SHIFT = 0x10;
    public const int VK_CONTROL = 0x11;
    public const int VK_MENU = 0x12; // Alt

    // wParam flags of mouse messages: buttons that are still down.
    public const nint MK_LBUTTON = 0x0001;
    public const nint MK_RBUTTON = 0x0002;
    public const nint MK_MBUTTON = 0x0010;
    public const nint MK_SHIFT = 0x0004;
    public const nint MK_CONTROL = 0x0008;

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
    public static partial int GetWindowRect(nint hwnd, RECT* rect);

    [LibraryImport(Lib)]
    public static partial nint SetWindowLongPtrW(nint hwnd, int index, nint value);

    [LibraryImport(Lib)]
    public static partial int EnableWindow(nint hwnd, int enable);

    [LibraryImport(Lib)]
    public static partial int PostMessageW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport(Lib)]
    public static partial int SetForegroundWindow(nint hwnd);

    [LibraryImport(Lib)]
    public static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport(Lib)]
    public static partial int GetMonitorInfoW(nint monitor, MONITORINFO* info);

    [LibraryImport(Lib)]
    public static partial int GetMessageW(MSG* msg, nint hwnd, uint filterMin, uint filterMax);

    [LibraryImport(Lib)]
    public static partial int TranslateMessage(MSG* msg);

    [LibraryImport(Lib)]
    public static partial nint DispatchMessageW(MSG* msg);

    [LibraryImport(Lib)]
    public static partial void PostQuitMessage(int exitCode);

    [LibraryImport(Lib)]
    public static partial short GetKeyState(int virtualKey);

    [LibraryImport(Lib)]
    public static partial int ScreenToClient(nint hwnd, POINT* point);

    public const int SM_CXDOUBLECLK = 36;

    [LibraryImport(Lib)]
    public static partial uint GetDoubleClickTime();

    public const uint WM_SETICON = 0x0080;
    public const nint ICON_SMALL = 0;
    public const nint ICON_BIG = 1;
    public const int SM_CXICON = 11;
    public const int SM_CXSMICON = 49;

    [LibraryImport(Lib)]
    public static partial nint SendMessageW(nint hwnd, uint message, nint wParam, nint lParam);

    [LibraryImport(Lib)]
    public static partial nint CreateIconIndirect(ICONINFO* iconInfo);

    [LibraryImport(Lib)]
    public static partial int DestroyIcon(nint icon);

    public const uint USER_TIMER_MINIMUM = 10;

    [LibraryImport(Lib)]
    public static partial nuint SetTimer(nint hwnd, nuint id, uint milliseconds, delegate* unmanaged<nint, uint, nuint, uint, void> callback);

    [LibraryImport(Lib)]
    public static partial int KillTimer(nint hwnd, nuint id);

    [LibraryImport(Lib)]
    public static partial int GetSystemMetrics(int index);

    public const nint HWND_MESSAGE = -3;
    public const uint CF_UNICODETEXT = 13;

    [LibraryImport(Lib)]
    public static partial int OpenClipboard(nint owner);

    [LibraryImport(Lib)]
    public static partial int CloseClipboard();

    [LibraryImport(Lib)]
    public static partial int EmptyClipboard();

    [LibraryImport(Lib)]
    public static partial nint GetClipboardData(uint format);

    [LibraryImport(Lib)]
    public static partial nint SetClipboardData(uint format, nint memory);

    [LibraryImport(Lib)]
    public static partial int TrackMouseEvent(TRACKMOUSEEVENT* eventTrack);

    [LibraryImport(Lib)]
    public static partial nint SetCapture(nint hwnd);

    [LibraryImport(Lib)]
    public static partial int ReleaseCapture();

    [LibraryImport(Lib)]
    public static partial nint LoadCursorW(nint instance, nint cursorName);

    [LibraryImport(Lib)]
    public static partial nint SetCursor(nint cursor);

    [LibraryImport(Lib)]
    public static partial nint BeginPaint(nint hwnd, PAINTSTRUCT* paint);

    [LibraryImport(Lib)]
    public static partial int EndPaint(nint hwnd, PAINTSTRUCT* paint);

    [LibraryImport(Lib)]
    public static partial int GetClientRect(nint hwnd, RECT* rect);

    [LibraryImport(Lib)]
    public static partial int InvalidateRect(nint hwnd, RECT* rect, int erase);
}

internal static unsafe partial class Gdi32
{
    public const uint BI_RGB = 0;
    public const uint DIB_RGB_COLORS = 0;

    public const int FW_NORMAL = 400;
    public const int FW_BOLD = 700;
    public const uint DEFAULT_CHARSET = 1;
    public const uint OUT_TT_PRECIS = 4;
    public const uint CLIP_DEFAULT_PRECIS = 0;
    public const uint ANTIALIASED_QUALITY = 4;
    public const uint DEFAULT_PITCH = 0;

    public const uint GGO_GRAY8_BITMAP = 6;
    public const uint GDI_ERROR = 0xFFFFFFFF;

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateDIBSection(nint hdc, BITMAPINFOHEADER* header, uint usage, void** bits, nint section, uint offset);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateBitmap(int width, int height, uint planes, uint bitsPerPixel, void* bits);

    [LibraryImport("gdi32.dll")]
    public static partial int DeleteObject(nint obj);

    [LibraryImport("gdi32.dll")]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    public static partial int DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateFontW(
        int height, int width, int escapement, int orientation, int weight,
        uint italic, uint underline, uint strikeOut, uint charSet, uint outPrecision,
        uint clipPrecision, uint quality, uint pitchAndFamily, string faceName);

    [LibraryImport("gdi32.dll")]
    public static partial nint SelectObject(nint hdc, nint obj);

    /// <summary>Calls <paramref name="callback"/>(LOGFONTW*, TEXTMETRICW*, font type, lParam) for each font; it returns nonzero to continue.</summary>
    [LibraryImport("gdi32.dll")]
    public static partial int EnumFontFamiliesExW(nint hdc, LOGFONTW* logFont,
        delegate* unmanaged<LOGFONTW*, void*, uint, nint, int> callback, nint lParam, uint flags);

    [LibraryImport("gdi32.dll")]
    public static partial int GetTextMetricsW(nint hdc, TEXTMETRICW* metrics);

    [LibraryImport("gdi32.dll")]
    public static partial uint GetGlyphOutlineW(
        nint hdc, uint ch, uint format, GLYPHMETRICS* metrics, uint bufferSize, void* buffer, MAT2* matrix);

    [LibraryImport("gdi32.dll")]
    public static partial int SetDIBitsToDevice(
        nint hdc, int xDest, int yDest, uint width, uint height, int xSrc, int ySrc,
        uint startScan, uint scanLines, void* bits, BITMAPINFOHEADER* bitmapInfo, uint colorUse);
}

internal static partial class Kernel32
{
    public const uint GMEM_MOVEABLE = 0x0002;

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalFree(nint memory);

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    public static partial int GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint GetModuleHandleW(string? moduleName);
}
