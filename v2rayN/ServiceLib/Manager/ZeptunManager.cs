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

    public async Task<bool> StartAsync(int socksPort, Func<bool, string, Task>? updateFunc = null)
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
            return false;
        }

        var exePath = GetZeptunExePath();
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
        {
            await SafeNotifyAsync(updateFunc, true, $"Zeptun executable was not found. Please download it from {ProjectUrl} and place zeptun.exe in the bin/zeptun/ folder, or configure its path in L.C Settings -> Tunneling Core Settings.");
            return false;
        }

        var interfaceName = string.IsNullOrWhiteSpace(item?.ZeptunInterfaceName) ? "zeptun0" : item.ZeptunInterfaceName.Trim();
        var mtu = item?.ZeptunMtu > 0 ? item.ZeptunMtu : 8500;
        var stack = string.IsNullOrWhiteSpace(item?.ZeptunStack) ? "userspace" : item.ZeptunStack.Trim();

        var arguments = $"run --tun {interfaceName} --mtu {mtu} --socks5 127.0.0.1:{socksPort} --stack {stack}";
        if (item?.ZeptunAutoRoute ?? true)
        {
            arguments += " --auto-route";
        }

        if (!string.IsNullOrWhiteSpace(item?.ExtraArguments))
        {
            arguments += $" {item.ExtraArguments.Trim()}";
        }

        var workingDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;

        try
        {
            await SafeNotifyAsync(updateFunc, false, $"Starting Zeptun TUN engine ({exePath} {arguments})...");
            _process = new ProcessService(exePath, arguments, workingDir, true, false, null, updateFunc);
            await _process.StartAsync();

            await Task.Delay(300);
            if (_process.HasExited)
            {
                throw new InvalidOperationException("The Zeptun process exited unexpectedly shortly after launch. Check permissions or command-line parameters.");
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
