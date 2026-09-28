using Doqua.GUI;

namespace Doqua.Controls;

/// <summary>
/// A named set of file name patterns for <see cref="FileInput"/>, e.g. <c>new FileFilter("Certificates", "*.pem", "*.crt")</c>.
/// Patterns use <c>*</c> (any characters) and <c>?</c> (one character) and are matched against the file name only,
/// ignoring case on every platform.
/// </summary>
public sealed class FileFilter
{
    /// <summary>Creates a filter; <paramref name="patterns"/> must contain at least one non-empty pattern.</summary>
    public FileFilter(string name, params string[] patterns)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(patterns);
        if (patterns.Length == 0 || patterns.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException(Localization.Get("Doqua.Error.FilterPatterns"), nameof(patterns));
        Name = name;
        Patterns = patterns.Select(p => p.Trim()).ToArray();
    }

    /// <summary>A filter matching every file ("All files", in the current language when it is created).</summary>
    public static FileFilter AllFiles => new(Localization.Get("Doqua.FileInput.AllFiles"), "*");

    /// <summary>Name shown in the file browser, e.g. "Certificates".</summary>
    public string Name { get; }

    /// <summary>The patterns, e.g. "*.pem" and "*.crt".</summary>
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>True if the file name (not the folder part) of <paramref name="path"/> matches one of the patterns.</summary>
    public bool Matches(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var name = Path.GetFileName(path);
        return Patterns.Any(pattern => Glob(pattern, name));
    }

    /// <summary>The name and the patterns, as shown in the file browser: "Certificates (*.pem, *.crt)".</summary>
    public override string ToString() => $"{Name} ({string.Join(", ", Patterns)})";

    /// <summary>Wildcard match with '*' and '?', ignoring case; backtracks to the last '*' on a mismatch.</summary>
    private static bool Glob(string pattern, string text)
    {
        int p = 0, t = 0, star = -1, starText = 0;
        while (t < text.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                star = p++;
                starText = t;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++starText;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Length && pattern[p] == '*')
            p++;
        return p == pattern.Length;
    }
}
