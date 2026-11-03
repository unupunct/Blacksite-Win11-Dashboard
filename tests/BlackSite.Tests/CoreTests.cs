using System.Diagnostics;
using BlackSite.Core.Monitoring;
using BlackSite.Core.Power;
using BlackSite.Core.Settings;
using BlackSite.Core.Shortcuts;
using Xunit;

namespace BlackSite.Tests;

internal sealed class CapturingStarter : IProcessStarter
{
    public List<ProcessStartInfo> Started { get; } = new();
    public void Start(ProcessStartInfo psi) => Started.Add(psi);
}

public class PowerTests
{
    [Theory]
    [InlineData(PowerAction.Shutdown)]
    [InlineData(PowerAction.Restart)]
    public void Unconfirmed_never_executes(PowerAction a)
    {
        var s = new CapturingStarter();
        var r = new PowerManager(s).Execute(a, confirmed: false);
        Assert.False(r.Success);
        Assert.Empty(s.Started);
    }

    [Fact]
    public void Confirmed_uses_shutdown_exe_with_right_flags()
    {
        var s = new CapturingStarter();
        new PowerManager(s).Execute(PowerAction.Shutdown, true);
        new PowerManager(s).Execute(PowerAction.Restart, true);
        Assert.Equal(2, s.Started.Count);
        Assert.EndsWith("shutdown.exe", s.Started[0].FileName);
        Assert.Contains("/s", s.Started[0].Arguments);
        Assert.Contains("/r", s.Started[1].Arguments);
    }
}

public class ShortcutTests
{
    [Fact]
    public void Default_catalogue_has_ten_apps_with_unique_ids()
    {
        var apps = DefaultShortcuts.Apps();
        Assert.Equal(10, apps.Count);
        Assert.Equal(10, apps.Select(a => a.Id).Distinct().Count());
        Assert.All(apps, a => Assert.True(ShortcutValidator.ValidateShortcut(a, false).IsValid, a.Name));
    }

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("calc.exe")]
    [InlineData("taskmgr.exe")]
    [InlineData("explorer.exe")]
    public void Core_windows_apps_resolve(string exe) => Assert.NotNull(ExecutableResolver.Resolve(exe));

    [Fact]
    public void System_shortcuts_are_available_and_use_expected_targets()
    {
        var launcher = new ApplicationLauncher(new CapturingStarter());
        var sys = DefaultShortcuts.System();
        Assert.All(sys, s => Assert.True(launcher.IsAvailable(s), s.Name));
        Assert.Contains("MyComputerFolder", sys[0].Targets[0].Arguments);
        Assert.Contains("ED7BA470-8E54-465E-825C-99712043E01C", sys[2].Targets[0].Arguments);
    }

    [Theory]
    [InlineData(@"C:\x\evil.bat")]
    [InlineData(@"C:\x\evil.ps1")]
    [InlineData("cmd.exe /c calc")]
    [InlineData("")]
    public void Dangerous_or_empty_executables_rejected(string path)
    {
        Assert.False(ShortcutValidator.ValidateTarget(new LaunchTarget(TargetKind.Executable, path), false).IsValid);
    }

    [Fact]
    public void Custom_arguments_rejected_but_trusted_ones_allowed()
    {
        Assert.False(ShortcutValidator.ValidateTarget(new LaunchTarget(TargetKind.Executable, "notepad.exe", "c:\\secret.txt"), false).IsValid);
        Assert.True(ShortcutValidator.ValidateTarget(new LaunchTarget(TargetKind.Executable, "explorer.exe", "shell:MyComputerFolder"), false).IsValid);
    }

    [Theory]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///c:/windows/system32/cmd.exe", false)]
    [InlineData("ms-settings:network", true)]
    [InlineData("https://example.com", true)]
    [InlineData("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", true)]
    [InlineData("ms-settings:x&calc", false)]
    public void Uri_allow_list(string uri, bool ok) =>
        Assert.Equal(ok, ShortcutValidator.ValidateTarget(new LaunchTarget(TargetKind.Uri, uri), false).IsValid);

    [Fact]
    public void Unavailable_app_reports_failure_without_throwing()
    {
        var s = new ShortcutDefinition
        {
            Name = "Ghost", Description = "x",
            Targets = { new LaunchTarget(TargetKind.Executable, @"C:\definitely\not\here.exe") },
        };
        var starter = new CapturingStarter();
        var l = new ApplicationLauncher(starter);
        Assert.False(l.IsAvailable(s));
        Assert.False(l.Launch(s).Success);
        Assert.Empty(starter.Started);
    }

    [Fact]
    public void Fallback_candidate_is_used_when_first_is_missing()
    {
        var s = new ShortcutDefinition
        {
            Name = "Fallback", Description = "x",
            Targets =
            {
                new LaunchTarget(TargetKind.Executable, @"C:\definitely\not\here.exe"),
                new LaunchTarget(TargetKind.Executable, "notepad.exe"),
            },
        };
        var starter = new CapturingStarter();
        Assert.True(new ApplicationLauncher(starter).Launch(s).Success);
        Assert.EndsWith("notepad.exe", starter.Started.Single().FileName, StringComparison.OrdinalIgnoreCase);
    }

    private static (ShortcutManager mgr, SettingsManager sm, string dir) NewManager()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid().ToString("N"));
        var sm = new SettingsManager(Path.Combine(dir, "settings.json"));
        sm.Load();
        return (new ShortcutManager(sm), sm, dir);
    }

    [Fact]
    public void Add_remove_move_replace_persist_and_restore()
    {
        var (mgr, sm, dir) = NewManager();
        try
        {
            var np = ExecutableResolver.Resolve("notepad.exe")!;
            Assert.True(mgr.Add(new ShortcutDefinition { Name = "Mine", Description = "Custom", Targets = { new LaunchTarget(TargetKind.Executable, np) } }).IsValid);
            Assert.Equal(11, mgr.Items.Count);

            var reloaded = new SettingsManager(Path.Combine(dir, "settings.json"));
            reloaded.Load();
            Assert.Equal(11, new ShortcutManager(reloaded).Items.Count);

            mgr.Move(10, 0);
            Assert.Equal("Mine", mgr.Items[0].Name);

            var id = mgr.Items[0].Id;
            Assert.True(mgr.Replace(id, new ShortcutDefinition { Name = "Renamed", Description = "d", Targets = { new LaunchTarget(TargetKind.Executable, np) } }).IsValid);
            Assert.Equal("Renamed", mgr.Items[0].Name);

            Assert.True(mgr.Remove(id));
            Assert.Equal(10, mgr.Items.Count);

            mgr.Add(new ShortcutDefinition { Name = "Again", Description = "", Targets = { new LaunchTarget(TargetKind.Executable, np) } });
            mgr.RestoreDefaults();
            Assert.Equal(10, mgr.Items.Count);
            Assert.Equal("Notepad", mgr.Items[0].Name);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Adding_missing_file_is_rejected()
    {
        var (mgr, _, dir) = NewManager();
        try
        {
            var r = mgr.Add(new ShortcutDefinition { Name = "Bad", Targets = { new LaunchTarget(TargetKind.Executable, @"C:\nope\nope.exe") } });
            Assert.False(r.IsValid);
            Assert.Equal(10, mgr.Items.Count);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Tampered_settings_shortcuts_are_dropped_on_load()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "settings.json"),
                """{"Shortcuts":[{"Name":"Evil","Targets":[{"Kind":"Executable","Value":"cmd.exe","Arguments":"/c calc"}]},{"Name":"Ok","Description":"","Targets":[{"Kind":"Executable","Value":"notepad.exe"}]}]}""");
            var sm = new SettingsManager(Path.Combine(dir, "settings.json"));
            sm.Load();
            var items = new ShortcutManager(sm).Items;
            Assert.Single(items);
            Assert.Equal("Ok", items[0].Name);
        }
        finally { Directory.Delete(dir, true); }
    }
}

public class SettingsTests
{
    [Fact]
    public void Round_trip_and_corrupt_file_recovery()
    {
        var dir = Path.Combine(Path.GetTempPath(), "bs-test-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(dir, "settings.json");
        try
        {
            var sm = new SettingsManager(file);
            sm.Load();
            sm.Current.AccentColor = "#FF8800";
            sm.Current.PanelOpacity = 0.8;
            sm.Current.AlwaysOnTop = true;
            sm.Current.WindowWidth = 1111;
            Assert.True(sm.Save());

            var sm2 = new SettingsManager(file);
            var s = sm2.Load();
            Assert.Equal("#FF8800", s.AccentColor);
            Assert.Equal(0.8, s.PanelOpacity);
            Assert.True(s.AlwaysOnTop);
            Assert.Equal(1111, s.WindowWidth);

            File.WriteAllText(file, "{ not json");
            var s3 = new SettingsManager(file).Load();
            Assert.Equal("#00E5FF", s3.AccentColor);
            Assert.True(File.Exists(file + ".bad"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Normalize_clamps_out_of_range_values()
    {
        var s = new AppSettings { PanelOpacity = 9, AnimationIntensity = -3, MasterVolume = 50, AccentColor = "red", BackgroundStyle = "x", WindowWidth = 10 };
        s.Normalize();
        Assert.Equal(1.0, s.PanelOpacity);
        Assert.Equal(0, s.AnimationIntensity);
        Assert.Equal(1.0, s.MasterVolume);
        Assert.Equal("#00E5FF", s.AccentColor);
        Assert.Equal("Circuit", s.BackgroundStyle);
        Assert.Equal(1440, s.WindowWidth);
    }
}

public class MonitorTests
{
    [Fact]
    public void Mbps_math()
    {
        Assert.Equal(8.0, NetworkMonitor.ComputeMbps(0, 1_000_000, 1), 6);
        Assert.Equal(4.0, NetworkMonitor.ComputeMbps(0, 1_000_000, 2), 6);
        Assert.Equal(0, NetworkMonitor.ComputeMbps(500, 100, 1)); // counter reset
        Assert.Equal(0, NetworkMonitor.ComputeMbps(0, 100, 0));
    }

    [Fact]
    public void Real_snapshot_is_sane()
    {
        using var m = new SystemMonitor();
        m.Sample();
        Thread.Sleep(400);
        var s = m.Sample();
        Assert.InRange(s.CpuPercent, 0, 100);
        Assert.InRange(s.MemoryPercent, 1, 100);
        Assert.True(s.MemoryTotalBytes > 1UL << 30);
        Assert.NotNull(s.Disk);
        Assert.True(s.Disk!.TotalBytes > s.Disk.FreeBytes);
        Assert.Equal(s.Disk.TotalBytes, s.Disk.UsedBytes + s.Disk.FreeBytes);
        Assert.True(s.Network.DownMbps >= 0 && s.Network.UpMbps >= 0);
    }
}
