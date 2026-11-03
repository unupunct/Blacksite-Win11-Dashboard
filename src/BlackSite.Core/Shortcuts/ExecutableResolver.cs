using Microsoft.Win32;

namespace BlackSite.Core.Shortcuts;

/// <summary>Finds an installed executable by name or path without launching anything.</summary>
public static class ExecutableResolver
{
    public static string? Resolve(string nameOrPath)
    {
        if (string.IsNullOrWhiteSpace(nameOrPath)) return null;
        var expanded = Environment.ExpandEnvironmentVariables(nameOrPath.Trim().Trim('"'));
        if (expanded.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || expanded.Contains('*') || expanded.Contains('?')) return null;

        if (Path.IsPathRooted(expanded)) return File.Exists(expanded) ? expanded : null;
        if (expanded.Contains('\\') || expanded.Contains('/')) return null; // relative paths are never resolved

        var dirs = new List<string> { Environment.SystemDirectory, Environment.GetFolderPath(Environment.SpecialFolder.Windows) };
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries));
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"));
        foreach (var d in dirs)
        {
            try
            {
                var p = Path.Combine(d.Trim('"'), expanded);
                if (File.Exists(p)) return p;
            }
            catch { /* bad PATH entry */ }
        }
        return AppPathsRegistry(expanded);
    }

    private static string? AppPathsRegistry(string exe)
    {
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var k = hive.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                var v = k?.GetValue(null) as string;
                if (!string.IsNullOrWhiteSpace(v))
                {
                    var p = Environment.ExpandEnvironmentVariables(v.Trim('"'));
                    if (File.Exists(p)) return p;
                }
            }
            catch { /* ignore */ }
        }
        return null;
    }

    /// <summary>True if the URI scheme has a registered handler (or is a shell: / web URI).</summary>
    public static bool IsSchemeRegistered(string scheme)
    {
        if (scheme is "shell" or "http" or "https") return true;
        try
        {
            using var k = Registry.ClassesRoot.OpenSubKey(scheme);
            return k?.GetValue("URL Protocol") is not null;
        }
        catch { return false; }
    }
}
