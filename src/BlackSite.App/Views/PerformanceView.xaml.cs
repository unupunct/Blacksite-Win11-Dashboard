using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BlackSite.Core.Monitoring;
using BlackSite.Core.Platform;

namespace BlackSite.App.Views;

public partial class PerformanceView : UserControl
{
    private sealed record Proc(string Name, string Size, double Fraction);

    private readonly DispatcherTimer _procTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private DateTime _lastUpdate = DateTime.MinValue;

    public PerformanceView()
    {
        InitializeComponent();
        var info = WindowsInfo.Read();
        CpuName.Text = info.Cpu;
        Cores.Text = info.LogicalCores.ToString();
        _procTimer.Tick += async (_, _) => await RefreshProcesses();
        // Process enumeration is the only costly call here, so it runs only while this page is visible.
        IsVisibleChanged += async (_, e) =>
        {
            if ((bool)e.NewValue) { await RefreshProcesses(); _procTimer.Start(); }
            else _procTimer.Stop();
        };
    }

    private async Task RefreshProcesses()
    {
        try
        {
            var (count, top) = await Task.Run(() => WindowsInfo.TopProcesses(8));
            ProcCount.Text = count.ToString();
            Uptime.Text = FormatUptime(WindowsInfo.Uptime);
            long max = Math.Max(1, top.Select(p => p.WorkingSetBytes).DefaultIfEmpty(1).Max());
            TopList.ItemsSource = top.Select(p => new Proc(p.Name, Fmt.Bytes((ulong)p.WorkingSetBytes), (double)p.WorkingSetBytes / max)).ToList();
        }
        catch (Exception ex) { BlackSite.Core.Logging.Log.Warn("Process list unavailable", ex); }
    }

    private static string FormatUptime(TimeSpan t) => t.TotalDays >= 1 ? $"{(int)t.TotalDays}d {t.Hours}h {t.Minutes}m" : $"{t.Hours}h {t.Minutes}m {t.Seconds}s";

    public void Update(SystemSnapshot s, IReadOnlyList<SystemSnapshot> history)
    {
        if (!IsVisible) return;
        CpuText.Text = $"{s.CpuPercent:0}%";
        CpuText.Foreground = Level(s.CpuPercent);
        CpuGraph.SetValues(history.Select(h => h.CpuPercent).ToList());
        MemText.Text = $"{s.MemoryPercent:0}%";
        MemText.Foreground = Level(s.MemoryPercent);
        MemDetail.Text = $"{Fmt.Bytes(s.MemoryUsedBytes)} of {Fmt.Bytes(s.MemoryTotalBytes)}";
        MemGraph.SetValues(history.Select(h => h.MemoryPercent).ToList());
        NetDown.Text = $"▼ {s.Network.DownMbps:0.00} Mbps";
        NetUp.Text = $"▲ {s.Network.UpMbps:0.00} Mbps";
        NetAdapter.Text = s.Network.Connected ? s.Network.Adapter : "Disconnected";
        NetGraph.SetValues(history.Select(h => h.Network.DownMbps).ToList(), history.Select(h => h.Network.UpMbps).ToList());
        Uptime.Text = FormatUptime(WindowsInfo.Uptime);
    }

    private Brush Level(double pct) => (Brush)FindResource(pct >= 90 ? "RedBrush" : pct >= 75 ? "OrangeBrush" : "TextBrush");
}
