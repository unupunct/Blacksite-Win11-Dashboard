using System.Text.RegularExpressions;

namespace BlackSite.Core.Shortcuts;

public sealed record ValidationResult(bool IsValid, string Message)
{
    public static ValidationResult Ok { get; } = new(true, "");
    public static ValidationResult Fail(string m) => new(false, m);
}

public static class ShortcutValidator
{
    public static readonly string[] AllowedExtensions = { ".exe", ".lnk", ".msc", ".cpl" };
    private static readonly Regex UriRx = new(@"^(ms-[a-z0-9\-]+|microsoft-edge|calculator|https?|shell):[^\s""<>|^&]*$", RegexOptions.IgnoreCase);
    private static readonly Regex ShellRx = new(@"^shell:(AppsFolder\\[A-Za-z0-9._!\-]+|[A-Za-z0-9 ]+)$", RegexOptions.IgnoreCase);

    /// <summary>Checks the target's shape and safety. <paramref name="requireExists"/> also requires it to resolve on this PC.</summary>
    public static ValidationResult ValidateTarget(LaunchTarget t, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(t.Value)) return ValidationResult.Fail("Target is empty.");
        if (!DefaultShortcuts.IsTrusted(t) && !string.IsNullOrEmpty(t.Arguments))
            return ValidationResult.Fail("Custom shortcuts cannot pass command-line arguments.");

        if (t.Kind == TargetKind.Uri)
        {
            if (!UriRx.IsMatch(t.Value)) return ValidationResult.Fail("Only ms-*, http(s), shell:AppsFolder and a few app links are allowed.");
            if (t.Value.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) && !ShellRx.IsMatch(t.Value))
                return ValidationResult.Fail("Unsupported shell: link.");
            var scheme = t.Value[..t.Value.IndexOf(':')].ToLowerInvariant();
            if (requireExists && !ExecutableResolver.IsSchemeRegistered(scheme))
                return ValidationResult.Fail($"No app is registered for '{scheme}:' on this PC.");
            return ValidationResult.Ok;
        }

        var ext = Path.GetExtension(t.Value.Trim().Trim('"')).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ValidationResult.Fail($"Only {string.Join(", ", AllowedExtensions)} targets are allowed.");
        if (requireExists && ExecutableResolver.Resolve(t.Value) is null)
            return ValidationResult.Fail("File not found.");
        return ValidationResult.Ok;
    }

    public static ValidationResult ValidateShortcut(ShortcutDefinition s, bool requireExists)
    {
        if (string.IsNullOrWhiteSpace(s.Name)) return ValidationResult.Fail("Name is required.");
        if (s.Name.Length > 32) return ValidationResult.Fail("Name must be 32 characters or fewer.");
        if (s.Description.Length > 40) return ValidationResult.Fail("Description must be 40 characters or fewer.");
        if (s.Name.Any(char.IsControl) || s.Description.Any(char.IsControl)) return ValidationResult.Fail("Text contains invalid characters.");
        if (s.Targets.Count == 0) return ValidationResult.Fail("A target is required.");
        foreach (var t in s.Targets)
        {
            // With several candidates only one has to exist; shape must be valid for all.
            var r = ValidateTarget(t, requireExists && s.Targets.Count == 1);
            if (!r.IsValid) return r;
        }
        return ValidationResult.Ok;
    }
}
