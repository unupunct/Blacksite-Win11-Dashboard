using BlackSite.Core.Logging;
using BlackSite.Core.Settings;

namespace BlackSite.Core.Shortcuts;

/// <summary>Owns the editable application-shortcut list and persists it through <see cref="SettingsManager"/>.</summary>
public sealed class ShortcutManager
{
    public const int MaxShortcuts = 20;
    private readonly SettingsManager _settings;
    private List<ShortcutDefinition> _items;

    public event Action? Changed;

    public ShortcutManager(SettingsManager settings)
    {
        _settings = settings;
        _items = Sanitize(settings.Current.Shortcuts) ?? DefaultShortcuts.Apps();
    }

    public IReadOnlyList<ShortcutDefinition> Items => _items;

    private static List<ShortcutDefinition>? Sanitize(List<ShortcutDefinition>? list)
    {
        if (list is null || list.Count == 0) return null;
        var good = new List<ShortcutDefinition>();
        foreach (var s in list.Take(MaxShortcuts))
        {
            var r = ShortcutValidator.ValidateShortcut(s, requireExists: false);
            if (r.IsValid) good.Add(s);
            else Log.Warn($"Dropped invalid shortcut '{s.Name}': {r.Message}");
        }
        return good.Count > 0 ? good : null;
    }

    public ValidationResult Add(ShortcutDefinition s)
    {
        if (_items.Count >= MaxShortcuts) return ValidationResult.Fail($"At most {MaxShortcuts} shortcuts are supported.");
        var r = ShortcutValidator.ValidateShortcut(s, requireExists: true);
        if (!r.IsValid) return r;
        _items.Add(s);
        Commit();
        return r;
    }

    public ValidationResult Replace(string id, ShortcutDefinition updated)
    {
        var i = _items.FindIndex(x => x.Id == id);
        if (i < 0) return ValidationResult.Fail("Shortcut not found.");
        var r = ShortcutValidator.ValidateShortcut(updated, requireExists: true);
        if (!r.IsValid) return r;
        updated.Id = id;
        _items[i] = updated;
        Commit();
        return r;
    }

    public bool Remove(string id)
    {
        var n = _items.RemoveAll(x => x.Id == id);
        if (n > 0) Commit();
        return n > 0;
    }

    public void Move(int from, int to)
    {
        if (from < 0 || from >= _items.Count || to < 0 || to >= _items.Count || from == to) return;
        var s = _items[from];
        _items.RemoveAt(from);
        _items.Insert(to, s);
        Commit();
    }

    public void RestoreDefaults()
    {
        _items = DefaultShortcuts.Apps();
        _settings.Current.Shortcuts = null;
        _settings.Save();
        Changed?.Invoke();
    }

    private void Commit()
    {
        _settings.Current.Shortcuts = _items;
        _settings.Save();
        Changed?.Invoke();
    }
}
