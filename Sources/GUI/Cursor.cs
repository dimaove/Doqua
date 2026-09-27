namespace Doqua.GUI;

/// <summary>Mouse pointer shape shown over a control.</summary>
public enum Cursor
{
    /// <summary>The parent's cursor (the arrow at the top of the tree).</summary>
    Default,
    Arrow,
    /// <summary>Text cursor, for places where text can be selected or typed.</summary>
    IBeam,
    /// <summary>Pointing hand, for links.</summary>
    Hand,
    /// <summary>Busy.</summary>
    Wait,
    Crosshair,
    /// <summary>Resize horizontally (west-east).</summary>
    SizeWE,
    /// <summary>Resize vertically (north-south).</summary>
    SizeNS,
    /// <summary>Resize diagonally (north-west to south-east).</summary>
    SizeNWSE,
    /// <summary>Resize diagonally (north-east to south-west).</summary>
    SizeNESW,
    /// <summary>Move in any direction.</summary>
    SizeAll,
    /// <summary>Action not allowed.</summary>
    No,
}
