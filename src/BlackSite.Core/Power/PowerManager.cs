using System.Diagnostics;
using BlackSite.Core.Logging;
using BlackSite.Core.Shortcuts;

namespace BlackSite.Core.Power;

public enum PowerAction { Shutdown, Restart }

/// <summary>Shutdown/restart. Refuses to act unless the caller passes an explicit confirmation.</summary>
public sealed class PowerManager
{
    private readonly IProcessStarter _starter;
    public PowerManager(IProcessStarter? starter = null) => _starter = starter ?? new ShellProcessStarter();

    public static ProcessStartInfo BuildCommand(PowerAction action) => new(
        Path.Combine(Environment.SystemDirectory, "shutdown.exe"),
        action == PowerAction.Shutdown ? "/s /t 0" : "/r /t 0")
    {
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    public LaunchResult Execute(PowerAction action, bool confirmed)
    {
        if (!confirmed) return new LaunchResult(false, "Power action was not confirmed.");
        try
        {
            Log.Warn($"Power action confirmed by user: {action}");
            _starter.Start(BuildCommand(action));
            return new LaunchResult(true, action == PowerAction.Shutdown ? "Shutting down…" : "Restarting…");
        }
        catch (Exception ex)
        {
            Log.Error($"Power action {action} failed", ex);
            return new LaunchResult(false, $"Could not execute {action}: {ex.Message}");
        }
    }
}
