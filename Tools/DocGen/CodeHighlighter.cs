using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DocGen;

/// <summary>Small syntax highlighters for the code shown in the documentation; Doqua type names link to their pages.</summary>
static partial class CodeHighlighter
{
    private static readonly HashSet<string> s_keywords =
    [
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
        "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
        "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
        "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
        "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
        "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void",
        "volatile", "while", "var", "record", "init", "get", "set", "value", "nameof", "when", "and", "or", "not", "with",
        "async", "await", "yield", "partial", "global", "nint", "nuint", "required", "file", "scoped",
    ];

    [GeneratedRegex("""(?<comment>//[^\n]*|/\*.*?\*/)|(?<string>\$?@?"(?:[^"\\\n]|\\.|"")*"|'(?:[^'\\\n]|\\.)')|(?<attr>^\s*\[[A-Za-z]+(?:\([^)\n]*\))?\])|(?<number>\b\d+(?:\.\d+)?[fFdDmMuUlL]?\b)|(?<word>[A-Za-z_][A-Za-z0-9_]*)""", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex CSharpTokens();

    [GeneratedRegex("""(?<comment>//[^\n]*)|(?<key>"(?:[^"\\\n]|\\.)*"(?=\s*:))|(?<string>"(?:[^"\\\n]|\\.)*")""")]
    private static partial Regex JsonTokens();

    public static string CSharp(string code, Site site, string prefix)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match match in CSharpTokens().Matches(code))
        {
            html.Append(WebUtility.HtmlEncode(code[last..match.Index]));
            var text = match.Value;
            var encoded = WebUtility.HtmlEncode(text);
            if (match.Groups["comment"].Success)
                html.Append($"<span class=\"c\">{encoded}</span>");
            else if (match.Groups["string"].Success)
                html.Append($"<span class=\"s\">{encoded}</span>");
            else if (match.Groups["attr"].Success)
                html.Append($"<span class=\"a\">{encoded}</span>");
            else if (match.Groups["number"].Success)
                html.Append($"<span class=\"n\">{encoded}</span>");
            else if (s_keywords.Contains(text))
                html.Append($"<span class=\"k\">{encoded}</span>");
            else if (site.TypeUrl(text) is { } url)
                html.Append($"<a class=\"t\" href=\"{prefix}{url}\">{encoded}</a>");
            else if (char.IsUpper(text[0]))
                html.Append($"<span class=\"t\">{encoded}</span>");
            else
                html.Append(encoded);
            last = match.Index + match.Length;
        }
        html.Append(WebUtility.HtmlEncode(code[last..]));
        return html.ToString();
    }

    public static string Json(string json)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match match in JsonTokens().Matches(json))
        {
            html.Append(WebUtility.HtmlEncode(json[last..match.Index]));
            var css = match.Groups["comment"].Success ? "c" : match.Groups["key"].Success ? "t" : "s";
            html.Append($"<span class=\"{css}\">{WebUtility.HtmlEncode(match.Value)}</span>");
            last = match.Index + match.Length;
        }
        html.Append(WebUtility.HtmlEncode(json[last..]));
        return html.ToString();
    }
}
