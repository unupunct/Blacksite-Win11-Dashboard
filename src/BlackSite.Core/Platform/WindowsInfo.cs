using System.Diagnostics;
using Microsoft.Win32;

namespace BlackSite.Core.Platform;

public sealed record ProcessUsage(string Name, long WorkingSetBytes);

public sealed record WindowsInfo(string Edition, string Version, string Build, string Machine, string User, string Cpu, int LogicalCores)
{
    public string Display => $"{Edition} {Version}".Trim();

    public static WindowsInfo Read()
    {
        string edition = "Windows", version = "", build = Environment.OSVersion.Version.Build.ToString(), cpu = "Unknown CPU";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            edition = k?.GetValue("ProductName") as string ?? edition;
            version = k?.GetValue("DisplayVersion") as string ?? k?.GetValue("ReleaseId") as string ?? "";
            var b = k?.GetValue("CurrentBuild") as string ?? build;
            var ubr = k?.GetValue("UBR");
            build = ubr is null ? b : $"{b}.{ubr}";
            // ProductName still says "Windows 10" on Windows 11.
            if (int.TryParse(b, out var bn) && bn >= 22000) edition = edition.Replace("Windows 10", "Windows 11");
        }
        catch { /* leave defaults */ }
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            cpu = (k?.GetValue("ProcessorNameString") as string)?.Trim() ?? cpu;
        }
        catch { }
        return new WindowsInfo(edition, version, build, Environment.MachineName, Environment.UserName, cpu, Environment.ProcessorCount);
    }

    public static TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    /// <summary>Largest processes by working set (names grouped).</summary>
    public static (int ProcessCount, List<ProcessUsage> Top) TopProcesses(int count)
    {
        var procs = Process.GetProcesses();
        try
        {
            var top = procs.Select(p =>
                {
                    try { return new ProcessUsage(p.ProcessName, p.WorkingSet64); }
                    catch { return new ProcessUsage(p.ProcessName, 0); }
                })
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => new ProcessUsage(g.Key, g.Sum(x => x.WorkingSetBytes)))
                .OrderByDescending(p => p.WorkingSetBytes)
                .Take(count)
                .ToList();
            return (procs.Length, top);
        }
        finally { foreach (var p in procs) p.Dispose(); }
    }
}
