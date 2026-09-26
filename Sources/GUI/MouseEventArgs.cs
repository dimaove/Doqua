namespace Doqua.GUI;

public enum MouseButton
{
    Left,
    Middle,
    Right,
}

public class MouseEventArgs : EventArgs
{
    public MouseEventArgs(MouseButton button, int x, int y)
    {
        Button = button;
        X = x;
        Y = y;
    }

    public MouseButton Button { get; }

    /// <summary>X coordinate relative to the client area.</summary>
    public int X { get; }

    /// <summary>Y coordinate relative to the client area.</summary>
    public int Y { get; }
}
