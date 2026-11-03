using BlackSite.Core.Logging;

namespace BlackSite.Core.Platform;

/// <summary>Launch-at-login via a shortcut in the user's Startup folder (no registry changes).</summary>
public static class StartupManager
{
    public const string StartupArg = "--startup";
    private static string LinkPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "BlackSite.lnk");

    public static bool IsEnabled => File.Exists(LinkPath);

    public static bool Set(bool enabled, string exePath)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(LinkPath)) File.Delete(LinkPath);
                return true;
            }
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return false;
            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(LinkPath);
            link.TargetPath = exePath;
            link.Arguments = StartupArg;
            link.WorkingDirectory = Path.GetDirectoryName(exePath);
            link.Description = "BLACKSITE System Control Panel";
            link.Save();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not update startup entry", ex);
            return false;
        }
    }
}
