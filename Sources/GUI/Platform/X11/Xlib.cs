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
}

internal static unsafe partial class Xlib
{
    private const string Lib = "libX11.so.6";

    public const int Expose = 12;
    public const int ConfigureNotify = 22;
    public const int ClientMessage = 33;

    public const nint ExposureMask = 1 << 15;
    public const nint StructureNotifyMask = 1 << 17;

    public const int PropModeReplace = 0;

    [LibraryImport(Lib)]
    public static partial int XInitThreads();

    [LibraryImport(Lib, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint XOpenDisplay(string? displayName);

    [LibraryImport(Lib)]
    public static partial int XDefaultScreen(nint display);

    [LibraryImport(Lib)]
    public static partial nuint XRootWindow(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial nuint XWhitePixel(nint display, int screen);

    [LibraryImport(Lib)]
    public static partial nuint XBlackPixel(nint display, int screen);

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
    public static partial int XNextEvent(nint display, XEvent* ev);

    [LibraryImport(Lib)]
    public static partial int XFlush(nint display);
}
