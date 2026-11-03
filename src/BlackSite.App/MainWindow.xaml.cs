using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using System.Windows.Threading;
using BlackSite.App.Services;
using BlackSite.App.Views;
using BlackSite.Core.Logging;
using BlackSite.Core.Monitoring;
using BlackSite.Core.Platform;
using BlackSite.Core.Power;
using BlackSite.Core.Settings;
using BlackSite.Core.Shortcuts;

namespace BlackSite.App;

public partial class MainWindow : Window
{
    private readonly SettingsManager _sm;
    private readonly ShortcutManager _shortcuts;
    private readonly SystemMonitor _monitor = new();
    private readonly Dictionary<string, UIElement> _pages = new();
    private readonly DispatcherTimer _clock = new();
    private readonly DispatcherTimer _toastTimer = new() { Interval = TimeSpan.FromSeconds(3.5) };
    private readonly WindowChrome _chrome = new() { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
    private DashboardView _dashboard = null!;
    private PerformanceView _performance = null!;
    private SettingsView _settingsView = null!;
    private WindowState _preFullState = WindowState.Normal;
    private bool _wasFull;
    private string _currentPage = "";

    private AppSettings S => _sm.Current;

    public MainWindow(SettingsManager sm, bool startMinimized)
    {
        InitializeComponent();
        _sm = sm;
        _shortcuts = new ShortcutManager(sm);

        var info = WindowsInfo.Read();
        OsText.Text = info.Display;
        BuildText.Text = $"BUILD {info.Build}";
        BuildActivityBars();

        RestoreGeometry();
        if (startMinimized) WindowState = WindowState.Minimized;

        _dashboard = new DashboardView();
        _dashboard.Initialize(_shortcuts, new ApplicationLauncher(), new PowerManager(), Notify);
        _performance = new PerformanceView();
        _settingsView = new SettingsView();
        _settingsView.Initialize(_sm, _shortcuts, ApplySettings, RestoreDefaultLayout);
        _pages["dashboard"] = _dashboard;
        _pages["performance"] = _performance;
        _pages["settings"] = _settingsView;
        _pages["info"] = new InfoView();
        _pages["computer"] = new FutureModuleView("", "Computer", "A detailed hardware and device overview is planned for a later release. Use the My Computer button on the Dashboard to browse your drives now.");
        _pages["network"] = new FutureModuleView("", "Network", "Adapter details and connection diagnostics are planned for a later release. Live download and upload speed is already shown on the Dashboard and Performance pages.");
        _pages["security"] = new FutureModuleView("", "Security", "A security overview is planned for a later release. BLACKSITE never changes Windows Defender, firewall or UAC settings.");
        _pages["files"] = new FutureModuleView("", "Files", "A built-in file browser is planned for a later release. Use the File Explorer shortcut on the Dashboard in the meantime.");

        ApplySettings(save: false);
        Navigate("dashboard", sound: false);

        AnimationManager.Changed += StartOrStopAnimations;
        SizeChanged += (_, _) => RestartScan();
        StateChanged += (_, _) => OnStateChanged();
        SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WndProc);
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        StartClock();
        _monitor.Updated += snap => Dispatcher.BeginInvoke(DispatcherPriority.Background, () => OnSnapshot(snap));
        _monitor.Start(TimeSpan.FromSeconds(1));
        _dashboard.Update(_monitor.Latest!, _monitor.History());
        StartOrStopAnimations();
        AudioManager.Play(SoundKind.Online);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveGeometry();
        _sm.Save();
        _monitor.Dispose();
    }

    // ---------------- monitoring ----------------
    private void OnSnapshot(SystemSnapshot snap)
    {
        if (WindowState == WindowState.Minimized) return; // nothing to draw
        var hist = _monitor.History();
        _dashboard.Update(snap, hist);
        _performance.Update(snap, hist);
    }

    // ---------------- navigation ----------------
    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded && _currentPage == "") return; // initial check is handled by the constructor
        Navigate(((RadioButton)sender).Uid, sound: true);
    }

    private void Navigate(string key, bool sound)
    {
        if (!_pages.TryGetValue(key, out var page) || key == _currentPage) return;
        _currentPage = key;
        Host.Content = page;
        foreach (RadioButton rb in NavGrid.Children) rb.IsChecked = rb.Uid == key;
        if (sound) AudioManager.Play(SoundKind.Navigation);
        if (AnimationManager.Enabled)
        {
            var move = new TranslateTransform(0, 14);
            page.RenderTransform = move;
            page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        else
        {
            page.BeginAnimation(OpacityProperty, null);
            page.Opacity = 1;
            page.RenderTransform = Transform.Identity;
        }
        if (key == "dashboard" && _monitor.Latest is { } s) _dashboard.Update(s, _monitor.History());
        if (key == "performance" && _monitor.Latest is { } p) _performance.Update(p, _monitor.History());
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key >= Key.D1 && e.Key <= Key.D8)
        {
            Navigate(((RadioButton)NavGrid.Children[e.Key - Key.D1]).Uid, true);
            e.Handled = true;
        }
    }

    // ---------------- toast ----------------
    public void Notify(string message, bool isError)
    {
        ToastText.Text = message;
        ToastText.Foreground = isError ? (Brush)FindResource("RedBrush") : (Brush)FindResource("TextBrush");
        Toast.BorderBrush = isError ? (Brush)FindResource("RedBrush") : (Brush)FindResource("AccentBrush");
        Toast.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Tick -= HideToast;
        _toastTimer.Tick += HideToast;
        _toastTimer.Start();
    }

    private void HideToast(object? s, EventArgs e) { Toast.Visibility = Visibility.Collapsed; _toastTimer.Stop(); }

    // ---------------- clock ----------------
    private void StartClock()
    {
        _clock.Tick += (_, _) => TickClock();
        TickClock();
        _clock.Start();
    }

    private void TickClock()
    {
        var now = DateTime.Now;
        ClockH.Text = now.ToString("HH");
        ClockM.Text = now.ToString("mm");
        ClockS.Text = now.ToString("ss");
        DateText.Text = now.ToString("dddd, d MMMM yyyy").ToUpperInvariant();
        _clock.Interval = TimeSpan.FromMilliseconds(1005 - now.Millisecond); // stay aligned to the second
    }

    // ---------------- settings application ----------------
    public void ApplySettings() => ApplySettings(save: true);

    private void ApplySettings(bool save)
    {
        ThemeManager.Apply(S);
        AnimationManager.Apply(S);
        AudioManager.Attach(S);
        Bg.StyleName = S.BackgroundStyle;
        Particles.StyleName = S.BackgroundStyle;
        ApplyWindowMode();
        RestartScan();
        if (save) _sm.Save();
    }

    private void ApplyWindowMode()
    {
        bool full = S.Fullscreen;
        bool borderless = S.Borderless || full;
        Topmost = S.AlwaysOnTop;
        WindowChrome.SetWindowChrome(this, borderless ? _chrome : null);
        WindowStyle = borderless ? WindowStyle.None : WindowStyle.SingleBorderWindow;
        ResizeMode = full ? ResizeMode.NoResize : ResizeMode.CanResize;
        CaptionButtons.Visibility = borderless && !full ? Visibility.Visible : Visibility.Collapsed;
        Shell.Margin = new Thickness(full ? 8 : 14);

        if (full && !_wasFull)
        {
            _preFullState = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
            if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal; // force WM_GETMINMAXINFO to re-run
            WindowState = WindowState.Maximized;
        }
        else if (!full && _wasFull)
        {
            WindowState = _preFullState;
        }
        _wasFull = full;
        FullBtn.Content = full ? "" : "";
        MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
    }

    private void ToggleFullscreen()
    {
        S.Fullscreen = !S.Fullscreen;
        ApplySettings();
        _settingsView.SyncWindowToggles();
        AudioManager.Play(SoundKind.Click);
    }

    private void RestoreDefaultLayout()
    {
        var d = new AppSettings();
        S.WindowWidth = d.WindowWidth; S.WindowHeight = d.WindowHeight;
        S.WindowLeft = null; S.WindowTop = null; S.WindowMaximized = false;
        S.Fullscreen = false;
        WindowState = WindowState.Normal;
        Width = d.WindowWidth; Height = d.WindowHeight;
        CenterOnCurrentScreen();
        ApplySettings();
        _settingsView.SyncWindowToggles();
    }

    // ---------------- window geometry ----------------
    private void RestoreGeometry()
    {
        Width = Math.Min(S.WindowWidth, SystemParameters.VirtualScreenWidth);
        Height = Math.Min(S.WindowHeight, SystemParameters.VirtualScreenHeight);
        if (S.WindowLeft is { } l && S.WindowTop is { } t && IsOnAnyScreen(new Rect(l, t, Width, Height)))
        {
            Left = l; Top = t;
        }
        else CenterOnCurrentScreen();
        if (S.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void CenterOnCurrentScreen()
    {
        var wa = SystemParameters.WorkArea;
        Left = wa.Left + Math.Max(0, (wa.Width - Width) / 2);
        Top = wa.Top + Math.Max(0, (wa.Height - Height) / 2);
    }

    /// <summary>True if enough of the rectangle is on the (possibly changed) multi-monitor desktop to grab it.</summary>
    private static bool IsOnAnyScreen(Rect r)
    {
        var virt = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var i = Rect.Intersect(r, virt);
        return !i.IsEmpty && i.Width >= 200 && i.Height >= 120 && r.Top >= virt.Top - 5;
    }

    private void SaveGeometry()
    {
        bool full = S.Fullscreen;
        var b = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (b.Width >= 800 && b.Height >= 560 && !b.IsEmpty)
        {
            S.WindowLeft = b.Left; S.WindowTop = b.Top; S.WindowWidth = b.Width; S.WindowHeight = b.Height;
        }
        S.WindowMaximized = full ? _preFullState == WindowState.Maximized : WindowState == WindowState.Maximized;
    }

    private void OnStateChanged() => MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";

    // ---------------- caption ----------------
    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (S.Fullscreen || e.ChangedButton != MouseButton.Left || !(S.Borderless)) return;
        if (e.OriginalSource is DependencyObject d && IsInside<Button>(d)) return;
        if (e.ClickCount == 2) Max_Click(sender, e);
        else if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { /* released early */ } }
    }

    private static bool IsInside<T>(DependencyObject d) where T : DependencyObject
    {
        while (d is not null) { if (d is T) return true; d = VisualTreeHelper.GetParent(d); }
        return false;
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Max_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Full_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();

    // ---------------- animations ----------------
    private readonly List<ScaleTransform> _bars = new();

    private void BuildActivityBars()
    {
        for (int i = 0; i < 6; i++)
        {
            var st = new ScaleTransform(1, 0.35);
            _bars.Add(st);
            Bars.Children.Add(new System.Windows.Shapes.Rectangle
            {
                Width = 3, Margin = new Thickness(1.5, 0, 1.5, 0), VerticalAlignment = VerticalAlignment.Bottom, Height = 16,
                RenderTransform = st, RenderTransformOrigin = new Point(0.5, 1), Fill = (Brush)FindResource("AccentBrush"),
            });
        }
    }

    private void StartOrStopAnimations()
    {
        bool on = AnimationManager.Enabled;
        double k = AnimationManager.Intensity;
        if (on)
        {
            OnlineDot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            foreach (var c in new[] { Colon1, Colon2 })
                c.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.2, TimeSpan.FromSeconds(0.5)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            for (int i = 0; i < _bars.Count; i++)
                _bars[i].BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.2, 1, TimeSpan.FromMilliseconds(420 + i * 130))
                    { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromMilliseconds(i * 90) });
        }
        else
        {
            OnlineDot.BeginAnimation(OpacityProperty, null);
            Colon1.BeginAnimation(OpacityProperty, null); Colon2.BeginAnimation(OpacityProperty, null);
            foreach (var b in _bars) { b.BeginAnimation(ScaleTransform.ScaleYProperty, null); b.ScaleY = 0.6; }
        }
        Particles.Refresh();
        RestartScan();
    }

    private void RestartScan()
    {
        ScanShift.BeginAnimation(TranslateTransform.YProperty, null);
        if (!AnimationManager.Enabled || AnimationManager.Intensity < 0.05 || ActualHeight < 100)
        {
            ScanLine.Visibility = Visibility.Collapsed;
            return;
        }
        var a = ThemeManager.Accent;
        double alpha = 0.10 + 0.35 * AnimationManager.Intensity;
        ScanLine.Fill = new LinearGradientBrush(new GradientStopCollection
        {
            new(Colors.Transparent, 0),
            new(Color.FromArgb((byte)(alpha * 255 * 0.5), a.R, a.G, a.B), 0.85),
            new(Color.FromArgb((byte)(alpha * 255), a.R, a.G, a.B), 0.97),
            new(Colors.Transparent, 1),
        }, new Point(0, 0), new Point(0, 1));
        ScanLine.Visibility = Visibility.Visible;
        ScanShift.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-150, ActualHeight + 10, TimeSpan.FromSeconds(9)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    // ---------------- maximize without covering the taskbar (borderless) ----------------
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0024 /* WM_GETMINMAXINFO */)
        {
            var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, 2 /* NEAREST */);
            if (monitor != IntPtr.Zero)
            {
                var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref info))
                {
                    var area = S.Fullscreen ? info.rcMonitor : info.rcWork; // fullscreen deliberately covers the taskbar
                    mmi.ptMaxPosition.x = area.left - info.rcMonitor.left;
                    mmi.ptMaxPosition.y = area.top - info.rcMonitor.top;
                    mmi.ptMaxSize.x = area.right - area.left;
                    mmi.ptMaxSize.y = area.bottom - area.top;
                    Marshal.StructureToPtr(mmi, lParam, true);
                }
            }
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO { public POINT ptReserved, ptMaxSize, ptMaxPosition, ptMinTrackSize, ptMaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
}
