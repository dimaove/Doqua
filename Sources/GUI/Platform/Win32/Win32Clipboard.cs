using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Doqua.GUI.Platform.Win32;

[SupportedOSPlatform("windows")]
internal sealed unsafe class Win32Clipboard : IClipboard
{
    // SetClipboardData needs an owner window; a message-only window is never shown.
    private readonly nint _owner = User32.CreateWindowExW(
        0, Win32Platform.WindowClassName, "", 0, 0, 0, 0, 0, User32.HWND_MESSAGE, 0, Win32Platform.Instance, 0);

    public string? GetText()
    {
        if (!Open())
            return null;
        try
        {
            var memory = User32.GetClipboardData(User32.CF_UNICODETEXT);
            if (memory == 0)
                return null;
            var data = Kernel32.GlobalLock(memory);
            if (data == 0)
                return null;
            try
            {
                return Marshal.PtrToStringUni(data);
            }
            finally
            {
                Kernel32.GlobalUnlock(memory);
            }
        }
        finally
        {
            User32.CloseClipboard();
        }
    }

    public bool SetText(string text)
    {
        if (!Open())
            return false;
        try
        {
            User32.EmptyClipboard();

            var memory = Kernel32.GlobalAlloc(Kernel32.GMEM_MOVEABLE, (nuint)((text.Length + 1) * sizeof(char)));
            if (memory == 0)
                return false;
            var data = (char*)Kernel32.GlobalLock(memory);
            text.CopyTo(new Span<char>(data, text.Length));
            data[text.Length] = '\0';
            Kernel32.GlobalUnlock(memory);

            // On success the system owns the memory; otherwise it must be freed here.
            if (User32.SetClipboardData(User32.CF_UNICODETEXT, memory) != 0)
                return true;
            Kernel32.GlobalFree(memory);
            return false;
        }
        finally
        {
            User32.CloseClipboard();
        }
    }

    /// <summary>Another application may hold the clipboard open for a moment, so retry briefly.</summary>
    private bool Open()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (User32.OpenClipboard(_owner) != 0)
                return true;
            Thread.Sleep(10);
        }
        return false;
    }
}
