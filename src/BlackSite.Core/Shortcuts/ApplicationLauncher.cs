using System.Diagnostics;
using BlackSite.Core.Logging;

namespace BlackSite.Core.Shortcuts;

public interface IProcessStarter { void Start(ProcessStartInfo psi); }

public sealed class ShellProcessStarter : IProcessStarter
{
    public void Start(ProcessStartInfo psi) => Process.Start(psi)?.Dispose();
}

public sealed record LaunchResult(bool Success, string Message);

public sealed class ApplicationLauncher
{
    private readonly IProcessStarter _starter;
    public ApplicationLauncher(IProcessStarter? starter = null) => _starter = starter ?? new ShellProcessStarter();

    /// <summary>First target that exists on this PC and passes validation, or null.</summary>
    public LaunchTarget? Resolve(ShortcutDefinition s) =>
        s.Targets.FirstOrDefault(t => ShortcutValidator.ValidateTarget(t, requireExists: true).IsValid);

    public bool IsAvailable(ShortcutDefinition s) => Resolve(s) is not null;

    public LaunchResult Launch(ShortcutDefinition s)
    {
        try
        {
            var t = Resolve(s);
            if (t is null) return new LaunchResult(false, $"{s.Name} is not available on this PC.");

            ProcessStartInfo psi;
            if (t.Kind == TargetKind.Uri)
            {
                psi = new ProcessStartInfo(t.Value) { UseShellExecute = true };
            }
            else
            {
                var path = ExecutableResolver.Resolve(t.Value)!;
                psi = new ProcessStartInfo(path) { UseShellExecute = true };
                if (!string.IsNullOrEmpty(t.Arguments)) psi.Arguments = t.Arguments;
            }
            _starter.Start(psi);
            Log.Info($"Launched {s.Name}: {psi.FileName} {psi.Arguments}");
            return new LaunchResult(true, $"{s.Name} launched.");
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to launch {s.Name}", ex);
            return new LaunchResult(false, $"Could not launch {s.Name}: {ex.Message}");
        }
    }
}
