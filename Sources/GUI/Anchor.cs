namespace Doqua.GUI;

/// <summary>
/// Distances in pixels that a control keeps from its parent's edges when the parent is resized.
/// For each axis:
/// <list type="bullet">
/// <item>Left and Right: X = Left and the width stretches so that X + Width + Right = parent width.</item>
/// <item>only Left: X = Left, the width is unchanged.</item>
/// <item>only Right: the width is unchanged and X moves so that X + Width + Right = parent width.</item>
/// <item>neither: X and Width are left alone.</item>
/// </list>
/// Top and Bottom work the same way vertically. Example: <c>new Anchor(Right: 10, Bottom: 10)</c>
/// keeps a control in the bottom-right corner.
/// </summary>
public readonly record struct Anchor(int? Left = null, int? Top = null, int? Right = null, int? Bottom = null)
{
    /// <summary>No anchoring: the control keeps its bounds.</summary>
    public static Anchor None => default;

    /// <summary>Fills the parent, leaving <paramref name="margin"/> pixels on every side.</summary>
    public static Anchor Fill(int margin = 0) => new(margin, margin, margin, margin);

    /// <summary>True when no edge is set: the control is neither moved nor resized.</summary>
    public bool IsNone => Left == null && Top == null && Right == null && Bottom == null;

    /// <summary>Position and size along one axis for a parent of size <paramref name="parentSize"/>.</summary>
    internal static (int Position, int Size) Arrange(int position, int size, int? start, int? end, int parentSize) =>
        (start, end) switch
        {
            ({ } s, { } e) => (s, Math.Max(0, parentSize - s - e)),
            ({ } s, null) => (s, size),
            (null, { } e) => (parentSize - e - size, size),
            _ => (position, size),
        };
}
