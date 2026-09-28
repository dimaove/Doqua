using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace DocGen;

/// <summary>Which public types and members are documented, their documentation IDs and C# signatures.</summary>
static class ApiModel
{
    private static readonly NullabilityInfoContext s_nullability = new();

    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    public static IEnumerable<Type> PublicTypes(Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => (t.IsPublic || t.IsNestedPublic) && !t.IsDefined(typeof(CompilerGeneratedAttribute)))
            .OrderBy(t => t.Namespace).ThenBy(t => DisplayName(t));

    /// <summary>Public members, and protected ones of types that can be derived from; no compiler-generated members.</summary>
    public static IEnumerable<MemberInfo> Members(Type type)
    {
        foreach (var member in type.GetMembers(Declared))
        {
            if (member is Type || member.IsDefined(typeof(CompilerGeneratedAttribute)) || member.Name.StartsWith('<'))
                continue;
            if (!IsVisible(member, type))
                continue;
            if (member is MethodBase { IsSpecialName: true } method && !method.Name.StartsWith("op_") && method is not ConstructorInfo)
                continue; // Property and event accessors.
            if (member is ConstructorInfo { IsStatic: true })
                continue;
            if (member is MethodInfo { Name: "Finalize" })
                continue;
            if (type.IsEnum && member is FieldInfo { IsSpecialName: true })
                continue; // value__
            yield return member;
        }
    }

    private static bool IsVisible(MemberInfo member, Type type)
    {
        bool Visible(MethodBase? m) => m != null && (m.IsPublic || (!type.IsSealed && (m.IsFamily || m.IsFamilyOrAssembly)));
        return member switch
        {
            MethodBase method => Visible(method),
            PropertyInfo property => Visible(property.GetMethod) || Visible(property.SetMethod),
            EventInfo @event => Visible(@event.AddMethod),
            FieldInfo field => field.IsPublic || (!type.IsSealed && (field.IsFamily || field.IsFamilyOrAssembly)),
            _ => false,
        };
    }

    public static bool IsProtected(MemberInfo member) => member switch
    {
        MethodBase m => !m.IsPublic,
        PropertyInfo p => !(p.GetMethod?.IsPublic ?? false) && !(p.SetMethod?.IsPublic ?? false),
        EventInfo e => !(e.AddMethod?.IsPublic ?? false),
        FieldInfo f => !f.IsPublic,
        _ => false,
    };

    public static bool IsRecord(Type type) =>
        type.GetMethod("PrintMembers", BindingFlags.NonPublic | BindingFlags.Instance, [typeof(StringBuilder)]) != null;

    public static bool IsStaticClass(Type type) => type.IsClass && type.IsAbstract && type.IsSealed;

    public static string Kind(Type type) =>
        type.IsEnum ? "enum"
        : type.IsInterface ? "interface"
        : typeof(Delegate).IsAssignableFrom(type) ? "delegate"
        : type.IsValueType ? (IsRecord(type) ? "record struct" : "struct")
        : IsRecord(type) ? "record"
        : IsStaticClass(type) ? "static class"
        : "class";

    // ------------------------------------------------------------------ Documentation IDs

    public static string TypeId(Type type) => "T:" + TypeIdName(type);

    public static string MemberId(MemberInfo member)
    {
        var owner = TypeIdName(member.DeclaringType!);
        return member switch
        {
            ConstructorInfo ctor => $"M:{owner}.#ctor{ParameterList(ctor.GetParameters())}",
            MethodInfo method => $"M:{owner}.{method.Name}{(method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "")}{ParameterList(method.GetParameters())}"
                + (method.Name is "op_Implicit" or "op_Explicit" ? "~" + IdTypeName(method.ReturnType) : ""),
            PropertyInfo property => $"P:{owner}.{property.Name}{ParameterList(property.GetIndexParameters())}",
            EventInfo @event => $"E:{owner}.{@event.Name}",
            FieldInfo field => $"F:{owner}.{field.Name}",
            _ => throw new NotSupportedException(member.GetType().Name),
        };
    }

    /// <summary>Name in documentation IDs: namespace, nested types joined with '.', generic arity as `n.</summary>
    private static string TypeIdName(Type type)
    {
        var name = type.Name;
        if (type.IsNested)
            return TypeIdName(type.DeclaringType!) + "." + name;
        return string.IsNullOrEmpty(type.Namespace) ? name : type.Namespace + "." + name;
    }

    private static string ParameterList(ParameterInfo[] parameters) =>
        parameters.Length == 0 ? "" : "(" + string.Join(",", parameters.Select(p => IdTypeName(p.ParameterType))) + ")";

    private static string IdTypeName(Type type)
    {
        if (type.IsGenericParameter)
            return (type.DeclaringMethod != null ? "``" : "`") + type.GenericParameterPosition;
        if (type.IsByRef)
            return IdTypeName(type.GetElementType()!) + "@";
        if (type.IsPointer)
            return IdTypeName(type.GetElementType()!) + "*";
        if (type.IsArray)
        {
            var rank = type.GetArrayRank();
            return IdTypeName(type.GetElementType()!) + (rank == 1 ? "[]" : "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]");
        }
        if (type.IsGenericType)
        {
            var definition = TypeIdName(type.GetGenericTypeDefinition());
            definition = definition[..definition.LastIndexOf('`')];
            return definition + "{" + string.Join(",", type.GetGenericArguments().Select(IdTypeName)) + "}";
        }
        return TypeIdName(type);
    }

    // ------------------------------------------------------------------ C# names and signatures

    private static readonly Dictionary<Type, string> s_keywords = new()
    {
        [typeof(void)] = "void", [typeof(bool)] = "bool", [typeof(byte)] = "byte", [typeof(sbyte)] = "sbyte",
        [typeof(short)] = "short", [typeof(ushort)] = "ushort", [typeof(int)] = "int", [typeof(uint)] = "uint",
        [typeof(long)] = "long", [typeof(ulong)] = "ulong", [typeof(float)] = "float", [typeof(double)] = "double",
        [typeof(decimal)] = "decimal", [typeof(char)] = "char", [typeof(string)] = "string", [typeof(object)] = "object",
        [typeof(nint)] = "nint", [typeof(nuint)] = "nuint",
    };

    /// <summary>C# name of a type definition, e.g. "ComboBox&lt;T&gt;" or "DrawingContext.ClipScope".</summary>
    public static string DisplayName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick] + "<" + string.Join(", ", type.GetGenericArguments().Skip(type.DeclaringType?.GetGenericArguments().Length ?? 0).Select(a => a.Name)) + ">";
        return type.IsNested ? DisplayName(type.DeclaringType!) + "." + name : name;
    }

    /// <summary>File name of a type's page: "Doqua.Controls.ComboBox-1.html".</summary>
    public static string PageName(Type type) => TypeIdName(type).Replace('`', '-') + ".html";

    public static NullabilityInfo? Nullability(ParameterInfo parameter) => Try(() => s_nullability.Create(parameter));

    public static NullabilityInfo? Nullability(PropertyInfo property) => Try(() => s_nullability.Create(property));

    public static NullabilityInfo? Nullability(FieldInfo field) => Try(() => s_nullability.Create(field));

    public static NullabilityInfo? Nullability(EventInfo @event) => Try(() => s_nullability.Create(@event));

    private static NullabilityInfo? Try(Func<NullabilityInfo> create)
    {
        try
        {
            return create();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>C# spelling of a type in a signature; <paramref name="link"/> turns a type into (HTML-encoded) markup.</summary>
    public static string TypeName(Type type, NullabilityInfo? nullability, Func<Type, string, string> link)
    {
        if (type.IsByRef)
            return TypeName(type.GetElementType()!, nullability?.ElementType ?? nullability, link);
        if (Nullable.GetUnderlyingType(type) is { } underlying)
            return TypeName(underlying, nullability?.GenericTypeArguments.FirstOrDefault(), link) + "?";
        var nullable = !type.IsValueType && nullability is { ReadState: NullabilityState.Nullable } ? "?" : "";
        if (type.IsArray)
            return TypeName(type.GetElementType()!, nullability?.ElementType, link) + "[" + new string(',', type.GetArrayRank() - 1) + "]" + nullable;
        if (s_keywords.TryGetValue(type, out var keyword))
            return link(type, keyword) + nullable;
        if (type.IsGenericParameter)
            return type.Name + nullable;
        if (type.IsGenericType)
        {
            var arguments = type.GetGenericArguments();
            if (type.FullName?.StartsWith("System.ValueTuple`") == true)
                return "(" + string.Join(", ", arguments.Select((a, i) => TypeName(a, nullability?.GenericTypeArguments.ElementAtOrDefault(i), link))) + ")" + nullable;
            var definition = type.GetGenericTypeDefinition();
            var name = definition.Name[..definition.Name.IndexOf('`')];
            return link(definition, name) + "&lt;" + string.Join(", ", arguments.Select((a, i) => TypeName(a, nullability?.GenericTypeArguments.ElementAtOrDefault(i), link))) + "&gt;" + nullable;
        }
        return link(type, type.IsNested ? DisplayName(type) : type.Name) + nullable;
    }

    public static string Accessibility(MemberInfo member) => IsProtected(member) ? "protected" : "public";

    /// <summary>"static", "abstract", "virtual", "override", "sealed override", "readonly", "const" as C# would show them.</summary>
    public static string Modifiers(MemberInfo member)
    {
        MethodInfo? method = member switch
        {
            MethodInfo m => m,
            PropertyInfo p => p.GetMethod ?? p.SetMethod,
            EventInfo e => e.AddMethod,
            _ => null,
        };
        if (member is FieldInfo field)
            return field.IsLiteral ? "const" : (field.IsStatic ? "static " : "") + (field.IsInitOnly ? "readonly" : "");
        if (method == null)
            return "";
        if (method.IsStatic)
            return "static";
        var isOverride = method.GetBaseDefinition().DeclaringType != method.DeclaringType;
        if (method.IsAbstract && !method.DeclaringType!.IsInterface)
            return isOverride ? "abstract override" : "abstract";
        if (isOverride)
            return method.IsFinal ? "sealed override" : "override";
        if (method.IsVirtual && !method.IsFinal && !method.DeclaringType!.IsInterface)
            return "virtual";
        return "";
    }

    public static string DefaultValue(ParameterInfo parameter)
    {
        var value = parameter.RawDefaultValue;
        var type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
        return value switch
        {
            null => type.IsValueType && Nullable.GetUnderlyingType(parameter.ParameterType) == null ? "default" : "null",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            _ when type.IsEnum => FormatEnum(type, value),
            float f => f.ToString(System.Globalization.CultureInfo.InvariantCulture) + "f",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "",
        };
    }

    private static string FormatEnum(Type type, object value)
    {
        var name = Enum.GetName(type, value);
        return name != null ? type.Name + "." + name : $"({type.Name}){value}";
    }
}
