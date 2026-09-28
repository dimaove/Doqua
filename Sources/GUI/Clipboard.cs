namespace Doqua.GUI;

/// <summary>System clipboard (text only). Call from the GUI thread (see <see cref="Application.Post"/>).</summary>
public static class Clipboard
{
    /// <summary>Returns the clipboard text, or null if the clipboard holds no text.</summary>
    public static string? GetText()
    {
        Application.VerifyAccess();
        return Application.Platform.Clipboard.GetText();
    }

    /// <summary>Puts <paramref name="text"/> on the clipboard. Returns false if the clipboard is unavailable.</summary>
    public static bool SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Application.VerifyAccess();
        return Application.Platform.Clipboard.SetText(text);
    }
}
