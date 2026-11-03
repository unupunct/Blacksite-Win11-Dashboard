namespace BlackSite.Core.Settings;

/// <summary>Resolves where settings and logs live. A "portable.flag" file beside the exe selects portable mode.</summary>
public static class AppPaths
{
    public static bool IsPortable { get; } = File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag"));

    public static string DataDir { get; } = IsPortable
        ? Path.Combine(AppContext.BaseDirectory, "data")
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BlackSite");

    public static string LogDir => Path.Combine(DataDir, "logs");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
}
