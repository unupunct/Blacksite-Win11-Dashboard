using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using BlackSite.Core.Logging;
using BlackSite.Core.Monitoring;
using BlackSite.Core.Platform;
using BlackSite.Core.Settings;

namespace BlackSite.App.Views;

public partial class InfoView : UserControl
{
    public InfoView()
    {
        InitializeComponent();
        var info = WindowsInfo.Read();
        var snap = new SystemMonitor().Sample();
        var ver = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"BLACKSITE — SYSTEM CONTROL PANEL  v{ver}";
        Facts.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("WINDOWS", info.Edition),
            new("VERSION / BUILD", $"{info.Version}  (build {info.Build})"),
            new("COMPUTER NAME", info.Machine),
            new("USER", info.User),
            new("PROCESSOR", info.Cpu),
            new("LOGICAL CORES", info.LogicalCores.ToString()),
            new("INSTALLED MEMORY", Fmt.Bytes(snap.MemoryTotalBytes)),
            new("SYSTEM DRIVE", snap.Disk is { } d ? $"{d.Drive}  {Fmt.Bytes(d.TotalBytes)}  ({Fmt.Bytes(d.FreeBytes)} free)" : "N/A"),
            new("DATA FOLDER", AppPaths.DataDir + (AppPaths.IsPortable ? "  (portable)" : "")),
        };
        Keys.ItemsSource = new List<KeyValuePair<string, string>>
        {
            new("F11", "Toggle fullscreen"),
            new("Ctrl + 1…8", "Switch pages"),
            new("Tab / Enter", "Move between and activate buttons"),
            new("Esc", "Cancel a dialog"),
        };
    }

    private static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { Log.Warn($"Could not open {path}", ex); }
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        Open(AppPaths.DataDir);
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var f = Log.FilePath;
        if (f is not null && File.Exists(f)) Open(f);
        else Open(AppPaths.LogDir);
    }
}
