namespace BlackSite.Core.Shortcuts;

/// <summary>The built-in, trusted shortcut catalogue. Only these targets may carry arguments.</summary>
public static class DefaultShortcuts
{
    private static LaunchTarget Exe(string v, string a = "") => new(TargetKind.Executable, v, a);
    private static LaunchTarget Uri(string v) => new(TargetKind.Uri, v);

    private static ShortcutDefinition S(string id, string name, string desc, string glyph, params LaunchTarget[] t) =>
        new() { Id = id, Name = name, Description = desc, Glyph = glyph, Targets = t.ToList() };

    /// <summary>Left-panel system shortcuts (fixed, not user-editable).</summary>
    public static IReadOnlyList<ShortcutDefinition> System() => new[]
    {
        S("sys.computer", "My Computer", "Explore your files", "", Exe("explorer.exe", "shell:MyComputerFolder")),
        S("sys.control", "Control Panel", "System settings", "", Exe("control.exe")),
        // The GodMode GUID opens the "All Tasks" shell folder directly; nothing is created or modified.
        S("sys.godmode", "God Mode", "Advanced settings", "",
            Exe("explorer.exe", "shell:::{ED7BA470-8E54-465E-825C-99712043E01C}")),
    };

    /// <summary>The 10 application shortcuts shown in the grid.</summary>
    public static List<ShortcutDefinition> Apps() => new()
    {
        S("app.notepad", "Notepad", "Text editor", "", Exe("notepad.exe")),
        S("app.calc", "Calculator", "Calculate", "", Exe("calc.exe"), Uri("calculator:")),
        S("app.paint", "Paint", "Graphics", "", Exe("mspaint.exe"), Uri("ms-paint:")),
        S("app.photos", "Photos", "View & edit", "", Uri("ms-photos:")),
        S("app.settings", "Settings", "System settings", "", Uri("ms-settings:")),
        S("app.edge", "Microsoft Edge", "Web browser", "",
            Exe(@"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"),
            Exe(@"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"), Uri("microsoft-edge:")),
        S("app.explorer", "File Explorer", "Browse files", "", Exe("explorer.exe")),
        S("app.taskmgr", "Task Manager", "Monitor tasks", "", Exe("taskmgr.exe")),
        S("app.terminal", "Windows Terminal", "PowerShell & CMD", "", Exe("wt.exe"), Exe("powershell.exe")),
        S("app.snip", "Snipping Tool", "Capture screen", "", Exe("SnippingTool.exe"), Uri("ms-screenclip:")),
    };

    /// <summary>True when the target (including arguments) exactly matches a built-in one.</summary>
    public static bool IsTrusted(LaunchTarget t) =>
        System().Concat(Apps()).SelectMany(s => s.Targets).Any(b =>
            b.Kind == t.Kind &&
            string.Equals(b.Value, t.Value, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(b.Arguments, t.Arguments, StringComparison.Ordinal));
}
