using System.Text.Json;
using System.Text.Json.Serialization;
using BlackSite.Core.Logging;

namespace BlackSite.Core.Settings;

public sealed class SettingsManager
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _file;

    public SettingsManager(string file) => _file = file;

    public AppSettings Current { get; private set; } = new();

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Json);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    Current = loaded;
                    return Current;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Settings file unreadable; using defaults", ex);
            try { File.Move(_file, _file + ".bad", true); } catch { /* ignore */ }
        }
        Current = new AppSettings();
        return Current;
    }

    public bool Save()
    {
        try
        {
            Current.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json));
            File.Move(tmp, _file, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Could not save settings", ex);
            return false;
        }
    }

    public void Reset()
    {
        Current = new AppSettings();
        Save();
    }
}
