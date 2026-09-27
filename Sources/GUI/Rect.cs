namespace Doqua.GUI;

public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>Creates a rect from its left, top, right and bottom edges (right and bottom exclusive).</summary>
    public static Rect FromEdges(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);

    public bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;

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
