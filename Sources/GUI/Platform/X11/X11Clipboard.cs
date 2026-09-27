using System.Runtime.Versioning;
using System.Text;

namespace Doqua.GUI.Platform.X11;

/// <summary>
/// The CLIPBOARD selection. X11 has no clipboard storage: the application that copied keeps the
/// data and sends it to others on request, so the text is lost when the app exits (unless a
/// desktop clipboard manager takes it over).
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class X11Clipboard : IClipboard
{
    private const int PasteTimeoutMilliseconds = 1000;

    private readonly nint _display;
    private readonly nuint _clipboard;
    private readonly nuint _targets;
    private readonly nuint _utf8String;
    private readonly nuint _text;
    private readonly nuint _incr;
    private readonly nuint _property; // Where owners put converted data for us.
    private string? _ownedText; // Our text while we own the selection.

    public X11Clipboard(X11Platform platform)
    {
        _display = platform.Display;
        _clipboard = Xlib.XInternAtom(_display, "CLIPBOARD", 0);
        _targets = Xlib.XInternAtom(_display, "TARGETS", 0);
        _utf8String = platform.Utf8String;
        _text = Xlib.XInternAtom(_display, "TEXT", 0);
        _incr = Xlib.XInternAtom(_display, "INCR", 0);
        _property = Xlib.XInternAtom(_display, "DOQUA_CLIPBOARD", 0);

        // Unmapped window used only to own the selection and receive the data; it outlives user windows.
        Window = Xlib.XCreateSimpleWindow(
            _display, Xlib.XRootWindow(_display, platform.Screen), 0, 0, 1, 1, 0, 0, 0);
    }

    internal nuint Window { get; }

    public bool SetText(string text)
    {
        _ownedText = text;
        Xlib.XSetSelectionOwner(_display, _clipboard, Window, Xlib.CurrentTime);
        if (Xlib.XGetSelectionOwner(_display, _clipboard) == Window)
            return true;
        _ownedText = null;
        return false;
    }

    public string? GetText()
    {
        if (_ownedText != null)
            return _ownedText; // No round trip to ourselves.
        return Request(_utf8String) ?? Request(Xlib.XA_STRING);
    }

    internal void HandleEvent(XEvent* ev)
    {
        switch (ev->type)
        {
            case Xlib.SelectionRequest:
                Answer((XSelectionRequestEvent*)ev);
                break;
            case Xlib.SelectionClear: // Another application copied something.
                if (((XSelectionClearEvent*)ev)->selection == _clipboard)
                    _ownedText = null;
                break;
        }
    }

    /// <summary>
    /// Asks the owner to convert the selection to <paramref name="target"/> and waits for its answer.
    /// Other events stay queued for the main loop.
    /// </summary>
    private string? Request(nuint target)
    {
        Xlib.XConvertSelection(_display, _clipboard, target, _property, Window, Xlib.CurrentTime);
        Xlib.XFlush(_display);

        var deadline = Environment.TickCount64 + PasteTimeoutMilliseconds;
        XEvent ev;
        while (Xlib.XCheckTypedWindowEvent(_display, Window, Xlib.SelectionNotify, &ev) == 0)
        {
            var remaining = deadline - Environment.TickCount64;
            if (remaining <= 0)
                return null;
            var fd = new PollFd { fd = Xlib.XConnectionNumber(_display), events = LibC.POLLIN };
            LibC.poll(&fd, 1, (int)Math.Min(remaining, 50));
        }

        var notify = (XSelectionEvent*)&ev;
        return notify->property == 0 ? null : ReadProperty();
    }

    private string? ReadProperty()
    {
        nuint type, count, remaining;
        int format;
        byte* data;
        if (Xlib.XGetWindowProperty(_display, Window, _property, 0, int.MaxValue / 4, 1, Xlib.AnyPropertyType,
                &type, &format, &count, &remaining, &data) != 0)
            return null;
        try
        {
            // INCR is the protocol for large data sent in chunks; not supported yet.
            if (data == null || type == _incr || format != 8)
                return null;
            var bytes = new ReadOnlySpan<byte>(data, (int)count);
            return type == Xlib.XA_STRING ? Encoding.Latin1.GetString(bytes) : Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            if (data != null)
                Xlib.XFree(data);
        }
    }

    /// <summary>Another application wants our text: store it on its window and notify it.</summary>
    private void Answer(XSelectionRequestEvent* request)
    {
        // Obsolete clients pass no property; the target name is used instead.
        var property = request->property != 0 ? request->property : request->target;
        var converted = _ownedText != null && request->selection == _clipboard
            && WriteProperty(request->requestor, property, request->target, _ownedText);

        XEvent reply = default;
        var notify = (XSelectionEvent*)&reply;
        notify->type = Xlib.SelectionNotify;
        notify->sendEvent = 1;
        notify->display = _display;
        notify->requestor = request->requestor;
        notify->selection = request->selection;
        notify->target = request->target;
        notify->property = converted ? property : 0;
        notify->time = request->time;
        Xlib.XSendEvent(_display, request->requestor, 0, 0, &reply);
        Xlib.XFlush(_display);
    }

    private bool WriteProperty(nuint window, nuint property, nuint target, string text)
    {
        if (target == _targets)
        {
            // Format-32 properties hold C longs, i.e. 64-bit values on LP64.
            var targets = stackalloc nuint[] { _targets, _utf8String, _text, Xlib.XA_STRING };
            Xlib.XChangeProperty(_display, window, property, Xlib.XA_ATOM, 32, Xlib.PropModeReplace, (byte*)targets, 4);
            return true;
        }

        byte[] bytes;
        nuint type;
        if (target == _utf8String || target == _text)
            (bytes, type) = (Encoding.UTF8.GetBytes(text), _utf8String);
        else if (target == Xlib.XA_STRING)
            (bytes, type) = (Encoding.Latin1.GetBytes(text), Xlib.XA_STRING);
        else
            return false;

        fixed (byte* data = bytes)
            Xlib.XChangeProperty(_display, window, property, type, 8, Xlib.PropModeReplace, data, bytes.Length);
        return true;
    }
}
