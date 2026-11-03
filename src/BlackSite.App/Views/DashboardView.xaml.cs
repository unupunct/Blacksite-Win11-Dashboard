using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using BlackSite.App.Dialogs;
using BlackSite.App.Services;
using BlackSite.Core.Monitoring;
using BlackSite.Core.Power;
using BlackSite.Core.Shortcuts;

namespace BlackSite.App.Views;

internal static class Fmt
{
    public static string Bytes(ulong b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i >= 3 ? $"{v:0.0} {u[i]}" : $"{v:0} {u[i]}";
    }
}

public partial class DashboardView : UserControl
{
    private ShortcutManager _mgr = null!;
    private ApplicationLauncher _launcher = null!;
    private PowerManager _power = null!;
    private Action<string, bool> _toast = (_, _) => { };

    public DashboardView()
    {
        InitializeComponent();
        CpuCores.Text = Environment.ProcessorCount.ToString();
    }

    public void Initialize(ShortcutManager mgr, ApplicationLauncher launcher, PowerManager power, Action<string, bool> toast)
    {
        _mgr = mgr; _launcher = launcher; _power = power; _toast = toast;
        var red = (Brush)FindResource("RedBrush");
        var orange = (Brush)FindResource("OrangeBrush");
        SysList.ItemsSource = new List<SysItem>
        {
            new() { Id = "sys.computer", Title = "MY COMPUTER", Description = "Explore your files", Glyph = "" },
            new() { Id = "sys.control", Title = "CONTROL PANEL", Description = "System settings", Glyph = "" },
            new() { Id = "sys.godmode", Title = "GOD MODE", Description = "Advanced settings", Glyph = "" },
            new() { Id = "power.shutdown", Title = "SHUT DOWN", Description = "Close system", Glyph = "", IconBrush = red },
            new() { Id = "power.restart", Title = "RESTART", Description = "Reboot system", Glyph = "", IconBrush = orange },
        };
        _mgr.Changed += RefreshShortcuts;
        RefreshShortcuts();
    }

    private void PageScroll_SizeChanged(object sender, SizeChangedEventArgs e) =>
        Root.Height = Math.Max(560, PageScroll.ViewportHeight > 0 ? PageScroll.ViewportHeight : e.NewSize.Height);

    public void RefreshShortcuts()
    {
        var defs = _mgr.Items.ToList();
        var items = defs.Select(d => new ShortcutItem(d, true)).ToList();
        AppList.ItemsSource = items;
        // Availability probing touches the file system / registry, so keep it off the UI thread.
        Task.Run(() => defs.Select(_launcher.IsAvailable).ToList()).ContinueWith(t =>
        {
            if (t.IsFaulted) return;
            Dispatcher.BeginInvoke(() =>
            {
                if (!ReferenceEquals(AppList.ItemsSource, items)) return;
                for (int i = 0; i < items.Count; i++) items[i].IsAvailable = t.Result[i];
            });
        });
    }

    private void Sys_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not SysItem item) return;
        AudioManager.Play(SoundKind.Click);
        BlackSite.Core.Logging.Log.Info($"System button: {item.Id}");
        var owner = Window.GetWindow(this);
        switch (item.Id)
        {
            case "power.shutdown":
                if (ConfirmDialog.Ask(owner, "Shut down this PC?", "Windows will shut down immediately. Save your work in other programs first.", "SHUT DOWN", ""))
                    Report(_power.Execute(PowerAction.Shutdown, confirmed: true));
                break;
            case "power.restart":
                if (ConfirmDialog.Ask(owner, "Restart this PC?", "Windows will restart immediately. Save your work in other programs first.", "RESTART", ""))
                    Report(_power.Execute(PowerAction.Restart, confirmed: true));
                break;
            default:
                var def = DefaultShortcuts.System().First(s => s.Id == item.Id);
                AudioManager.Play(SoundKind.Launch);
                Report(_launcher.Launch(def));
                break;
        }
    }

    private void Report(LaunchResult r)
    {
        if (!r.Success) { AudioManager.Play(SoundKind.Warning); _toast(r.Message, true); }
        else _toast(r.Message, false);
    }

    private void App_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not ShortcutItem item) return;
        AudioManager.Play(SoundKind.Click);
        var owner = Window.GetWindow(this);

        if (!_launcher.IsAvailable(item.Definition))
        {
            item.IsAvailable = false;
            if (!ConfirmDialog.Ask(owner, $"{item.Name} not found", $"{item.Name} could not be found on this PC. Do you want to choose its program manually?", "CHOOSE PROGRAM", "", false)) return;
            var dlg = new ShortcutEditDialog(item.Definition) { Owner = owner };
            if (dlg.ShowDialog() == true && dlg.Result is not null)
            {
                var v = _mgr.Replace(item.Definition.Id, dlg.Result);
                if (!v.IsValid) _toast(v.Message, true);
            }
            return;
        }

        AudioManager.Play(SoundKind.Launch);
        var res = _launcher.Launch(item.Definition);
        Report(res);
        if (!res.Success) return;
        item.IsLaunching = true;
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        t.Tick += (_, _) => { item.IsLaunching = false; t.Stop(); };
        t.Start();
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        new ShortcutEditorWindow(_mgr) { Owner = Window.GetWindow(this) }.ShowDialog();
        RefreshShortcuts();
    }

    public void Update(SystemSnapshot s, IReadOnlyList<SystemSnapshot> history)
    {
        CpuGauge.Value = s.CpuPercent;
        CpuState.Text = s.CpuPercent < 30 ? "IDLE" : s.CpuPercent < 75 ? "NORMAL" : s.CpuPercent < 90 ? "HIGH" : "CRITICAL";
        CpuState.Foreground = StateBrush(s.CpuPercent);
        CpuGraph.SetValues(history.Select(h => h.CpuPercent).ToList());

        MemGauge.Value = s.MemoryPercent;
        MemUsed.Text = Fmt.Bytes(s.MemoryUsedBytes);
        MemFree.Text = Fmt.Bytes(s.MemoryTotalBytes - s.MemoryUsedBytes);
        MemTotal.Text = Fmt.Bytes(s.MemoryTotalBytes);
        MemGraph.SetValues(history.Select(h => h.MemoryPercent).ToList());

        if (s.Disk is { } d)
        {
            DiskPanel.Header = $"DISK USAGE ({d.Drive})";
            DiskGauge.Value = d.Percent;
            DiskUsed.Text = Fmt.Bytes(d.UsedBytes);
            DiskFree.Text = Fmt.Bytes(d.FreeBytes);
            DiskTotal.Text = Fmt.Bytes(d.TotalBytes);
        }
        else
        {
            DiskUsed.Text = DiskFree.Text = DiskTotal.Text = "N/A";
        }

        NetDown.Text = s.Network.DownMbps.ToString("0.00");
        NetUp.Text = s.Network.UpMbps.ToString("0.00");
        NetStatus.Text = s.Network.Connected ? s.Network.Adapter ?? "" : "DISCONNECTED";
        NetStatus.Foreground = s.Network.Connected ? (Brush)FindResource("TextDimBrush") : (Brush)FindResource("OrangeBrush");
        NetGraph.SetValues(history.Select(h => h.Network.DownMbps).ToList(), history.Select(h => h.Network.UpMbps).ToList());
    }

    private Brush StateBrush(double pct) => (Brush)FindResource(pct >= 90 ? "RedBrush" : pct >= 75 ? "OrangeBrush" : "GreenBrush");
}
