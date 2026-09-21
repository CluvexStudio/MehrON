using System.Net;
using System.Net.Sockets;

namespace ServiceLib.Manager;

/// <summary>
/// Manages the Zeptun userspace TUN engine (https://github.com/Noisemux/zeptun).
/// Zeptun is a high-performance, userspace network engine written in Zig
/// that acts as an ultra-fast tun2socks implementation.
/// </summary>
public sealed class ZeptunManager
{
    public const string ProjectUrl = "https://github.com/Noisemux/zeptun";
    public const string ReleasesUrl = "https://github.com/Noisemux/zeptun/releases";

    private static readonly Lazy<ZeptunManager> _instance = new(() => new());
    public static ZeptunManager Instance => _instance.Value;

    private ProcessService? _process;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public static string? GetZeptunExePath()
    {
        var config = AppManager.Instance.Config.TunnelingItem;
        if (!string.IsNullOrWhiteSpace(config?.ZeptunPath) && File.Exists(config.ZeptunPath))
        {
            return config.ZeptunPath;
        }

        var exeName = Utils.GetExeName("zeptun");

        // 1. bin/zeptun/zeptun.exe
        var inSubDir = Utils.GetBinPath(exeName, "zeptun");
        if (File.Exists(inSubDir))
        {
            return inSubDir;
        }

        // 2. bin/zeptun.exe
        var inBin = Utils.GetBinPath(exeName);
        if (File.Exists(inBin))
        {
            return inBin;
        }

        // 3. System PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv != null)
        {
            foreach (var p in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(p, exeName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    public static bool IsZeptunAvailable() => !string.IsNullOrEmpty(GetZeptunExePath());

    public async Task<bool> StartAsync(int socksPort, ProfileItem? node = null, Func<bool, string, Task>? updateFunc = null)
    {
        await StopAsync();

        var config = AppManager.Instance.Config;
        var item = config.TunnelingItem;

        if (Utils.IsWindows() && !Utils.IsAdministrator())
        {
            await SafeNotifyAsync(updateFunc, true, "Zeptun TUN requires administrator privileges. Restarting as administrator...");
            if (ProcUtils.RebootAsAdmin())
            {
                await AppManager.Instance.AppExitAsync(true);
            }
            else
            {
                await SafeNotifyAsync(updateFunc, true, "Could not elevate to Administrator automatically. Please restart MehrN manually as Administrator (Right-click MehrN -> Run as administrator).");
            }
            return false;
        }

        var exePath = GetZeptunExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            await SafeNotifyAsync(updateFunc, true, $"Zeptun executable was not found. Please download it from {ProjectUrl} and place zeptun.exe in the bin/zeptun/ folder, or configure its path in L.C Settings -> Tunneling Core Settings.");
            return false;
        }

        var interfaceName = string.IsNullOrWhiteSpace(item?.ZeptunInterfaceName) ? "zeptun0" : item.ZeptunInterfaceName.Trim();
        var mtu = (item?.ZeptunMtu > 0 && item.ZeptunMtu != 8500) ? item.ZeptunMtu : 1500;
        var stack = (Utils.IsWindows() || string.IsNullOrWhiteSpace(item?.ZeptunStack) || item?.ZeptunStack is "hybrid" or "system") ? "userspace" : item.ZeptunStack.Trim();

        var excludeIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Keep local LAN and loopback accessible
        excludeIps.Add("127.0.0.0/8");
        excludeIps.Add("10.0.0.0/8");
        excludeIps.Add("172.16.0.0/12");
        excludeIps.Add("192.168.0.0/16");
        excludeIps.Add("169.254.0.0/16");

        // CRITICAL: Exclude remote proxy server address to avoid routing loop!
        if (!string.IsNullOrWhiteSpace(node?.Address))
        {
            var host = node.Address.Trim();
            if (IPAddress.TryParse(host, out var directIp))
            {
                excludeIps.Add(directIp.AddressFamily == AddressFamily.InterNetworkV6 ? $"{directIp}/128" : $"{directIp}/32");
            }
            else
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(host);
                    foreach (var addr in addresses)
                    {
                        excludeIps.Add(addr.AddressFamily == AddressFamily.InterNetworkV6 ? $"{addr}/128" : $"{addr}/32");
                    }
                }
                catch (Exception ex)
                {
                    Logging.SaveLog($"Zeptun: failed to resolve host {host}", ex);
                }
            }
        }

        if (config.TunModeItem.RouteExcludeAddress is { Count: > 0 })
        {
            foreach (var addr in config.TunModeItem.RouteExcludeAddress)
            {
                if (!string.IsNullOrWhiteSpace(addr))
                {
                    excludeIps.Add(addr.Trim());
                }
            }
        }

        var arguments = $"run --tun {interfaceName} --mtu {mtu} --socks5 127.0.0.1:{socksPort} --stack {stack}";
        if (item?.ZeptunAutoRoute ?? true)
        {
            arguments += " --auto-route";
        }
        if (item?.ZeptunStrictRoute == true)
        {
            arguments += " --strict-route";
        }

        foreach (var cidr in excludeIps)
        {
            arguments += $" --exclude {cidr}";
        }

        if (!string.IsNullOrWhiteSpace(item?.ExtraArguments))
        {
            arguments += $" {item.ExtraArguments.Trim()}";
        }

        var errorLogs = new List<string>();
        Func<bool, string, Task> capturedUpdateFunc = async (isError, msg) =>
        {
            if (!string.IsNullOrWhiteSpace(msg))
            {
                lock (errorLogs)
                {
                    errorLogs.Add(msg.Trim());
                    if (errorLogs.Count > 30)
                    {
                        errorLogs.RemoveAt(0);
                    }
                }
            }

            if (updateFunc != null)
            {
                await updateFunc(isError, msg);
            }
        };

        var workingDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            await SafeNotifyAsync(updateFunc, false, $"Starting Zeptun TUN engine ({exePath} {arguments})...");
            _process = new ProcessService(exePath, arguments, workingDir, true, false, null, capturedUpdateFunc);
            await _process.StartAsync();

            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(100);
                if (_process.HasExited)
                {
                    break;
                }
            }

            if (_process.HasExited)
            {
                await Task.Delay(150);
                string outputDetails;
                lock (errorLogs)
                {
                    outputDetails = errorLogs.Count > 0 ? string.Join(Environment.NewLine, errorLogs) : string.Empty;
                }

                var isPermission = outputDetails.Contains("error 5", StringComparison.OrdinalIgnoreCase)
                    || outputDetails.Contains("PermissionDenied", StringComparison.OrdinalIgnoreCase)
                    || outputDetails.Contains("cannot open or create adapter", StringComparison.OrdinalIgnoreCase)
                    || (!Utils.IsAdministrator() && Utils.IsWindows());

                if (isPermission)
                {
                    throw new InvalidOperationException($"Zeptun TUN requires Administrator privileges to create network adapters. Please run MehrN as Administrator (Right click -> Run as administrator).\n{outputDetails}");
                }

                var code = _process.ExitCode;
                throw new InvalidOperationException($"Zeptun process exited unexpectedly (Exit code: {code}). {(string.IsNullOrWhiteSpace(outputDetails) ? "Check command-line arguments or system logs." : outputDetails)}");
            }

            _isRunning = true;
            await SafeNotifyAsync(updateFunc, false, $"Zeptun TUN engine started successfully (Interface: {interfaceName}, MTU: {mtu}, Target: 127.0.0.1:{socksPort}).");
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(ZeptunManager), ex);
            await StopAsync();
            await SafeNotifyAsync(updateFunc, true, $"Failed to start Zeptun TUN engine: {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        if (_process != null)
        {
            try
            {
                await _process.StopAsync();
                _process.Dispose();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(ZeptunManager), ex);
            }
            finally
            {
                _process = null;
            }
        }
        await Task.CompletedTask;
    }

    private static async Task SafeNotifyAsync(Func<bool, string, Task>? updateFunc, bool isError, string message)
    {
        if (updateFunc != null)
        {
            await updateFunc(isError, message);
        }
        NoticeManager.Instance.Enqueue(message);
    }
}
