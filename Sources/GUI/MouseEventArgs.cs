namespace Doqua.GUI;

public enum MouseButton
{
    Left,
    Middle,
    Right,
}

public class MouseEventArgs : EventArgs
{
    public MouseEventArgs(MouseButton button, int x, int y, KeyModifiers modifiers = KeyModifiers.None, int clickCount = 1)
    {
        Button = button;
        X = x;
        Y = y;
        Modifiers = modifiers;
        ClickCount = clickCount;
    }

    public MouseButton Button { get; }

    /// <summary>X coordinate relative to the client area.</summary>
    public int X { get; }

    /// <summary>Y coordinate relative to the client area.</summary>
    public int Y { get; }

    /// <summary>Keyboard modifiers held during the event (e.g. Shift+click).</summary>
    public KeyModifiers Modifiers { get; }

    /// <summary>
    /// 1 for a single click, 2 for a double click, 3 for a triple click and so on: presses of the
    /// same button on the same control within the system double-click time and distance.
    /// </summary>
    public int ClickCount { get; }
}

public class MouseMoveEventArgs : EventArgs
{
    public MouseMoveEventArgs(int x, int y, KeyModifiers modifiers = KeyModifiers.None)
    {
        X = x;
        Y = y;
        Modifiers = modifiers;
    }

    /// <summary>X coordinate relative to the control receiving the event.</summary>
    public int X { get; }

    /// <summary>Y coordinate relative to the control receiving the event.</summary>
    public int Y { get; }

    public KeyModifiers Modifiers { get; }
}
