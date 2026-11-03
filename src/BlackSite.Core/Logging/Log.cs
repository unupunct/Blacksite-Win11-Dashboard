using System.Text;

namespace BlackSite.Core.Logging;

/// <summary>Minimal thread-safe file logger. No-op until <see cref="Configure"/> is called.</summary>
public static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();
    private static string? _file;

    public static string? FilePath => _file;

    public static void Configure(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            _file = Path.Combine(directory, "blacksite.log");
        }
        catch { _file = null; }
    }

    public static void Info(string message) => Write("INFO ", message, null);
    public static void Warn(string message, Exception? ex = null) => Write("WARN ", message, ex);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        var path = _file;
        if (path is null) return;
        try
        {
            var sb = new StringBuilder();
            sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append(' ').Append(level).Append(' ').Append(message);
            if (ex is not null)
            {
                sb.Append(" | ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                if (level == "ERROR")
                {
                    var st = ex.StackTrace ?? "";
                    sb.AppendLine().Append(st.Length > 1200 ? st[..1200] : st);
                }
            }
            sb.AppendLine();
            lock (Gate)
            {
                var fi = new FileInfo(path);
                if (fi.Exists && fi.Length > MaxBytes)
                {
                    var old = path + ".old";
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }
                File.AppendAllText(path, sb.ToString());
            }
        }
        catch { /* logging must never throw */ }
    }
}
