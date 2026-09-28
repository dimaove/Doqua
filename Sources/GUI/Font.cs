namespace Doqua.GUI;

/// <summary>Font style flags; they can be combined: <c>FontStyle.Bold | FontStyle.Italic</c>.</summary>
[Flags]
public enum FontStyle
{
    /// <summary>Neither bold nor italic.</summary>
    Regular = 0,
    /// <summary>Bold.</summary>
    Bold = 1,
    /// <summary>Italic.</summary>
    Italic = 2,
}

/// <summary>
/// Immutable font description. <see cref="Size"/> is the em size in pixels.
/// Use <c>font with { Size = 20 }</c> to derive a changed copy.
/// </summary>
public sealed record Font
{
    private static Font? s_default;

    private readonly string _family = "";
    private readonly float _size;

    /// <summary>Creates a font description; the font itself is loaded when it is first used.</summary>
    public Font(string family, float size, FontStyle style = FontStyle.Regular)
    {
        Family = family;
        Size = size;
        Style = style;
    }

    /// <summary>
    /// Names of the font families installed on the system, sorted alphabetically; any of them can be
    /// passed to the constructor. Asks the operating system each time, so keep the result if needed often.
    /// </summary>
    public static IReadOnlyList<string> GetInstalledFamilies() =>
        [.. Application.Platform.Fonts.GetFamilies()
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)];

    /// <summary>System UI font at 14 pixels.</summary>
    public static Font Default => s_default ??= new Font(Application.Platform.Fonts.DefaultFamily, 14);

    /// <summary>Family name, e.g. "Noto Sans", or a generic one such as "sans-serif".</summary>
    public string Family
    {
        get => _family;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _family = value;
        }
    }

    /// <summary>Em size in pixels.</summary>
    public float Size
    {
        get => _size;
        init
        {
            if (!float.IsFinite(value) || value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, Localization.Get("Doqua.Error.FontSize"));
            _size = value;
        }
    }

    /// <summary>Bold and / or italic.</summary>
    public FontStyle Style { get; init; }

    /// <inheritdoc cref="Platform.IFontFace.Ascent"/>
    public int Ascent => Face.Ascent;

    /// <inheritdoc cref="Platform.IFontFace.Descent"/>
    public int Descent => Face.Descent;

    /// <inheritdoc cref="Platform.IFontFace.LineHeight"/>
    public int LineHeight => Face.LineHeight;

    internal FontFace Face => FontFace.Get(this);

    /// <summary>Size of the box that <see cref="DrawingContext.DrawText"/> fills. Lines are split by '\n'.</summary>
    public Size MeasureText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0)
            return default;

        var face = Face;
        float lineWidth = 0, maxWidth = 0;
        var lines = 1;
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case '\n':
                    maxWidth = Math.Max(maxWidth, lineWidth);
                    lineWidth = 0;
                    lines++;
                    break;
                case '\r':
                    break;
                default:
                    lineWidth += face.GetGlyph(rune.Value).Advance;
                    break;
            }
        }
        maxWidth = Math.Max(maxWidth, lineWidth);
        return new Size((int)MathF.Ceiling(maxWidth), lines * face.LineHeight);
    }

    /// <summary>Family, size and style, e.g. "Noto Sans 14px Bold".</summary>
    public override string ToString() =>
        Style == FontStyle.Regular ? $"{Family} {Size}px" : $"{Family} {Size}px {Style}";
}
