namespace Doqua.GUI;

public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    public static readonly Color Transparent = new(0, 0, 0, 0);
    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Gray = new(128, 128, 128);
    public static readonly Color LightGray = new(211, 211, 211);
    public static readonly Color Red = new(255, 0, 0);
    public static readonly Color Green = new(0, 128, 0);
    public static readonly Color Blue = new(0, 0, 255);
    public static readonly Color LightBlue = new(173, 216, 230);
    public static readonly Color Yellow = new(255, 255, 0);
    public static readonly Color Orange = new(255, 165, 0);
    public static readonly Color Purple = new(128, 0, 128);

    private static readonly Dictionary<Color, string> s_names = new()
    {
        [Transparent] = nameof(Transparent),
        [Black] = nameof(Black),
        [White] = nameof(White),
        [Gray] = nameof(Gray),
        [LightGray] = nameof(LightGray),
        [Red] = nameof(Red),
        [Green] = nameof(Green),
        [Blue] = nameof(Blue),
        [LightBlue] = nameof(LightBlue),
        [Yellow] = nameof(Yellow),
        [Orange] = nameof(Orange),
        [Purple] = nameof(Purple),
    };

    /// <summary>Creates an opaque color from 0xRRGGBB.</summary>
    public static Color FromRgb(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>Framebuffer pixel value, 0x00RRGGBB.</summary>
    internal uint ToPixel() => (uint)(R << 16 | G << 8 | B);

    public override string ToString() =>
        s_names.TryGetValue(this, out var name) ? name
        : A == 255 ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
