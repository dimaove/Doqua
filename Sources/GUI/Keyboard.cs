namespace Doqua.GUI;

/// <summary>Physical key, independent of the keyboard layout (letters are those of the first layout).</summary>
public enum Key
{
    None,
    Backspace,
    Tab,
    Enter,
    Escape,
    Space,
    PageUp,
    PageDown,
    End,
    Home,
    Left,
    Up,
    Right,
    Down,
    Insert,
    Delete,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
}

public class KeyEventArgs : EventArgs
{
    public KeyEventArgs(Key key, KeyModifiers modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public Key Key { get; }
    public KeyModifiers Modifiers { get; }

    /// <summary>Set to true to stop the event from reaching the parent controls and the window.</summary>
    public bool Handled { get; set; }
}

/// <summary>Text typed by the user, as produced by the keyboard layout and input method.</summary>
public class TextInputEventArgs : EventArgs
{
    public TextInputEventArgs(string text) => Text = text;

    public string Text { get; }

    /// <summary>Set to true to stop the event from reaching the parent controls.</summary>
    public bool Handled { get; set; }
}
