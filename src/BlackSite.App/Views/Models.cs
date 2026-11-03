using System.Windows.Media;
using BlackSite.App.Services;
using BlackSite.Core.Shortcuts;

namespace BlackSite.App.Views;

/// <summary>One application card in the grid.</summary>
public sealed class ShortcutItem : ObservableBase
{
    private bool _available = true, _launching;

    public ShortcutItem(ShortcutDefinition def, bool available)
    {
        Definition = def;
        _available = available;
        Icon = IconLoader.Load(def.IconPath);
        if (Icon is null && def.Primary is { Kind: TargetKind.Executable } t && IsCustom(def))
        {
            var p = ExecutableResolver.Resolve(t.Value);
            if (p is not null) Icon = IconLoader.ShellIcon(p);
        }
    }

    private static bool IsCustom(ShortcutDefinition d) => !d.Id.StartsWith("app.", StringComparison.Ordinal);

    public ShortcutDefinition Definition { get; }
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public string Glyph => Definition.Glyph;
    public ImageSource? Icon { get; }
    public bool HasIcon => Icon is not null;
    public bool HasGlyph => Icon is null;

    public bool IsAvailable { get => _available; set { if (Set(ref _available, value)) Raise(nameof(Tooltip)); } }
    public bool IsLaunching { get => _launching; set => Set(ref _launching, value); }

    public string Tooltip => IsAvailable
        ? $"{Name} — {Description}\nClick to launch"
        : $"{Name} is not available on this PC\nClick to choose its program manually";
}

/// <summary>One button in the left "system shortcuts" panel.</summary>
public sealed class SysItem
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }
    public required string Id { get; init; }
    /// <summary>Fixed icon colour (e.g. red for Shut Down); null = follow the accent colour.</summary>
    public Brush? IconBrush { get; init; }
    public bool HasCustomBrush => IconBrush is not null;
}
