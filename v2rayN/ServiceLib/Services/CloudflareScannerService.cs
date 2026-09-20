using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using ServiceLib.Models.Dto;

namespace ServiceLib.Services;

public class CloudflareScannerService
{
    public static readonly string[] PopularSubnets =
    [
        "104.16.0.0/13",
        "104.24.0.0/14",
        "172.64.0.0/13",
        "162.158.0.0/15",
        "198.41.128.0/17",
        "188.114.96.0/20",
        "141.101.64.0/18"
    ];

    public static readonly string[] AllCloudflareRanges =
    [
        "173.245.48.0/20",
        "103.21.244.0/22",
        "103.22.200.0/22",
        "103.31.4.0/22",
        "141.101.64.0/18",
        "108.162.192.0/18",
        "190.93.240.0/20",
        "188.114.96.0/20",
        "197.234.240.0/22",
        "198.41.128.0/17",
        "162.158.0.0/15",
        "104.16.0.0/13",
        "104.24.0.0/14",
        "172.64.0.0/13",
        "131.0.72.0/22"
    ];

    public static List<string> GenerateRandomIps(IEnumerable<string> cidrList, int totalCount)
    {
        var ranges = cidrList
            .Select(ParseCidr)
            .Where(r => r != null)
            .Select(r => r!.Value)
            .ToList();

        if (ranges.Count == 0)
        {
            return [];
        }

        var results = new HashSet<string>();
        var maxAttempts = totalCount * 4;
        var attempts = 0;

        while (results.Count < totalCount && attempts < maxAttempts)
        {
            attempts++;
            var range = ranges[Random.Shared.Next(ranges.Count)];
            var offset = (uint)Random.Shared.Next(1, (int)Math.Max(2, range.HostCount - 2));
            var ipInt = range.StartIp + offset;
            var bytes = BitConverter.GetBytes(ipInt).Reverse().ToArray();
            var ipStr = new IPAddress(bytes).ToString();
            results.Add(ipStr);
        }

        return results.ToList();
    }

    private static (uint StartIp, uint HostCount)? ParseCidr(string cidr)
    {
        try
        {
            var parts = cidr.Trim().Split('/');
            if (parts.Length != 2) return null;
            var ipBytes = IPAddress.Parse(parts[0]).GetAddressBytes();
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(ipBytes);
            }
            var startIp = BitConverter.ToUInt32(ipBytes, 0);
            var prefix = int.Parse(parts[1]);
            if (prefix < 0 || prefix > 32) return null;
            var hostCount = (uint)(1L << (32 - prefix));
            return (startIp, hostCount);
        }
        catch
        {
            return null;
        }
    }

    public static async Task<CloudflareIpResultItem> TestIpAsync(
        string ip,
        int port,
        string host,
        bool enableSpeedTest,
        CancellationToken ct)
    {
        var item = new CloudflareIpResultItem
        {
            Ip = ip,
            Port = port,
            Ping = -1,
            Status = "Timeout",
            DataCenter = "-",
            Speed = "-",
            IsSuccess = false
        };

        if (string.IsNullOrWhiteSpace(host))
        {
            host = "speed.cloudflare.com";
        }

        // 1. TCP Ping
        var sw = Stopwatch.StartNew();
        try
        {
            using var pingSocket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1500));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            await pingSocket.ConnectAsync(new IPEndPoint(IPAddress.Parse(ip), port), linkedCts.Token);
            sw.Stop();
            item.Ping = (int)sw.ElapsedMilliseconds;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            item.Status = "Timeout";
            return item;
        }
        catch
        {
            item.Status = "TCP Failed";
            return item;
        }

        // 2. TLS & Trace verification
        try
        {
            using var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (_, token) =>
                {
                    var sock = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    await sock.ConnectAsync(new IPEndPoint(IPAddress.Parse(ip), port), token);
                    return new NetworkStream(sock, ownsSocket: true);
                },
                SslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = host,
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
                PooledConnectionLifetime = TimeSpan.FromSeconds(5)
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            var traceUrl = port == 443
                ? $"https://{host}/cdn-cgi/trace"
                : $"https://{host}:{port}/cdn-cgi/trace";

            var response = await httpClient.GetAsync(traceUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                item.IsSuccess = true;
                item.Status = "Working";

                var content = await response.Content.ReadAsStringAsync(ct);
                var lines = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.StartsWith("colo=", StringComparison.OrdinalIgnoreCase))
                    {
                        item.DataCenter = line[5..].Trim();
                        break;
                    }
                }

                // 3. Optional Speed Test
                if (enableSpeedTest)
                {
                    try
                    {
                        var speedUrl = port == 443
                            ? $"https://{host}/__down?bytes=300000"
                            : $"https://{host}:{port}/__down?bytes=300000";

                        var speedSw = Stopwatch.StartNew();
                        var speedResp = await httpClient.GetAsync(speedUrl, ct);
                        if (speedResp.IsSuccessStatusCode)
                        {
                            var bytes = await speedResp.Content.ReadAsByteArrayAsync(ct);
                            speedSw.Stop();
                            if (speedSw.Elapsed.TotalSeconds > 0 && bytes.Length > 0)
                            {
                                var mb = bytes.Length / (1024.0 * 1024.0);
                                var mbps = mb / speedSw.Elapsed.TotalSeconds;
                                item.Speed = $"{mbps:F2} MB/s";
                            }
                        }
                    }
                    catch
                    {
                        item.Speed = "-";
                    }
                }
            }
            else
            {
                item.Status = $"HTTP {(int)response.StatusCode}";
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            item.Status = "TLS Timeout";
        }
        catch (Exception ex)
        {
            item.Status = ex.InnerException?.Message ?? "TLS Failed";
        }

        return item;
    }
}
