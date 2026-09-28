namespace Doqua.GUI;

/// <summary>Rectangle with integer position and size, e.g. the bounds of a control.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    /// <summary>X + Width: the first column right of the rectangle.</summary>
    public int Right => X + Width;
    /// <summary>Y + Height: the first row below the rectangle.</summary>
    public int Bottom => Y + Height;
    /// <summary>True when the width or the height is zero or negative.</summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Creates a rect from its left, top, right and bottom edges (right and bottom exclusive).</summary>
    public static Rect FromEdges(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    /// <summary>True when (<paramref name="x"/>, <paramref name="y"/>) is inside the rectangle.</summary>
    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

    /// <summary>The rectangle moved by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    public Rect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };

    /// <summary>Returns the overlapping area, or an empty rect if there is none.</summary>
    public Rect Intersect(Rect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? new Rect(left, top, right - left, bottom - top) : default;
    }
}
