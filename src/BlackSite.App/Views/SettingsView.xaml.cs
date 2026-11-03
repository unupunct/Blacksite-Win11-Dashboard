using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BlackSite.App.Dialogs;
using BlackSite.App.Services;
using BlackSite.Core.Platform;
using BlackSite.Core.Settings;
using BlackSite.Core.Shortcuts;

namespace BlackSite.App.Views;

public partial class SettingsView : UserControl
{
    private SettingsManager _sm = null!;
    private ShortcutManager _shortcuts = null!;
    private Action _apply = () => { };
    private Action _restoreLayout = () => { };
    private bool _loading = true; // true until Load() runs: sliders raise events while the XAML initialises

    private AppSettings S => _sm.Current;

    public SettingsView() => InitializeComponent();

    public void Initialize(SettingsManager sm, ShortcutManager shortcuts, Action apply, Action restoreLayout)
    {
        _sm = sm; _shortcuts = shortcuts; _apply = apply; _restoreLayout = restoreLayout;
        BuildChoices();
        Load();
        PathText.Text = $"Settings: {AppPaths.SettingsFile}\nLog: {BlackSite.Core.Logging.Log.FilePath}" + (AppPaths.IsPortable ? "\nPortable mode: data is stored next to the program." : "");
    }

    private void BuildChoices()
    {
        foreach (var (name, hex) in ThemeManager.Palette)
        {
            var rb = new RadioButton
            {
                Style = (Style)FindResource("Swatch"), Tag = hex, ToolTip = name, GroupName = "accent",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
            };
            System.Windows.Automation.AutomationProperties.SetName(rb, $"Accent {name}");
            rb.Checked += (_, _) => { if (_loading) return; S.AccentColor = hex; Changed(SoundKind.Click); };
            SwatchPanel.Children.Add(rb);
        }
        foreach (var style in AppSettings.BackgroundStyles)
        {
            var rb = new RadioButton { Style = (Style)FindResource("Chip"), Content = style.ToUpperInvariant(), Tag = style, GroupName = "bg" };
            rb.Checked += (_, _) => { if (_loading) return; S.BackgroundStyle = style; Changed(SoundKind.Click); };
            BgPanel.Children.Add(rb);
        }
        foreach (var (label, get, set) in new (string, Func<SoundToggles, bool>, Action<SoundToggles, bool>)[]
        {
            ("Click", t => t.Click, (t, v) => t.Click = v), ("Launch", t => t.Launch, (t, v) => t.Launch = v),
            ("Navigation", t => t.Navigation, (t, v) => t.Navigation = v), ("System online", t => t.Online, (t, v) => t.Online = v),
            ("Warning", t => t.Warning, (t, v) => t.Warning = v),
        })
        {
            var cb = new CheckBox { Content = label, Margin = new Thickness(0, 2, 22, 2), Tag = get };
            RoutedEventHandler toggled = (_, _) =>
            {
                if (_loading) return;
                set(S.Sounds, cb.IsChecked == true);
                _sm.Save();
                if (cb.IsChecked == true)
                    AudioManager.Play(label switch { "Click" => SoundKind.Click, "Launch" => SoundKind.Launch, "Navigation" => SoundKind.Navigation, "System online" => SoundKind.Online, _ => SoundKind.Warning });
            };
            cb.Checked += toggled;
            cb.Unchecked += toggled;
            SoundPanel.Children.Add(cb);
        }
    }

    public void Load()
    {
        _loading = true;
        foreach (RadioButton rb in SwatchPanel.Children) rb.IsChecked = string.Equals((string)rb.Tag, S.AccentColor, StringComparison.OrdinalIgnoreCase);
        foreach (RadioButton rb in BgPanel.Children) rb.IsChecked = (string)rb.Tag == S.BackgroundStyle;
        OpacitySlider.Value = S.PanelOpacity;
        AnimToggle.IsChecked = S.AnimationsEnabled;
        IntensitySlider.Value = S.AnimationIntensity;
        IntensitySlider.IsEnabled = S.AnimationsEnabled;
        SoundToggle.IsChecked = S.SoundEnabled;
        VolumeSlider.Value = S.MasterVolume;
        foreach (CheckBox cb in SoundPanel.Children) cb.IsChecked = ((Func<SoundToggles, bool>)cb.Tag)(S.Sounds);
        TopToggle.IsChecked = S.AlwaysOnTop;
        FullToggle.IsChecked = S.Fullscreen;
        BorderlessToggle.IsChecked = S.Borderless;
        MinToggle.IsChecked = S.StartMinimized;
        StartupToggle.IsChecked = StartupManager.IsEnabled;
        UpdateLabels();
        _loading = false;
    }

    private void UpdateLabels()
    {
        OpacityLabel.Text = $"PANEL TRANSPARENCY — {S.PanelOpacity * 100:0}% opaque";
        IntensityLabel.Text = $"ANIMATION INTENSITY — {S.AnimationIntensity * 100:0}%";
        VolumeLabel.Text = $"MASTER VOLUME — {S.MasterVolume * 100:0}%";
    }

    private void Changed(SoundKind? sound = null)
    {
        UpdateLabels();
        _apply();
        if (sound is { } s) AudioManager.Play(s);
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_loading) return; S.PanelOpacity = e.NewValue; Changed(); }
    private void Intensity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_loading) return; S.AnimationIntensity = e.NewValue; Changed(); }
    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loading) return;
        S.MasterVolume = e.NewValue;
        UpdateLabels();
        _sm.Save();
    }

    private void Anim_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        S.AnimationsEnabled = AnimToggle.IsChecked == true;
        IntensitySlider.IsEnabled = S.AnimationsEnabled;
        Changed(SoundKind.Click);
    }

    private void Sound_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        S.SoundEnabled = SoundToggle.IsChecked == true;
        _sm.Save();
        AudioManager.Play(SoundKind.Click); // audible confirmation at the chosen volume
    }

    private void Window_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        S.AlwaysOnTop = TopToggle.IsChecked == true;
        S.Fullscreen = FullToggle.IsChecked == true;
        S.Borderless = BorderlessToggle.IsChecked == true;
        S.StartMinimized = MinToggle.IsChecked == true;
        Changed(SoundKind.Click);
    }

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        bool want = StartupToggle.IsChecked == true;
        var exe = Environment.ProcessPath ?? "";
        if (!StartupManager.Set(want, exe))
        {
            MessageBox.Show(Window.GetWindow(this), "Could not update the Windows startup entry. See the log for details.", "BLACKSITE", MessageBoxButton.OK, MessageBoxImage.Warning);
            _loading = true;
            StartupToggle.IsChecked = StartupManager.IsEnabled;
            _loading = false;
            return;
        }
        S.LaunchAtStartup = want;
        _sm.Save();
        AudioManager.Play(SoundKind.Click);
    }

    public void SyncWindowToggles()
    {
        _loading = true;
        TopToggle.IsChecked = S.AlwaysOnTop;
        FullToggle.IsChecked = S.Fullscreen;
        BorderlessToggle.IsChecked = S.Borderless;
        _loading = false;
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e) =>
        new ShortcutEditorWindow(_shortcuts) { Owner = Window.GetWindow(this) }.ShowDialog();

    private void RestoreShortcuts_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(Window.GetWindow(this), "Restore default shortcuts?", "This replaces your shortcut list with the 10 built-in applications.", "RESTORE", "", false)) return;
        _shortcuts.RestoreDefaults();
    }

    private void RestoreLayout_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(Window.GetWindow(this), "Restore default layout?", "Window size and position, fullscreen and the shortcut list return to their defaults. Other settings are kept.", "RESTORE", "", false)) return;
        _restoreLayout();
        _shortcuts.RestoreDefaults();
        Load();
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDialog.Ask(Window.GetWindow(this), "Reset all settings?", "Every setting and shortcut returns to its factory default. This cannot be undone.", "RESET", "", true)) return;
        StartupManager.Set(false, Environment.ProcessPath ?? "");
        _sm.Reset();
        _shortcuts.RestoreDefaults();
        AudioManager.Attach(_sm.Current);
        Load();
        _apply();
    }
}
