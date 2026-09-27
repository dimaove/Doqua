using Doqua.GUI.Platform.Win32;
using Doqua.GUI.Platform.X11;

namespace Doqua.GUI.Platform;

internal static class PlatformFactory
{
    public static IPlatform Create()
    {
        if (OperatingSystem.IsWindows())
            return new Win32Platform();
        if (OperatingSystem.IsLinux())
            return new X11Platform();
        throw new PlatformNotSupportedException(Localization.Get("Doqua.Error.UnsupportedPlatform"));
    }
}
