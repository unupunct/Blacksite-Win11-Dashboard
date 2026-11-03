using BlackSite.Core.Shortcuts;

namespace BlackSite.Core.Settings;

public sealed class SoundToggles
{
    public bool Click { get; set; } = true;
    public bool Launch { get; set; } = true;
    public bool Navigation { get; set; } = true;
    public bool Online { get; set; } = true;
    public bool Warning { get; set; } = true;
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;

    // Appearance
    public string AccentColor { get; set; } = "#00E5FF";
    public string BackgroundStyle { get; set; } = "Circuit"; // Circuit | Grid | Particles | Plain
    public double PanelOpacity { get; set; } = 0.55;
    public bool AnimationsEnabled { get; set; } = true;
    public double AnimationIntensity { get; set; } = 0.6;

    // Sound
    public bool SoundEnabled { get; set; } = true;
    public double MasterVolume { get; set; } = 0.25;
    public SoundToggles Sounds { get; set; } = new();

    // Window
    public bool AlwaysOnTop { get; set; }
    public bool Fullscreen { get; set; }
    public bool Borderless { get; set; } = true;
    public bool StartMinimized { get; set; }
    public bool LaunchAtStartup { get; set; }
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1440;
    public double WindowHeight { get; set; } = 860;
    public bool WindowMaximized { get; set; }

    // Shortcuts (null = built-in defaults)
    public List<ShortcutDefinition>? Shortcuts { get; set; }

    public static readonly string[] BackgroundStyles = { "Circuit", "Grid", "Particles", "Plain" };

    /// <summary>Clamps every value into a safe range (settings files are user-editable).</summary>
    public void Normalize()
    {
        PanelOpacity = Math.Clamp(double.IsFinite(PanelOpacity) ? PanelOpacity : 0.55, 0.15, 1.0);
        AnimationIntensity = Math.Clamp(double.IsFinite(AnimationIntensity) ? AnimationIntensity : 0.6, 0.0, 1.0);
        MasterVolume = Math.Clamp(double.IsFinite(MasterVolume) ? MasterVolume : 0.25, 0.0, 1.0);
        if (!BackgroundStyles.Contains(BackgroundStyle)) BackgroundStyle = "Circuit";
        if (!System.Text.RegularExpressions.Regex.IsMatch(AccentColor ?? "", "^#[0-9A-Fa-f]{6}$")) AccentColor = "#00E5FF";
        if (!double.IsFinite(WindowWidth) || WindowWidth < 800) WindowWidth = 1440;
        if (!double.IsFinite(WindowHeight) || WindowHeight < 560) WindowHeight = 860;
        if (WindowLeft is { } l && !double.IsFinite(l)) WindowLeft = null;
        if (WindowTop is { } t && !double.IsFinite(t)) WindowTop = null;
        Sounds ??= new SoundToggles();
    }
}
