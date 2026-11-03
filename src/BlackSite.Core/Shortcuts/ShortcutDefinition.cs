using System.Text.Json.Serialization;

namespace BlackSite.Core.Shortcuts;

public enum TargetKind { Executable, Uri }

public sealed class LaunchTarget
{
    public TargetKind Kind { get; set; }
    public string Value { get; set; } = "";
    public string Arguments { get; set; } = "";

    public LaunchTarget() { }
    public LaunchTarget(TargetKind kind, string value, string args = "") { Kind = kind; Value = value; Arguments = args; }

    public LaunchTarget Clone() => new(Kind, Value, Arguments);
    public override string ToString() => string.IsNullOrEmpty(Arguments) ? Value : $"{Value} {Arguments}";
}

public sealed class ShortcutDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Segoe Fluent Icons glyph used when no custom icon is set.</summary>
    public string Glyph { get; set; } = "";
    /// <summary>Optional image (png/jpg/ico/bmp) or executable whose icon is shown instead of the glyph.</summary>
    public string IconPath { get; set; } = "";
    /// <summary>Candidates tried in order; the first one available on this PC is launched.</summary>
    public List<LaunchTarget> Targets { get; set; } = new();

    [JsonIgnore] public LaunchTarget? Primary => Targets.FirstOrDefault();

    public ShortcutDefinition Clone() => new()
    {
        Id = Id, Name = Name, Description = Description, Glyph = Glyph, IconPath = IconPath,
        Targets = Targets.Select(t => t.Clone()).ToList(),
    };
}
