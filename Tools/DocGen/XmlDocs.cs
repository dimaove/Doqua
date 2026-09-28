using System.Net;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace DocGen;

/// <summary>The compiler's XML documentation file, with &lt;inheritdoc&gt; and overrides resolved to their source.</summary>
sealed class XmlDocs
{
    private readonly Dictionary<string, XElement> _members;

    public XmlDocs(string path)
    {
        _members = XDocument.Load(path).Descendants("member")
            .Where(m => m.Attribute("name") != null)
            .GroupBy(m => m.Attribute("name")!.Value)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>Documentation of a type or member; for undocumented overrides, that of the member they override.</summary>
    public XElement? For(MemberInfo member)
    {
        var id = member is Type type ? ApiModel.TypeId(type) : ApiModel.MemberId(member);
        var doc = Resolve(id, 0);
        if (doc != null && doc.Elements().Any(e => e.Name != "inheritdoc"))
            return doc;

        // <inheritdoc/> without cref, or no comment at all: the overridden member's documentation.
        var baseMember = BaseMember(member);
        return baseMember != null ? For(baseMember) : doc;
    }

    public XElement? For(string id) => Resolve(id, 0);

    private XElement? Resolve(string id, int depth)
    {
        if (!_members.TryGetValue(id, out var doc) || depth > 5)
            return doc;
        var inherit = doc.Element("inheritdoc");
        if (inherit?.Attribute("cref")?.Value is { } cref && Resolve(cref, depth + 1) is { } source)
        {
            // Own elements win; the rest comes from the cref.
            var merged = new XElement("member", doc.Elements().Where(e => e.Name != "inheritdoc"));
            foreach (var element in source.Elements())
            {
                if (merged.Element(element.Name) == null || element.Name == "param")
                    merged.Add(element);
            }
            return merged;
        }
        return doc;
    }

    private static MemberInfo? BaseMember(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                var baseMethod = method.GetBaseDefinition();
                return baseMethod.DeclaringType != method.DeclaringType ? baseMethod : null;
            case PropertyInfo property:
                var accessor = (property.GetMethod ?? property.SetMethod)?.GetBaseDefinition();
                if (accessor == null || accessor.DeclaringType == property.DeclaringType)
                    return null;
                return accessor.DeclaringType!.GetProperty(property.Name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
            default:
                return null;
        }
    }
}

/// <summary>Turns documentation XML into HTML, linking crefs through <see cref="Site"/>.</summary>
sealed class DocRenderer(Site site, string prefix)
{
    public string Summary(XElement? doc) => Block(doc?.Element("summary"));

    /// <summary>The summary as plain text (for the search index and tables).</summary>
    public static string PlainSummary(XElement? doc)
    {
        var summary = doc?.Element("summary");
        if (summary == null)
            return "";
        var text = new StringBuilder();
        foreach (var node in summary.DescendantNodes())
        {
            if (node is XText t)
                text.Append(t.Value);
            else if (node is XElement { Name.LocalName: "see" or "paramref" or "typeparamref" } e && !e.Nodes().Any())
                text.Append(ShortCref(e.Attribute("cref")?.Value ?? e.Attribute("name")?.Value ?? e.Attribute("langword")?.Value ?? ""));
        }
        return Normalize(text.ToString());
    }

    /// <summary>Contents of an element as HTML paragraphs (text, inline markup and blocks such as lists and code).</summary>
    public string Block(XElement? element)
    {
        if (element == null)
            return "";
        var html = new StringBuilder();
        var paragraph = new StringBuilder();
        void Flush()
        {
            var text = paragraph.ToString().Trim();
            if (text.Length > 0)
                html.Append("<p>").Append(text).Append("</p>");
            paragraph.Clear();
        }

        foreach (var node in element.Nodes())
        {
            if (node is XElement { Name.LocalName: "list" or "code" or "para" } block)
            {
                Flush();
                html.Append(BlockElement(block));
            }
            else
            {
                paragraph.Append(Inline(node));
            }
        }
        Flush();
        return html.ToString();
    }

    private string BlockElement(XElement element) => element.Name.LocalName switch
    {
        "para" => Block(element),
        "code" => "<pre class=\"code\"><code>" + CodeHighlighter.CSharp(Dedent(element.Value), site, prefix) + "</code></pre>",
        "list" => List(element),
        _ => Inline(element),
    };

    private string List(XElement list)
    {
        var type = list.Attribute("type")?.Value ?? "bullet";
        if (type == "table")
        {
            var rows = list.Elements("item").Select(i => $"<tr><td>{Inline(i.Element("term"))}</td><td>{Inline(i.Element("description"))}</td></tr>");
            return "<table class=\"members\">" + string.Concat(rows) + "</table>";
        }
        var tag = type == "number" ? "ol" : "ul";
        var items = list.Elements("item").Select(item =>
        {
            var term = item.Element("term");
            var description = item.Element("description");
            var content = term != null ? $"<b>{Inline(term)}</b> — {Inline(description)}" : Inline(item);
            return $"<li>{content}</li>";
        });
        return $"<{tag}>{string.Concat(items)}</{tag}>";
    }

    public string Inline(XNode? node)
    {
        switch (node)
        {
            case null:
                return "";
            case XText text:
                return WebUtility.HtmlEncode(Normalize(text.Value, keepEdges: true));
            case XElement element:
                switch (element.Name.LocalName)
                {
                    case "see" or "seealso":
                        if (element.Attribute("langword")?.Value is { } word)
                            return $"<code>{word}</code>";
                        if (element.Attribute("href")?.Value is { } href)
                            return $"<a href=\"{href}\">{(element.Nodes().Any() ? Children(element) : WebUtility.HtmlEncode(href))}</a>";
                        var cref = element.Attribute("cref")?.Value ?? "";
                        var label = element.Nodes().Any() ? Children(element) : WebUtility.HtmlEncode(ShortCref(cref));
                        return site.Link(cref, label, prefix, code: !element.Nodes().Any());
                    case "paramref" or "typeparamref":
                        return $"<code class=\"param\">{WebUtility.HtmlEncode(element.Attribute("name")?.Value ?? "")}</code>";
                    case "c":
                        return $"<code>{WebUtility.HtmlEncode(element.Value)}</code>";
                    case "br":
                        return "<br>";
                    case "code" or "list" or "para":
                        return BlockElement(element);
                    default:
                        return Children(element);
                }
            default:
                return "";
        }
    }

    private string Children(XElement element) => string.Concat(element.Nodes().Select(Inline));

    /// <summary>"P:Doqua.GUI.Window.Title" -> "Window.Title"; "T:Doqua.GUI.Font" -> "Font"; methods without their parameters.</summary>
    public static string ShortCref(string cref)
    {
        var name = cref.Length > 2 && cref[1] == ':' ? cref[2..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0)
            name = name[..paren];
        name = name.Replace("#ctor", "ctor");
        var parts = name.Split('.');
        var kind = cref.Length > 2 && cref[1] == ':' ? cref[0] : 'T';
        var shortName = kind == 'T' || parts.Length < 2 ? parts[^1] : parts[^2] + "." + parts[^1];
        var tick = shortName.IndexOf('`');
        return tick >= 0 ? shortName[..tick] + "<T>" : shortName;
    }

    private static string Normalize(string text, bool keepEdges = false)
    {
        var collapsed = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return keepEdges ? collapsed : collapsed.Trim();
    }

    private static string Dedent(string code)
    {
        var lines = code.Replace("\r", "").Split('\n').ToList();
        while (lines.Count > 0 && lines[0].Trim().Length == 0)
            lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
            lines.RemoveAt(lines.Count - 1);
        var indent = lines.Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        return string.Join("\n", lines.Select(l => l.Length >= indent ? l[indent..] : l.TrimStart()));
    }
}
