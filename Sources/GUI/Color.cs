namespace Doqua.GUI;

/// <summary>Color with red, green, blue and alpha (opacity) components, 0 to 255 each.</summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
/// <param name="A">Alpha: 0 is fully transparent, 255 (the default) opaque.</param>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Fully transparent (alpha 0).</summary>
    public static readonly Color Transparent = new(0, 0, 0, 0);
    /// <summary>Black (0, 0, 0).</summary>
    public static readonly Color Black = new(0, 0, 0);
    /// <summary>White (255, 255, 255).</summary>
    public static readonly Color White = new(255, 255, 255);
    /// <summary>Gray (128, 128, 128).</summary>
    public static readonly Color Gray = new(128, 128, 128);
    /// <summary>Light gray (211, 211, 211).</summary>
    public static readonly Color LightGray = new(211, 211, 211);
    /// <summary>Red (255, 0, 0).</summary>
    public static readonly Color Red = new(255, 0, 0);
    /// <summary>Green (0, 128, 0).</summary>
    public static readonly Color Green = new(0, 128, 0);
    /// <summary>Blue (0, 0, 255).</summary>
    public static readonly Color Blue = new(0, 0, 255);
    /// <summary>Light blue (173, 216, 230).</summary>
    public static readonly Color LightBlue = new(173, 216, 230);
    /// <summary>Yellow (255, 255, 0).</summary>
    public static readonly Color Yellow = new(255, 255, 0);
    /// <summary>Orange (255, 165, 0).</summary>
    public static readonly Color Orange = new(255, 165, 0);
    /// <summary>Purple (128, 0, 128).</summary>
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

    /// <summary>Mixes two colors: <paramref name="amount"/> 0 gives <paramref name="from"/>, 1 gives <paramref name="to"/>.</summary>
    public static Color Lerp(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        static byte Mix(byte a, byte b, float t) => (byte)MathF.Round(a + (b - a) * t);
        return new Color(Mix(from.R, to.R, amount), Mix(from.G, to.G, amount), Mix(from.B, to.B, amount), Mix(from.A, to.A, amount));
    }

    /// <summary>Framebuffer pixel value, 0x00RRGGBB.</summary>
    internal uint ToPixel() => (uint)(R << 16 | G << 8 | B);

    /// <summary>The color's name if it is one of the named colors, otherwise #RRGGBB (#AARRGGBB when translucent).</summary>
    public override string ToString() =>
        s_names.TryGetValue(this, out var name) ? name
        : A == 255 ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
