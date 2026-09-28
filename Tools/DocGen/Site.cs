using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DocGen;

/// <summary>An example program shown in the documentation.</summary>
record ExampleInfo(string Folder, string Title, string Summary, string[] Images, string[] ExtraFiles);

/// <summary>A hand-written page from Pages/: its output path, title and HTML body.</summary>
record PageInfo(string Path, string Title, string Body);

/// <summary>Writes the whole documentation site.</summary>
sealed partial class Site
{
    private static readonly Dictionary<string, string> s_namespaceSummaries = new()
    {
        ["Doqua.GUI"] = "The foundation: windows and the application loop, the control tree, drawing, fonts, bitmaps, input events, "
            + "the clipboard, timers, cursors and localization.",
        ["Doqua.Controls"] = "Ready-made controls in the classic style: panels, labels, buttons, check and radio buttons, text inputs, "
            + "lists, tables, trees, tabs, menus, date and number inputs.",
        ["Doqua.Core"] = "Information about the library.",
    };

    private readonly Assembly _assembly;
    private readonly XmlDocs _docs;
    private readonly string _root;       // Repository root.
    private readonly string _output;     // Docs/html.
    private readonly List<Type> _types;
    private readonly Dictionary<string, string> _urls = new();           // Documentation ID -> "api/X.html#anchor".
    private readonly Dictionary<string, string> _typeUrlsByName = new(); // "Window" -> "api/Doqua.GUI.Window.html".
    private readonly List<object> _searchIndex = [];
    private readonly List<ExampleInfo> _examples;
    private readonly List<PageInfo> _guides = [];

    public Site(Assembly assembly, XmlDocs docs, string root, string output, List<ExampleInfo> examples)
    {
        _assembly = assembly;
        _docs = docs;
        _root = root;
        _output = output;
        _examples = examples;
        _types = ApiModel.PublicTypes(assembly).ToList();

        foreach (var type in _types)
        {
            var url = "api/" + ApiModel.PageName(type);
            _urls[ApiModel.TypeId(type)] = url;
            var simple = type.Name.Split('`')[0];
            _typeUrlsByName.TryAdd(simple, url);
            _typeUrlsByName.TryAdd(ApiModel.DisplayName(type), url);
            foreach (var (member, anchor) in Anchors(type))
                _urls[ApiModel.MemberId(member)] = url + "#" + anchor;
        }
    }

    /// <summary>URL (relative to the site root) of a Doqua type's page by its C# name, e.g. "Window".</summary>
    public string? TypeUrl(string name) => _typeUrlsByName.GetValueOrDefault(name);

    /// <summary>A link for a cref: to the Doqua page, to learn.microsoft.com for .NET types, or plain text.</summary>
    public string Link(string cref, string label, string prefix, bool code)
    {
        var inner = code ? $"<code>{label}</code>" : label;
        if (_urls.TryGetValue(cref, out var url))
            return $"<a href=\"{prefix}{url}\">{inner}</a>";
        if (cref.Length > 2 && cref[1] == ':' && cref[2..].StartsWith("System."))
        {
            var name = cref[2..];
            var paren = name.IndexOf('(');
            if (paren >= 0)
                name = name[..paren];
            if (cref[0] != 'T')
                name = name[..name.LastIndexOf('.')] + "." + name[(name.LastIndexOf('.') + 1)..];
            return $"<a class=\"external\" href=\"https://learn.microsoft.com/dotnet/api/{name.Replace('`', '-').ToLowerInvariant()}\">{inner}</a>";
        }
        return inner;
    }

    public void Generate()
    {
        if (Directory.Exists(_output))
        {
            // Keep the screenshots; everything else is generated again.
            foreach (var dir in new[] { "api", "guides", "examples", "assets" })
            {
                if (Directory.Exists(Path.Combine(_output, dir)))
                    Directory.Delete(Path.Combine(_output, dir), recursive: true);
            }
            foreach (var file in Directory.GetFiles(_output, "*.html"))
                File.Delete(file);
        }
        Directory.CreateDirectory(_output);

        CopyAssets();
        LoadGuides();
        WriteHome();
        foreach (var guide in _guides)
            WritePage(guide.Path, guide.Title, ExpandMacros(guide.Body, Prefix(guide.Path)));
        WriteExamples();
        WriteApiIndex();
        foreach (var group in _types.GroupBy(t => t.Namespace ?? ""))
            WriteNamespace(group.Key, group.ToList());
        foreach (var type in _types)
            WriteType(type);
        WriteSearchIndex();
    }

    // ------------------------------------------------------------------ Layout

    private static string Prefix(string path) => string.Concat(Enumerable.Repeat("../", path.Count(c => c == '/')));

    private void WritePage(string path, string title, string body)
    {
        var prefix = Prefix(path);
        var html = new StringBuilder();
        html.Append($"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{WebUtility.HtmlEncode(title)} · Doqua</title>
            <link rel="icon" href="{prefix}../logo_32.png">
            <link rel="stylesheet" href="{prefix}assets/style.css">
            </head>
            <body>
            <header class="top">
              <a class="brand" href="{prefix}index.html"><img src="{prefix}../logo.png" alt=""><span>Doqua</span></a>
              <span class="tagline">Cross-platform GUI for .NET</span>
              <div class="search"><input id="search" type="search" placeholder="Search the documentation" autocomplete="off"><div id="search-results"></div></div>
            </header>
            <div class="layout">
            <nav class="side">{Navigation(prefix, path)}</nav>
            <main>
            {body}
            <footer>Doqua documentation · generated from the source code and its XML comments</footer>
            </main>
            </div>
            <script>const DOQUA_ROOT = "{prefix}";</script>
            <script src="{prefix}assets/search-index.js"></script>
            <script src="{prefix}assets/search.js"></script>
            </body>
            </html>
            """);
        var file = Path.Combine(_output, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, html.ToString());
    }

    private string Navigation(string prefix, string current)
    {
        var html = new StringBuilder();
        string Item(string url, string text, string css = "") =>
            $"<li><a class=\"{css}{(url == current ? " current" : "")}\" href=\"{prefix}{url}\">{WebUtility.HtmlEncode(text)}</a></li>";

        html.Append("<h4>Guide</h4><ul>");
        html.Append(Item("index.html", "Overview"));
        foreach (var guide in _guides)
            html.Append(Item(guide.Path, guide.Title));
        html.Append("</ul><h4>Examples</h4><ul>");
        html.Append(Item("examples/index.html", "All examples"));
        foreach (var example in _examples)
            html.Append(Item($"examples/{example.Folder.ToLowerInvariant()}.html", example.Title));
        html.Append("</ul><h4>API reference</h4><ul>");
        html.Append(Item("api/index.html", "Namespaces"));
        html.Append("</ul>");
        foreach (var group in _types.GroupBy(t => t.Namespace ?? ""))
        {
            html.Append($"<h5><a href=\"{prefix}api/{group.Key}.html\">{group.Key}</a></h5><ul class=\"types\">");
            foreach (var type in group.Where(t => !t.IsNested))
                html.Append(Item("api/" + ApiModel.PageName(type), ApiModel.DisplayName(type), KindCss(type)));
            html.Append("</ul>");
        }
        return html.ToString();
    }

    private static string KindCss(Type type) => "kind-" + ApiModel.Kind(type).Replace(' ', '-');

    // ------------------------------------------------------------------ Hand-written pages

    private void LoadGuides()
    {
        var folder = Path.Combine(_root, "Tools", "DocGen", "Pages", "guides");
        foreach (var file in Directory.GetFiles(folder, "*.html").Order())
        {
            var body = File.ReadAllText(file);
            var title = TitleRegex().Match(body) is { Success: true } m ? WebUtility.HtmlDecode(m.Groups[1].Value) : Path.GetFileNameWithoutExtension(file);
            // "01-getting-started.html" -> "guides/getting-started.html"
            var name = Regex.Replace(Path.GetFileName(file), @"^\d+-", "");
            _guides.Add(new PageInfo("guides/" + name, title, body));
            _searchIndex.Add(new { n = title, k = "guide", u = "guides/" + name, s = FirstParagraph(body) });
        }
    }

    [GeneratedRegex("<h1>(.*?)</h1>")]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"\[\[([A-Za-z0-9_.<>]+)\]\]")]
    private static partial Regex MacroRegex();

    [GeneratedRegex("<pre class=\"cs\">(.*?)</pre>", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex("<p>(.*?)</p>", RegexOptions.Singleline)]
    private static partial Regex ParagraphRegex();

    private static string FirstParagraph(string html) =>
        ParagraphRegex().Match(html) is { Success: true } m
            ? WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, "<.*?>", "").Replace("[[", "").Replace("]]", "")).Trim()
            : "";

    /// <summary>[[Window]] and [[Window.Title]] become links; &lt;pre class="cs"&gt; blocks are highlighted; {root} is the site root.</summary>
    private string ExpandMacros(string body, string prefix)
    {
        body = CodeBlockRegex().Replace(body, m =>
            "<pre class=\"code\"><code>" + CodeHighlighter.CSharp(WebUtility.HtmlDecode(m.Groups[1].Value).Trim('\n'), this, prefix) + "</code></pre>");
        body = MacroRegex().Replace(body, m =>
        {
            var name = m.Groups[1].Value;
            var dot = name.IndexOf('.');
            var typeName = dot < 0 ? name : name[..dot];
            if (TypeUrl(typeName) is not { } url)
            {
                Console.Error.WriteLine($"warning: unknown type in [[{name}]]");
                return $"<code>{WebUtility.HtmlEncode(name)}</code>";
            }
            if (dot >= 0)
            {
                var member = name[(dot + 1)..];
                var type = _types.First(t => "api/" + ApiModel.PageName(t) == url);
                var anchor = Anchors(type).FirstOrDefault(a => a.Member.Name == member).Anchor;
                if (anchor != null)
                    url += "#" + anchor;
                else
                    Console.Error.WriteLine($"warning: unknown member in [[{name}]]");
            }
            return $"<a href=\"{prefix}{url}\"><code>{WebUtility.HtmlEncode(name)}</code></a>";
        });
        return body.Replace("{root}", prefix);
    }

    private void WriteHome()
    {
        var body = File.ReadAllText(Path.Combine(_root, "Tools", "DocGen", "Pages", "index.html"));
        var gallery = new StringBuilder("<div class=\"gallery\">");
        foreach (var example in _examples)
        {
            gallery.Append($"<a class=\"card\" href=\"examples/{example.Folder.ToLowerInvariant()}.html\"><img src=\"images/examples/{example.Images[0]}\" alt=\"\">"
                + $"<b>{WebUtility.HtmlEncode(example.Title)}</b><span>{WebUtility.HtmlEncode(example.Summary)}</span></a>");
        }
        gallery.Append("</div>");
        body = body.Replace("{{gallery}}", gallery.ToString());
        WritePage("index.html", "Overview", ExpandMacros(body, ""));
    }

    private void WriteExamples()
    {
        var index = new StringBuilder("<h1>Examples</h1><p>The <code>Examples</code> folder has one project per example; "
            + "build them all with <code>dotnet build Examples/examples.sln</code> and run one with "
            + "<code>dotnet run --project Examples/Simple</code>.</p><div class=\"gallery\">");
        foreach (var example in _examples)
        {
            var page = $"examples/{example.Folder.ToLowerInvariant()}.html";
            index.Append($"<a class=\"card\" href=\"{example.Folder.ToLowerInvariant()}.html\"><img src=\"../images/examples/{example.Images[0]}\" alt=\"\">"
                + $"<b>{WebUtility.HtmlEncode(example.Title)}</b><span>{WebUtility.HtmlEncode(example.Summary)}</span></a>");

            var body = new StringBuilder($"<h1>{WebUtility.HtmlEncode(example.Title)}</h1><p class=\"lead\">{WebUtility.HtmlEncode(example.Summary)}</p>");
            body.Append($"<p>Run it with <code>dotnet run --project Examples/{example.Folder}</code>.</p>");
            foreach (var image in example.Images)
                body.Append($"<figure><img class=\"shot\" src=\"../images/examples/{image}\" alt=\"{WebUtility.HtmlEncode(example.Title)}\"></figure>");
            var folder = Path.Combine(_root, "Examples", example.Folder);
            foreach (var file in new[] { "Program.cs" }.Concat(example.ExtraFiles))
            {
                var text = File.ReadAllText(Path.Combine(folder, file));
                var highlighted = file.EndsWith(".json") ? CodeHighlighter.Json(text) : CodeHighlighter.CSharp(text, this, "../");
                body.Append($"<h2>{WebUtility.HtmlEncode(file)}</h2><pre class=\"code\"><code>{highlighted}</code></pre>");
            }
            WritePage(page, example.Title, body.ToString());
            _searchIndex.Add(new { n = example.Title + " example", k = "example", u = page, s = example.Summary });
        }
        index.Append("</div>");
        WritePage("examples/index.html", "Examples", index.ToString());
    }

    // ------------------------------------------------------------------ API reference

    private void WriteApiIndex()
    {
        var body = new StringBuilder("<h1>API reference</h1><p>Everything public in <code>doqua.dll</code>, by namespace. "
            + "Controls you use directly are in <code>Doqua.Controls</code>; the window, drawing and input types they build on are in <code>Doqua.GUI</code>.</p>");
        foreach (var group in _types.GroupBy(t => t.Namespace ?? ""))
        {
            body.Append($"<h2><a href=\"{group.Key}.html\">{group.Key}</a></h2><p>{WebUtility.HtmlEncode(s_namespaceSummaries.GetValueOrDefault(group.Key, ""))}</p>");
            body.Append(TypeTable(group, ""));
        }
        WritePage("api/index.html", "API reference", body.ToString());
    }

    private void WriteNamespace(string ns, List<Type> types)
    {
        var body = new StringBuilder($"<p class=\"crumbs\"><a href=\"index.html\">API reference</a></p><h1>{ns} namespace</h1>");
        body.Append($"<p class=\"lead\">{WebUtility.HtmlEncode(s_namespaceSummaries.GetValueOrDefault(ns, ""))}</p>");
        foreach (var kind in new[] { "class", "static class", "record", "struct", "record struct", "enum", "interface", "delegate" })
        {
            var ofKind = types.Where(t => ApiModel.Kind(t) == kind).ToList();
            if (ofKind.Count == 0)
                continue;
            var heading = kind switch { "class" => "Classes", "static class" => "Static classes", "record" => "Records", "struct" => "Structures", "record struct" => "Record structures", "enum" => "Enumerations", "interface" => "Interfaces", _ => "Delegates" };
            body.Append($"<h2>{heading}</h2>").Append(TypeTable(ofKind, ""));
        }
        WritePage($"api/{ns}.html", ns, body.ToString());
        _searchIndex.Add(new { n = ns, k = "namespace", u = $"api/{ns}.html", s = s_namespaceSummaries.GetValueOrDefault(ns, "") });
    }

    private string TypeTable(IEnumerable<Type> types, string prefix)
    {
        var renderer = new DocRenderer(this, "../");
        var rows = types.Select(t =>
            $"<tr><td><a class=\"{KindCss(t)}\" href=\"{prefix}{ApiModel.PageName(t)}\">{WebUtility.HtmlEncode(ApiModel.DisplayName(t))}</a></td>"
            + $"<td>{Inline(renderer, _docs.For(t)?.Element("summary"))}</td></tr>");
        return "<table class=\"members\">" + string.Concat(rows) + "</table>";
    }

    /// <summary>Summary as inline HTML (first paragraph only, for tables).</summary>
    private static string Inline(DocRenderer renderer, XElement? summary)
    {
        if (summary == null)
            return "";
        var html = renderer.Block(summary);
        var first = ParagraphRegex().Match(html);
        return first.Success ? first.Groups[1].Value : html;
    }

    /// <summary>Anchors for a type's members: the member name, with the parameter types added for overloads.</summary>
    private static IEnumerable<(MemberInfo Member, string Anchor)> Anchors(Type type)
    {
        var members = ApiModel.Members(type).ToList();
        foreach (var group in members.GroupBy(m => m is ConstructorInfo ? "ctor" : m.Name))
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
                yield return (list[i], list.Count == 1 ? group.Key : group.Key + "-" + (i + 1));
        }
    }

    private string TypeLink(Type type, string text, string prefix)
    {
        if (type.IsGenericType && !type.IsGenericTypeDefinition)
            type = type.GetGenericTypeDefinition();
        if (_urls.TryGetValue(ApiModel.TypeId(type), out var url))
            return $"<a href=\"{prefix}{url}\">{text}</a>";
        if (type.Namespace?.StartsWith("System") == true && !type.IsGenericParameter && type.FullName != null)
            return $"<a class=\"external\" href=\"https://learn.microsoft.com/dotnet/api/{type.FullName.Replace('`', '-').Replace('+', '.').ToLowerInvariant()}\">{text}</a>";
        return text;
    }

    private void WriteType(Type type)
    {
        const string prefix = "../";
        var page = "api/" + ApiModel.PageName(type);
        var renderer = new DocRenderer(this, prefix);
        var doc = _docs.For(type);
        string Name(Type t, NullabilityInfo? n = null) => ApiModel.TypeName(t, n, (x, text) => TypeLink(x, text, prefix));
        var kind = ApiModel.Kind(type);
        var display = ApiModel.DisplayName(type);

        var body = new StringBuilder();
        body.Append($"<p class=\"crumbs\"><a href=\"index.html\">API reference</a> › <a href=\"{type.Namespace}.html\">{type.Namespace}</a></p>");
        body.Append($"<h1>{WebUtility.HtmlEncode(display)} <small>{kind}</small></h1>");

        // Declaration, inheritance, derived types.
        var declaration = new StringBuilder($"<span class=\"k\">public</span> ");
        if (type.IsSealed && !type.IsValueType && !ApiModel.IsStaticClass(type) && !type.IsEnum && !typeof(Delegate).IsAssignableFrom(type))
            declaration.Append("<span class=\"k\">sealed</span> ");
        else if (type.IsAbstract && !type.IsInterface && !ApiModel.IsStaticClass(type))
            declaration.Append("<span class=\"k\">abstract</span> ");
        declaration.Append($"<span class=\"k\">{kind}</span> {WebUtility.HtmlEncode(display.Contains('.') ? display[(display.LastIndexOf('.') + 1)..] : display)}");
        if (ApiModel.IsRecord(type))
        {
            var primary = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();
            if (primary != null && primary.GetParameters().Length > 0)
                declaration.Append("(" + string.Join(", ", primary.GetParameters().Select(p => Name(p.ParameterType, ApiModel.Nullability(p)) + " " + p.Name)) + ")");
        }
        var bases = new List<string>();
        if (type.BaseType is { } baseType && baseType != typeof(object) && baseType != typeof(ValueType) && baseType != typeof(Enum))
            bases.Add(Name(baseType));
        var ownInterfaces = type.GetInterfaces().Where(i => type.BaseType == null || !type.BaseType.GetInterfaces().Contains(i))
            .Where(i => i.IsPublic && !(ApiModel.IsRecord(type) && i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>)));
        bases.AddRange(ownInterfaces.Select(i => Name(i)));
        if (bases.Count > 0)
            declaration.Append(" : " + string.Join(", ", bases));
        body.Append($"<pre class=\"declaration\"><code>{declaration}</code></pre>");
        body.Append($"<p class=\"meta\">Namespace <a href=\"{type.Namespace}.html\">{type.Namespace}</a> · Assembly <code>doqua.dll</code></p>");

        var chain = new List<Type>();
        for (var b = type.BaseType; b != null && b != typeof(object) && b != typeof(ValueType) && b != typeof(Enum); b = b.BaseType)
            chain.Insert(0, b);
        if (chain.Count > 0)
            body.Append("<p class=\"meta\">Inheritance: " + string.Join(" → ", chain.Select(b => Name(b))) + " → <b>" + WebUtility.HtmlEncode(display) + "</b></p>");
        var derived = _types.Where(t => t.BaseType != null && (t.BaseType == type || (t.BaseType.IsGenericType && t.BaseType.GetGenericTypeDefinition() == type))).ToList();
        if (derived.Count > 0)
            body.Append("<p class=\"meta\">Derived: " + string.Join(", ", derived.Select(d => TypeLink(d, WebUtility.HtmlEncode(ApiModel.DisplayName(d)), prefix))) + "</p>");

        body.Append(renderer.Summary(doc));
        if (doc?.Element("remarks") is { } remarks)
            body.Append("<div class=\"remarks\">").Append(renderer.Block(remarks)).Append("</div>");
        foreach (var typeParameter in doc?.Elements("typeparam") ?? [])
            body.Append($"<p><b>Type parameter <code>{typeParameter.Attribute("name")?.Value}</code></b>: {renderer.Block(typeParameter)}</p>");
        if (doc?.Element("example") is { } example)
            body.Append("<h2>Example</h2>").Append(renderer.Block(example));

        var shot = Path.Combine(_output, "images", "types", display.Split('<')[0] + ".png");
        if (File.Exists(shot))
            body.Append($"<figure><img class=\"shot\" src=\"../images/types/{Path.GetFileName(shot)}\" alt=\"{WebUtility.HtmlEncode(display)}\"></figure>");

        var nested = _types.Where(t => t.DeclaringType == type).ToList();
        if (nested.Count > 0)
            body.Append("<h2>Nested types</h2>").Append(TypeTable(nested, ""));

        var anchors = Anchors(type).ToList();
        if (type.IsEnum)
        {
            body.Append("<h2>Values</h2><table class=\"members\"><tr><th>Name</th><th>Value</th><th>Description</th></tr>");
            foreach (var (member, anchor) in anchors)
            {
                var field = (FieldInfo)member;
                var value = Convert.ToInt64(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture);
                body.Append($"<tr id=\"{anchor}\"><td><code>{field.Name}</code></td><td>{value}</td><td>{Inline(renderer, _docs.For(field)?.Element("summary"))}</td></tr>");
                _searchIndex.Add(new { n = $"{display}.{field.Name}", k = "value", u = $"{page}#{anchor}", s = DocRenderer.PlainSummary(_docs.For(field)) });
            }
            body.Append("</table>");
        }
        else
        {
            var sections = new (string Title, Func<MemberInfo, bool> Filter)[]
            {
                ("Constructors", m => m is ConstructorInfo),
                ("Properties", m => m is PropertyInfo),
                ("Methods", m => m is MethodInfo { IsSpecialName: false }),
                ("Events", m => m is EventInfo),
                ("Fields", m => m is FieldInfo),
                ("Operators", m => m is MethodInfo { IsSpecialName: true }),
            };
            foreach (var (title, filter) in sections)
            {
                var members = anchors.Where(a => filter(a.Member)).OrderBy(a => a.Member is ConstructorInfo ? "" : a.Member.Name).ToList();
                if (members.Count == 0)
                    continue;
                body.Append($"<h2>{title}</h2><table class=\"members\">");
                foreach (var (member, anchor) in members)
                {
                    var badge = ApiModel.IsProtected(member) ? " <span class=\"badge\">protected</span>" : "";
                    var isStatic = member is MethodBase { IsStatic: true } or PropertyInfo { GetMethod.IsStatic: true } or FieldInfo { IsStatic: true } ? " <span class=\"badge\">static</span>" : "";
                    body.Append($"<tr><td><a href=\"#{anchor}\">{WebUtility.HtmlEncode(ShortSignature(member))}</a>{badge}{isStatic}</td>"
                        + $"<td>{Inline(renderer, _docs.For(member)?.Element("summary"))}</td></tr>");
                }
                body.Append("</table>");
                foreach (var (member, anchor) in members)
                    body.Append(MemberDetails(type, member, anchor, renderer, Name));
            }
            body.Append(InheritedMembers(type, prefix));
        }

        WritePage(page, display, body.ToString());
        _searchIndex.Add(new { n = display, k = kind, u = page, s = DocRenderer.PlainSummary(doc) });
        foreach (var (member, anchor) in anchors.Where(a => !type.IsEnum))
        {
            _searchIndex.Add(new
            {
                n = $"{display.Split('<')[0]}.{(member is ConstructorInfo ? display.Split('<')[0] : member.Name)}",
                k = MemberKind(member),
                u = $"{page}#{anchor}",
                s = DocRenderer.PlainSummary(_docs.For(member)),
            });
        }
    }

    private static string MemberKind(MemberInfo member) => member switch
    {
        ConstructorInfo => "constructor",
        PropertyInfo => "property",
        EventInfo => "event",
        FieldInfo => "field",
        _ => "method",
    };

    /// <summary>"Show(Control, int, int, Action&lt;MenuItem?&gt;?)" for tables.</summary>
    private static string ShortSignature(MemberInfo member)
    {
        string Short(Type t) => WebUtility.HtmlDecode(Regex.Replace(ApiModel.TypeName(t, null, (_, text) => text), "<.*?>", ""));
        return member switch
        {
            ConstructorInfo c => ApiModel.DisplayName(c.DeclaringType!).Split('<')[0] + "(" + string.Join(", ", c.GetParameters().Select(p => Short(p.ParameterType))) + ")",
            MethodInfo m => m.Name + (m.IsGenericMethodDefinition ? "<" + string.Join(", ", m.GetGenericArguments().Select(a => a.Name)) + ">" : "")
                + "(" + string.Join(", ", m.GetParameters().Select(p => Short(p.ParameterType))) + ")",
            PropertyInfo p when p.GetIndexParameters().Length > 0 => "this[" + string.Join(", ", p.GetIndexParameters().Select(i => Short(i.ParameterType))) + "]",
            _ => member.Name,
        };
    }

    private string MemberDetails(Type type, MemberInfo member, string anchor, DocRenderer renderer, Func<Type, NullabilityInfo?, string> name)
    {
        var doc = _docs.For(member);
        var html = new StringBuilder($"<section class=\"member\" id=\"{anchor}\"><h3>{WebUtility.HtmlEncode(ShortSignature(member))}</h3>");
        var modifiers = ApiModel.Modifiers(member);
        var signature = new StringBuilder($"<span class=\"k\">{ApiModel.Accessibility(member)}</span> ");
        if (modifiers.Length > 0)
            signature.Append($"<span class=\"k\">{modifiers.Trim()}</span> ");
        string Parameters(ParameterInfo[] parameters) => string.Join(", ", parameters.Select(p =>
        {
            var text = new StringBuilder();
            if (p.IsDefined(typeof(ParamArrayAttribute)))
                text.Append("<span class=\"k\">params</span> ");
            if (p.ParameterType.IsByRef)
                text.Append(p.IsOut ? "<span class=\"k\">out</span> " : p.IsIn ? "<span class=\"k\">in</span> " : "<span class=\"k\">ref</span> ");
            text.Append(name(p.ParameterType, ApiModel.Nullability(p))).Append(' ').Append(p.Name);
            if (p.HasDefaultValue)
                text.Append(" = ").Append(WebUtility.HtmlEncode(ApiModel.DefaultValue(p)));
            return text.ToString();
        }));

        switch (member)
        {
            case ConstructorInfo ctor:
                signature.Append(WebUtility.HtmlEncode(ApiModel.DisplayName(type).Split('<')[0].Split('.')[^1])).Append('(').Append(Parameters(ctor.GetParameters())).Append(')');
                break;
            case MethodInfo method:
                signature.Append(name(method.ReturnType, ApiModel.Nullability(method.ReturnParameter))).Append(' ')
                    .Append(WebUtility.HtmlEncode(method.Name));
                if (method.IsGenericMethodDefinition)
                    signature.Append("&lt;" + string.Join(", ", method.GetGenericArguments().Select(a => a.Name)) + "&gt;");
                signature.Append('(').Append(Parameters(method.GetParameters())).Append(')');
                break;
            case PropertyInfo property:
                signature.Append(name(property.PropertyType, ApiModel.Nullability(property))).Append(' ');
                var indexers = property.GetIndexParameters();
                signature.Append(indexers.Length > 0 ? "<span class=\"k\">this</span>[" + Parameters(indexers) + "]" : WebUtility.HtmlEncode(property.Name));
                var accessors = new List<string>();
                if (property.GetMethod is { } getter && (getter.IsPublic || getter.IsFamily || getter.IsFamilyOrAssembly))
                    accessors.Add("<span class=\"k\">get</span>;");
                if (property.SetMethod is { } setter && (setter.IsPublic || setter.IsFamily || setter.IsFamilyOrAssembly))
                {
                    var init = setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit");
                    var prot = setter.IsPublic || ApiModel.IsProtected(property) ? "" : "<span class=\"k\">protected</span> ";
                    accessors.Add(prot + (init ? "<span class=\"k\">init</span>;" : "<span class=\"k\">set</span>;"));
                }
                signature.Append(" { " + string.Join(" ", accessors) + " }");
                break;
            case EventInfo @event:
                signature.Append("<span class=\"k\">event</span> ").Append(name(@event.EventHandlerType!, ApiModel.Nullability(@event))).Append(' ').Append(@event.Name);
                break;
            case FieldInfo field:
                signature.Append(name(field.FieldType, ApiModel.Nullability(field))).Append(' ').Append(field.Name);
                if (field.IsLiteral)
                    signature.Append(" = " + WebUtility.HtmlEncode(field.GetRawConstantValue() is string s ? $"\"{s}\"" : Convert.ToString(field.GetRawConstantValue(), System.Globalization.CultureInfo.InvariantCulture)));
                break;
        }
        html.Append($"<pre class=\"declaration\"><code>{signature}</code></pre>");
        html.Append(renderer.Summary(doc));

        var parameters = member switch { MethodBase m => m.GetParameters(), PropertyInfo p => p.GetIndexParameters(), _ => [] };
        var documented = parameters.Where(p => doc?.Elements("param").Any(e => e.Attribute("name")?.Value == p.Name) == true).ToList();
        if (documented.Count > 0)
        {
            html.Append("<dl class=\"params\">");
            foreach (var p in documented)
                html.Append($"<dt><code>{p.Name}</code></dt><dd>{renderer.Block(doc!.Elements("param").First(e => e.Attribute("name")?.Value == p.Name))}</dd>");
            html.Append("</dl>");
        }
        if (doc?.Element("returns") is { } returns)
            html.Append("<p><b>Returns:</b> ").Append(Strip(renderer.Block(returns))).Append("</p>");
        if (doc?.Element("value") is { } value)
            html.Append("<p><b>Value:</b> ").Append(Strip(renderer.Block(value))).Append("</p>");
        foreach (var exception in doc?.Elements("exception") ?? [])
        {
            var cref = exception.Attribute("cref")?.Value ?? "";
            html.Append($"<p><b>Throws</b> {Link(cref, WebUtility.HtmlEncode(DocRenderer.ShortCref(cref)), "../", code: true)}: {Strip(renderer.Block(exception))}</p>");
        }
        if (doc?.Element("remarks") is { } remarks)
            html.Append("<div class=\"remarks\">").Append(renderer.Block(remarks)).Append("</div>");
        if (doc?.Element("example") is { } example)
            html.Append(renderer.Block(example));
        if (member is MethodInfo overriding && overriding.GetBaseDefinition() is { } baseMethod && baseMethod.DeclaringType != overriding.DeclaringType)
        {
            var baseType = baseMethod.DeclaringType!;
            html.Append($"<p class=\"meta\">Overrides {TypeLink(baseType, WebUtility.HtmlEncode(ApiModel.DisplayName(baseType)), "../")}.{WebUtility.HtmlEncode(baseMethod.Name)}.</p>");
        }
        html.Append("</section>");
        return html.ToString();
    }

    private static string Strip(string html) => html.StartsWith("<p>") && html.EndsWith("</p>") && html.IndexOf("<p>", 1) < 0 ? html[3..^4] : html;

    /// <summary>Members a type gets from its Doqua base types (the ones it does not override), linked to their pages.</summary>
    private string InheritedMembers(Type type, string prefix)
    {
        var html = new StringBuilder();
        var own = new HashSet<string>(ApiModel.Members(type).Select(m => m.Name));
        for (var b = type.BaseType; b != null; b = b.BaseType)
        {
            var definition = b.IsGenericType ? b.GetGenericTypeDefinition() : b;
            if (!_urls.ContainsKey(ApiModel.TypeId(definition)))
                break;
            var links = Anchors(definition)
                .Where(a => a.Member is not ConstructorInfo && own.Add(a.Member.Name))
                .OrderBy(a => a.Member.Name)
                .Select(a => $"<a href=\"{prefix}{_urls[ApiModel.MemberId(a.Member)]}\">{WebUtility.HtmlEncode(a.Member.Name)}</a>")
                .Distinct()
                .ToList();
            if (links.Count > 0)
                html.Append($"<p class=\"inherited\"><b>From {TypeLink(definition, WebUtility.HtmlEncode(ApiModel.DisplayName(definition)), prefix)}:</b> {string.Join(", ", links)}</p>");
        }
        return html.Length > 0 ? "<h2>Inherited members</h2>" + html : "";
    }

    // ------------------------------------------------------------------ Assets and search

    private void CopyAssets()
    {
        var source = Path.Combine(_root, "Tools", "DocGen", "Assets");
        var target = Path.Combine(_output, "assets");
        Directory.CreateDirectory(target);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
    }

    private void WriteSearchIndex()
    {
        var json = JsonSerializer.Serialize(_searchIndex);
        File.WriteAllText(Path.Combine(_output, "assets", "search-index.js"), "const DOQUA_SEARCH = " + json + ";\n");
    }
}
