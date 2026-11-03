using System.Runtime.InteropServices;
using BlackSite.Core.Logging;

namespace BlackSite.Core.Monitoring;

public sealed record DiskInfo(string Drive, ulong UsedBytes, ulong FreeBytes, ulong TotalBytes)
{
    public double Percent => TotalBytes == 0 ? 0 : 100.0 * UsedBytes / TotalBytes;
}

public sealed record NetworkSample(bool Connected, double DownMbps, double UpMbps, string? Adapter);

public sealed record SystemSnapshot(
    double CpuPercent,
    double MemoryPercent, ulong MemoryUsedBytes, ulong MemoryTotalBytes,
    DiskInfo? Disk,
    NetworkSample Network,
    DateTime Timestamp);

/// <summary>Samples real Windows counters once per interval on a background timer.</summary>
public sealed class SystemMonitor : IDisposable
{
    public const int HistoryLength = 90;

    private readonly NetworkMonitor _net = new();
    private readonly string _driveRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
    private ulong _prevIdle, _prevKernel, _prevUser;
    private bool _haveCpuBaseline;
    private CancellationTokenSource? _cts;
    private readonly object _historyLock = new();
    private readonly List<SystemSnapshot> _history = new();

    public event Action<SystemSnapshot>? Updated;
    public SystemSnapshot? Latest { get; private set; }

    public IReadOnlyList<SystemSnapshot> History()
    {
        lock (_historyLock) return _history.ToArray();
    }

    public void Start(TimeSpan interval)
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        Sample(); // establish CPU/network baselines
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    var s = Sample();
                    Updated?.Invoke(s);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log.Error("System monitor stopped", ex); }
        }, token);
    }

    public SystemSnapshot Sample()
    {
        var snap = new SystemSnapshot(ReadCpu(), 0, 0, 0, null, new NetworkSample(false, 0, 0, null), DateTime.Now);
        var (memPct, used, total) = ReadMemory();
        DiskInfo? disk = null;
        try
        {
            var d = new DriveInfo(_driveRoot);
            if (d.IsReady) disk = new DiskInfo(_driveRoot.TrimEnd('\\'), (ulong)(d.TotalSize - d.TotalFreeSpace), (ulong)d.TotalFreeSpace, (ulong)d.TotalSize);
        }
        catch (Exception ex) { Log.Warn("Disk metrics unavailable", ex); }

        snap = snap with { MemoryPercent = memPct, MemoryUsedBytes = used, MemoryTotalBytes = total, Disk = disk, Network = _net.Sample() };
        Latest = snap;
        lock (_historyLock)
        {
            _history.Add(snap);
            if (_history.Count > HistoryLength) _history.RemoveAt(0);
        }
        return snap;
    }

    private double ReadCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        ulong i = idle.ToUInt64(), k = kernel.ToUInt64(), u = user.ToUInt64();
        double pct = 0;
        if (_haveCpuBaseline)
        {
            ulong dIdle = i - _prevIdle, dTotal = (k - _prevKernel) + (u - _prevUser); // kernel time includes idle
            if (dTotal > 0) pct = 100.0 * (dTotal - dIdle) / dTotal;
        }
        _prevIdle = i; _prevKernel = k; _prevUser = u; _haveCpuBaseline = true;
        return Math.Clamp(pct, 0, 100);
    }

    private static (double pct, ulong used, ulong total) ReadMemory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref m) || m.ullTotalPhys == 0) return (0, 0, 0);
        var used = m.ullTotalPhys - m.ullAvailPhys;
        return (100.0 * used / m.ullTotalPhys, used, m.ullTotalPhys);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint Low, High;
        public ulong ToUInt64() => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX status);
}
