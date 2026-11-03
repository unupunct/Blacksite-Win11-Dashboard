using System.Runtime.InteropServices;
using System.Windows;
using BlackSite.Core.Logging;
using BlackSite.Core.Platform;
using BlackSite.Core.Settings;

namespace BlackSite.App;

public partial class App : Application
{
    private Mutex? _single;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log.Configure(AppPaths.LogDir);
        Log.Info($"BLACKSITE starting (portable={AppPaths.IsPortable})");

        _single = new Mutex(true, @"Local\BlackSite.SystemControlPanel", out bool first);
        if (!first)
        {
            ActivateExisting();
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unhandled UI exception", ex.Exception);
            MessageBox.Show($"An unexpected error occurred and was logged.\n\n{ex.Exception.Message}", "BLACKSITE", MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error("Fatal exception", ex.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ex) => { Log.Error("Unobserved task exception", ex.Exception); ex.SetObserved(); };

        var sm = new SettingsManager(AppPaths.SettingsFile);
        sm.Load();

        bool fromStartup = e.Args.Contains(StartupManager.StartupArg, StringComparer.OrdinalIgnoreCase);
        bool minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase) || (fromStartup && sm.Current.StartMinimized);

        // Keep the Windows startup entry in step with the setting (e.g. portable folder moved).
        if (sm.Current.LaunchAtStartup != StartupManager.IsEnabled) sm.Current.LaunchAtStartup = StartupManager.IsEnabled;

        var window = new MainWindow(sm, minimized);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info("BLACKSITE exiting");
        _single?.Dispose();
        base.OnExit(e);
    }

    private static void ActivateExisting()
    {
        var h = FindWindow(null, "BLACKSITE — SYSTEM CONTROL PANEL");
        if (h == IntPtr.Zero) return;
        ShowWindow(h, 9 /* SW_RESTORE */);
        SetForegroundWindow(h);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? cls, string title);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
}
