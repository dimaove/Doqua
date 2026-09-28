namespace Doqua.Core;

/// <summary>Information about the Doqua library.</summary>
public static class DoquaInfo
{
    /// <summary>The library's name.</summary>
    public const string Name = "Doqua";
    /// <summary>The library's assembly version.</summary>
    public static Version Version => typeof(DoquaInfo).Assembly.GetName().Version ?? new Version(0, 0);
}
