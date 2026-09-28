namespace Doqua.GUI;

/// <summary>Physical key, independent of the keyboard layout (letters are those of the first layout).</summary>
public enum Key
{
    /// <summary>No key (a key Doqua does not map).</summary>
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

/// <summary>Modifier keys held during an event; they can be combined.</summary>
[Flags]
public enum KeyModifiers
{
    /// <summary>No modifier.</summary>
    None = 0,
    /// <summary>A Shift key.</summary>
    Shift = 1,
    /// <summary>A Ctrl key.</summary>
    Control = 2,
    /// <summary>An Alt key.</summary>
    Alt = 4,
}

/// <summary>A key press: the key, the modifiers held and <see cref="Handled"/>.</summary>
public class KeyEventArgs : EventArgs
{
    /// <summary>Creates the event arguments.</summary>
    public KeyEventArgs(Key key, KeyModifiers modifiers)
    {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>The key that was pressed.</summary>
    public Key Key { get; }
    /// <summary>Shift, Ctrl and Alt held with the key.</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>Set to true to stop the event from reaching the parent controls and the window.</summary>
    public bool Handled { get; set; }
}

/// <summary>Text typed by the user, as produced by the keyboard layout and input method.</summary>
public class TextInputEventArgs : EventArgs
{
    /// <summary>Creates the event arguments.</summary>
    public TextInputEventArgs(string text) => Text = text;

    /// <summary>The typed text.</summary>
    public string Text { get; }

    /// <summary>Set to true to stop the event from reaching the parent controls.</summary>
    public bool Handled { get; set; }
}
