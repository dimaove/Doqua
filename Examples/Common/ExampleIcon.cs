using System.Reflection;
using Doqua.GUI;

/// <summary>
/// The Doqua logo in 16, 32 and 256 pixels, for <see cref="Window.Icons"/>. The PNGs are embedded
/// into every example from Docs/ by Examples/Directory.Build.props.
/// </summary>
static class ExampleIcon
{
    public static IReadOnlyList<Bitmap> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        return [.. new[] { 16, 32, 256 }.Select(size =>
        {
            var name = $"Doqua.Logo.{size}.png";
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Embedded resource '{name}' not found.");
            return Bitmap.Load(stream);
        })];
    }
}
