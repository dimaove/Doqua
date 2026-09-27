namespace Doqua.GUI.Platform;

internal interface IClipboard
{
    string? GetText();

    bool SetText(string text);
}
