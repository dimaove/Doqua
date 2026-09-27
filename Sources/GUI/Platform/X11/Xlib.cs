using System.Runtime.InteropServices;

namespace Doqua.GUI.Platform.X11;

/// <summary>
/// Xlib XEvent union (24 longs = 192 bytes on 64-bit). Only the fields Doqua uses are mapped.
/// XIDs (Window, Atom) are C "unsigned long", i.e. <see cref="nuint"/>.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct XEvent
{
    [FieldOffset(0)] public int type;

    // XAnyEvent: the window the event was reported relative to.
    [FieldOffset(32)] public nuint window;

    // XConfigureEvent
    [FieldOffset(56)] public int configureWidth;
    [FieldOffset(60)] public int configureHeight;

    // XClientMessageEvent
    [FieldOffset(40)] public nuint clientMessageType;
    [FieldOffset(48)] public int clientFormat;
    [FieldOffset(56)] public nint clientData0;

    // XButtonEvent, XMotionEvent and XCrossingEvent share the pointer position.
    [FieldOffset(64)] public int pointerX;
    [FieldOffset(68)] public int pointerY;

    // XButtonEvent
    [FieldOffset(84)] public uint button;

    // Modifier and button state of XKeyEvent, XButtonEvent and XMotionEvent.
    [FieldOffset(80)] public uint state;

    // XKeyEvent (same layout as XButtonEvent)
    [FieldOffset(84)] public uint keycode;

    // XFocusChangeEvent
    [FieldOffset(40)] public int focusMode;
    [FieldOffset(44)] public int focusDetail;

    // XCrossingEvent
    [FieldOffset(80)] public int crossingMode;
}

[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct XSelectionRequestEvent
{
    [FieldOffset(0)] public int type;
    [FieldOffset(32)] public nuint owner;
    [FieldOffset(40)] public nuint requestor;
    [FieldOffset(48)] public nuint selection;
    [FieldOffset(56)] public nuint target;
    [FieldOffset(64)] public nuint property;
    [FieldOffset(72)] public nuint time;
}

/// <summary>SelectionNotify: the answer to XConvertSelection.</summary>
[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct XSelectionEvent
{
    [FieldOffset(0)] public int type;
    [FieldOffset(16)] public int sendEvent;
    [FieldOffset(24)] public nint display;
    [FieldOffset(32)] public nuint requestor;
    [FieldOffset(40)] public nuint selection;
    [FieldOffset(48)] public nuint target;
    [FieldOffset(56)] public nuint property; // 0 (None) if the conversion failed.
    [FieldOffset(64)] public nuint time;
}

[StructLayout(LayoutKind.Explicit, Size = 192)]
internal struct XSelectionClearEvent
{
    [FieldOffset(0)] public int type;
    [FieldOffset(32)] public nuint window;
    [FieldOffset(40)] public nuint selection;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PollFd
{
    public int fd;
    public short events;
    public short revents;
}

/// <summary>Xlib XImage describing client-side pixels; initialized with XInitImage.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct XImage
{
    public int width;
    public int height;
    public int xoffset;
    public int format;
    public byte* data;
    public int byte_order;
    public int bitmap_unit;
    public int bitmap_bit_order;
    public int bitmap_pad;
    public int depth;
    public int bytes_per_line;
    public int bits_per_pixel;
    public nuint red_mask;
    public nuint green_mask;
    public nuint blue_mask;
    public nint obdata;
    public fixed long funcs[6]; // Function pointers filled in by XInitImage.
}

internal static unsafe partial class Xlib
{
    private const string Lib = "libX11.so.6";

    public const int KeyPress = 2;
    public const int ButtonPress = 4;
    public const int ButtonRelease = 5;
    public const int MotionNotify = 6;
    public const int LeaveNotify = 8;
    public const int FocusIn = 9;
    public const int FocusOut = 10;
    public const int Expose = 12;
    public const int ConfigureNotify = 22;
    public const int SelectionClear = 29;
    public const int SelectionRequest = 30;
    public const int SelectionNotify = 31;
    public const int ClientMessage = 33;

    public const nuint XA_ATOM = 4;
    public const nuint XA_STRING = 31;
    public const nuint XA_CARDINAL = 6;
    public const nuint AnyPropertyType = 0;
    public const nuint CurrentTime = 0;

    public const nint KeyPressMask = 1 << 0;
    public const nint KeyReleaseMask = 1 << 1;
    public const nint ButtonPressMask = 1 << 2;
    public const nint ButtonReleaseMask = 1 << 3;
    public const nint LeaveWindowMask = 1 << 5;
    public const nint PointerMotionMask = 1 << 6;
    public const nint ExposureMask = 1 << 15;
    public const nint FocusChangeMask = 1 << 21;
    public const nint StructureNotifyMask = 1 << 17;

    public const int NotifyGrab = 1;
    public const int NotifyUngrab = 2;
    public const int NotifyPointer = 5;

    public const uint ShiftMask = 1 << 0;
    public const uint ControlMask = 1 << 2;
    public const uint Mod1Mask = 1 << 3; // Alt

    public const nint XIMPreeditNothing = 0x0008;
    public const nint XIMStatusNothing = 0x0400;
    public const int XBufferOverflow = -1;

    public const int PropModeReplace = 0;

    public const int ZPixmap = 2;
    public const int LSBFirst = 0;
    public const int MSBFirst = 1;

    [LibraryImport(Lib)]
    public static partial int XInitThreads();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint XOpenDisplay(string? displayName);

    [LibraryImport(Lib)]
    public static partial int XDefaultScreen(nint display);

    [LibraryImport(Lib)]
    public static partial nuint XRootWindow(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial nuint XBlackPixel(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial int XDefaultDepth(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial nint XDefaultGC(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial nuint XCreateSimpleWindow(
        nint display, nuint parent, int x, int y, uint width, uint height,
        uint borderWidth, nuint border, nuint background);

    [LibraryImport(Lib)]
    public static partial int XDestroyWindow(nint display, nuint window);

    [LibraryImport(Lib)]
    public static partial int XMapWindow(nint display, nuint window);

    [LibraryImport(Lib)]
    public static partial int XResizeWindow(nint display, nuint window, uint width, uint height);

    [LibraryImport(Lib)]
    public static partial int XSelectInput(nint display, nuint window, nint eventMask);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int XStoreName(nint display, nuint window, string name);

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nuint XInternAtom(nint display, string atomName, int onlyIfExists);

    [LibraryImport(Lib)]
    public static partial int XSetWMProtocols(nint display, nuint window, nuint* protocols, int count);

    [LibraryImport(Lib)]
    public static partial int XChangeProperty(
        nint display, nuint window, nuint property, nuint type,
        int format, int mode, byte* data, int elementCount);

    [LibraryImport(Lib)]
    public static partial int XSetWindowBackgroundPixmap(nint display, nuint window, nuint pixmap);

    [LibraryImport(Lib)]
    public static partial int XInitImage(XImage* image);

    [LibraryImport(Lib)]
    public static partial int XPutImage(
        nint display, nuint drawable, nint gc, XImage* image,
        int srcX, int srcY, int destX, int destY, uint width, uint height);

    [LibraryImport(Lib)]
    public static partial int XPending(nint display);

    [LibraryImport(Lib)]
    public static partial int XNextEvent(nint display, XEvent* ev);

    [LibraryImport(Lib)]
    public static partial int XFlush(nint display);

    [LibraryImport(Lib)]
    public static partial int XDeleteProperty(nint display, nuint window, nuint property);

    [LibraryImport(Lib)]
    public static partial int XSetSelectionOwner(nint display, nuint selection, nuint owner, nuint time);

    [LibraryImport(Lib)]
    public static partial nuint XGetSelectionOwner(nint display, nuint selection);

    [LibraryImport(Lib)]
    public static partial int XConvertSelection(
        nint display, nuint selection, nuint target, nuint property, nuint requestor, nuint time);

    [LibraryImport(Lib)]
    public static partial int XGetWindowProperty(
        nint display, nuint window, nuint property, nint offset, nint length, int delete, nuint requestedType,
        nuint* actualType, int* actualFormat, nuint* itemCount, nuint* bytesAfter, byte** data);

    [LibraryImport(Lib)]
    public static partial int XSendEvent(nint display, nuint window, int propagate, nint eventMask, XEvent* ev);

    [LibraryImport(Lib)]
    public static partial int XCheckTypedWindowEvent(nint display, nuint window, int eventType, XEvent* ev);

    [LibraryImport(Lib)]
    public static partial int XConnectionNumber(nint display);

    [LibraryImport(Lib)]
    public static partial int XFree(void* data);

    [LibraryImport(Lib)]
    public static partial int XSupportsLocale();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint XSetLocaleModifiers(string modifiers);

    [LibraryImport(Lib)]
    public static partial nint XOpenIM(nint display, nint database, nint resourceName, nint resourceClass);

    /// <summary>
    /// XCreateIC is variadic (name/value pairs ending with NULL). This fixed signature matches the
    /// SysV x86-64 and AArch64 Linux calling conventions for integer and pointer arguments.
    /// </summary>
    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint XCreateIC(
        nint im, string name1, nint value1, string name2, nuint value2, string name3, nuint value3, nint end);

    [LibraryImport(Lib)]
    public static partial void XDestroyIC(nint ic);

    [LibraryImport(Lib)]
    public static partial void XSetICFocus(nint ic);

    [LibraryImport(Lib)]
    public static partial void XUnsetICFocus(nint ic);

    /// <summary>Lets the input method consume events (e.g. dead keys, IBus); true means skip the event.</summary>
    [LibraryImport(Lib)]
    public static partial int XFilterEvent(XEvent* ev, nuint window);

    [LibraryImport(Lib)]
    public static partial int Xutf8LookupString(nint ic, XEvent* ev, byte* buffer, int size, nuint* keysym, int* status);

    [LibraryImport(Lib)]
    public static partial int XLookupString(XEvent* ev, byte* buffer, int size, nuint* keysym, nint status);

    [LibraryImport(Lib)]
    public static partial nuint XkbKeycodeToKeysym(nint display, uint keycode, int group, int level);
}

internal static unsafe partial class LibC
{
    public const int LC_CTYPE = 0;
    public const short POLLIN = 1;

    [LibraryImport("libc.so.6")]
    public static partial int poll(PollFd* fds, nuint count, int timeoutMilliseconds);

    [LibraryImport("libc.so.6", StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint setlocale(int category, string locale);
}
