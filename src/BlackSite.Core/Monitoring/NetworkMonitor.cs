using System.Diagnostics;
using System.Net.NetworkInformation;
using BlackSite.Core.Logging;

namespace BlackSite.Core.Monitoring;

/// <summary>Throughput from interface byte-counter deltas. Only adapters with a default gateway are counted.</summary>
public sealed class NetworkMonitor
{
    private long _prevRx, _prevTx;
    private long _prevTicks;
    private bool _haveBaseline;

    public static double ComputeMbps(long previousBytes, long currentBytes, double seconds)
    {
        if (seconds <= 0 || currentBytes < previousBytes) return 0; // counter reset or bad interval
        return (currentBytes - previousBytes) * 8.0 / 1_000_000.0 / seconds;
    }

    public NetworkSample Sample()
    {
        try
        {
            long rx = 0, tx = 0;
            string? adapter = null;
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var props = nic.GetIPProperties();
                if (!props.GatewayAddresses.Any(g => g.Address.GetAddressBytes().Any(b => b != 0))) continue;
                var stats = nic.GetIPv4Statistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
                adapter ??= nic.Name;
            }

            var now = Stopwatch.GetTimestamp();
            var connected = adapter is not null;
            double down = 0, up = 0;
            if (_haveBaseline && connected)
            {
                var secs = (now - _prevTicks) / (double)Stopwatch.Frequency;
                down = ComputeMbps(_prevRx, rx, secs);
                up = ComputeMbps(_prevTx, tx, secs);
            }
            _prevRx = rx; _prevTx = tx; _prevTicks = now; _haveBaseline = connected;
            return new NetworkSample(connected, down, up, adapter);
        }
        catch (Exception ex)
        {
            Log.Warn("Network metrics unavailable", ex);
            _haveBaseline = false;
            return new NetworkSample(false, 0, 0, null);
        }
    }
}
