using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BlackSite.Core.Logging;
using BlackSite.Core.Settings;

namespace BlackSite.App.Services;

/// <summary>Applies the accent colour and panel opacity to the application resources.</summary>
public static class ThemeManager
{
    public static event Action? Changed;
    public static Color Accent { get; private set; } = (Color)ColorConverter.ConvertFromString("#00E5FF");

    public static readonly (string Name, string Hex)[] Palette =
    {
        ("Cyan", "#00E5FF"), ("Electric blue", "#2979FF"), ("Green", "#00FF9C"), ("Violet", "#B388FF"),
        ("Magenta", "#FF4DD2"), ("Orange", "#FFA726"), ("Red", "#FF3B5C"), ("White", "#E6F4FF"),
    };

    public static void Apply(AppSettings s)
    {
        var res = Application.Current.Resources;
        Accent = (Color)ColorConverter.ConvertFromString(s.AccentColor);
        res["AccentColor"] = Accent;
        res["AccentBrush"] = Frozen(Accent);
        res["AccentBrushDim"] = Frozen(Color.FromArgb(0x60, Accent.R, Accent.G, Accent.B));
        res["AccentBrushFaint"] = Frozen(Color.FromArgb(0x1C, Accent.R, Accent.G, Accent.B));
        byte a = (byte)Math.Round(s.PanelOpacity * 255);
        res["PanelBrush"] = Frozen(Color.FromArgb(a, 0x0A, 0x18, 0x2C));
        res["PanelBrushStrong"] = Frozen(Color.FromArgb((byte)Math.Min(255, a + 60), 0x0C, 0x20, 0x3A));
        Changed?.Invoke();
    }

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

/// <summary>Global switch for decorative animation. Hover/press feedback is not affected.</summary>
public static class AnimationManager
{
    public static event Action? Changed;
    public static bool Enabled { get; private set; } = true;
    public static double Intensity { get; private set; } = 0.6;

    public static void Apply(AppSettings s)
    {
        Enabled = s.AnimationsEnabled;
        Intensity = s.AnimationIntensity;
        Changed?.Invoke();
    }
}

public enum SoundKind { Click, Launch, Navigation, Online, Warning }

/// <summary>Short synthesised UI blips; no audio files shipped. Volume is baked into the samples.</summary>
public static class AudioManager
{
    private const int Rate = 22050;
    private static AppSettings? _settings;
    private static readonly Dictionary<(SoundKind, int), byte[]> Cache = new();

    public static void Attach(AppSettings settings) => _settings = settings;

    public static void Play(SoundKind kind)
    {
        var s = _settings;
        if (s is null || !s.SoundEnabled || s.MasterVolume <= 0.001) return;
        bool on = kind switch
        {
            SoundKind.Click => s.Sounds.Click,
            SoundKind.Launch => s.Sounds.Launch,
            SoundKind.Navigation => s.Sounds.Navigation,
            SoundKind.Online => s.Sounds.Online,
            _ => s.Sounds.Warning,
        };
        if (!on) return;
        try
        {
            int vol = (int)Math.Round(s.MasterVolume * 100);
            if (!Cache.TryGetValue((kind, vol), out var wav))
                Cache[(kind, vol)] = wav = Build(kind, s.MasterVolume);
            using var player = new System.Media.SoundPlayer(new MemoryStream(wav));
            player.Play(); // async; the player copies the stream before returning
        }
        catch (Exception ex) { Log.Warn("Sound playback failed", ex); }
    }

    private static byte[] Build(SoundKind kind, double volume)
    {
        // (startHz, endHz, ms, gain) segments
        var segs = kind switch
        {
            SoundKind.Click => new[] { (1400.0, 1000.0, 35, 0.6) },
            SoundKind.Launch => new[] { (500.0, 1500.0, 170, 0.7) },
            SoundKind.Navigation => new[] { (760.0, 980.0, 70, 0.5) },
            SoundKind.Online => new[] { (520.0, 520.0, 70, 0.6), (780.0, 780.0, 70, 0.6), (1040.0, 1040.0, 130, 0.6) },
            _ => new[] { (440.0, 440.0, 120, 0.7), (0.0, 0.0, 50, 0.0), (440.0, 440.0, 120, 0.7) },
        };
        var samples = new List<short>();
        double amp = Math.Clamp(volume, 0, 1) * 0.45 * short.MaxValue;
        foreach (var (f0, f1, ms, gain) in segs)
        {
            int n = Rate * ms / 1000;
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n;
                double f = f0 + (f1 - f0) * t;
                phase += 2 * Math.PI * f / Rate;
                double env = Math.Min(1, i / 80.0) * Math.Pow(1 - t, 1.4);
                double v = kind == SoundKind.Warning ? Math.Sign(Math.Sin(phase)) * 0.5 + Math.Sin(phase) * 0.5 : Math.Sin(phase);
                samples.Add((short)(v * env * gain * amp));
            }
        }
        using var ms2 = new MemoryStream();
        using var w = new BinaryWriter(ms2);
        int dataLen = samples.Count * 2;
        w.Write("RIFF"u8.ToArray()); w.Write(36 + dataLen); w.Write("WAVEfmt "u8.ToArray());
        w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
        w.Write("data"u8.ToArray()); w.Write(dataLen);
        foreach (var s in samples) w.Write(s);
        w.Flush();
        return ms2.ToArray();
    }
}

/// <summary>Loads a shortcut's custom icon (image file, or the icon of an executable).</summary>
public static class IconLoader
{
    private static readonly string[] ImageExt = { ".png", ".jpg", ".jpeg", ".bmp", ".ico", ".gif" };

    public static ImageSource? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            path = Environment.ExpandEnvironmentVariables(path);
            if (!File.Exists(path)) return null;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ImageExt.Contains(ext))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 128;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            return ShellIcon(path);
        }
        catch (Exception ex)
        {
            Log.Warn($"Icon load failed for {path}", ex);
            return null;
        }
    }

    public static ImageSource? ShellIcon(string path)
    {
        var info = new SHFILEINFO();
        // SHGFI_ICON | SHGFI_LARGEICON
        if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf(info), 0x100) == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
        try
        {
            var src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally { DestroyIcon(info.hIcon); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attrs, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr h);
}

public abstract class ObservableBase : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new(name));
        return true;
    }

    protected void Raise([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new(name));
}

public sealed class InvBoolToVis : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        value is true ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
        throw new NotSupportedException();
}
