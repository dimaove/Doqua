namespace Doqua.Core;

public static class DoquaInfo
{
    public const string Name = "Doqua";
    public static Version Version => typeof(DoquaInfo).Assembly.GetName().Version ?? new Version(0, 0);
}
