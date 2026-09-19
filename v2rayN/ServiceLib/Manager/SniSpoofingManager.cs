namespace ServiceLib.Manager;

/// <summary>
/// Hosts the official Patterniha SNI-Spoofing source runtime. It is deliberately
/// separate from the proxy core: the upstream implementation uses WinDivert to
/// observe the TCP handshake and inject its wrong-sequence ClientHello.
/// </summary>
public sealed class SniSpoofingManager
{
    private const string EngineFolder = "sni-spoofing";
    private const string EngineFile = "main.py";
    private static readonly Lazy<SniSpoofingManager> _instance = new(() => new());
    public static SniSpoofingManager Instance => _instance.Value;

    private ProcessService? _process;
    private bool _isRunning;
    private string _activeProfileId = string.Empty;

    public static bool CanUse(ProfileItem node)
    {
        return IsSupported(node)
               && Instance._isRunning
               && node.IndexId == Instance._activeProfileId;
    }

    private static bool IsSupported(ProfileItem node)
    {
        var setting = AppManager.Instance.Config.SniSpoofingItem;
        return Utils.IsWindows()
               && setting.Enabled
               && node.ConfigType is EConfigType.VMess or EConfigType.VLESS or EConfigType.Trojan
               && node.StreamSecurity == Global.StreamSecurity
               && File.Exists(GetEnginePath());
    }

    public static string GetOutboundAddress(ProfileItem node) => CanUse(node) ? Global.Loopback : node.Address;

    public static int GetOutboundPort(ProfileItem node) => CanUse(node) ? AppManager.Instance.Config.SniSpoofingItem.ListenPort : node.Port;

    public async Task<bool> StartAsync(ProfileItem node, Func<bool, string, Task>? updateFunc)
    {
        if (!IsSupported(node))
        {
            return true;
        }
        if (!Utils.IsAdministrator())
        {
            await updateFunc?.Invoke(true, "SNI Spoofing requires MehrN to be run as administrator.");
            return false;
        }

        await StopAsync();
        var setting = AppManager.Instance.Config.SniSpoofingItem;
        var targetIp = setting.ConnectIp.IsNullOrEmpty()
            ? await ResolveIpv4Async(node.Address)
            : setting.ConnectIp.Trim();
        if (targetIp == null)
        {
            await updateFunc?.Invoke(true, "SNI Spoofing could not resolve the selected server to an IPv4 address.");
            return false;
        }

        var targetPort = !setting.ConnectIp.IsNullOrEmpty() && setting.ConnectPort is > 0 and <= 65535
            ? setting.ConnectPort
            : (node.Port > 0 ? node.Port : (setting.ConnectPort > 0 ? setting.ConnectPort : 443));

        var folder = GetEngineDirectory();
        var configPath = Path.Combine(folder, "config.json");
        var content = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["LISTEN_HOST"] = setting.ListenHost,
            ["LISTEN_PORT"] = setting.ListenPort,
            ["CONNECT_IP"] = targetIp,
            ["CONNECT_PORT"] = targetPort,
            ["FAKE_SNI"] = setting.FakeSni,
        }, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, content);

        // The upstream PyInstaller executable crashes on Windows consoles that
        // use CP1252 because it prints Persian text. Running the official source
        // under Python with UTF-8 enabled avoids that crash.
        _process = new ProcessService(GetPythonExecutable(), "-X utf8 main.py", folder, true, false, null, updateFunc);
        try
        {
            await _process.StartAsync();
            for (var i = 0; i < 10; i++)
            {
                await Task.Delay(100);
                if (_process.HasExited)
                {
                    throw new InvalidOperationException("The SNI Spoofing engine exited immediately. Ensure MehrN is running as Administrator and Python with pydivert is installed.");
                }
            }

            _isRunning = true;
            _activeProfileId = node.IndexId;
            await updateFunc?.Invoke(false, $"SNI Spoofing enabled: {setting.ListenHost}:{setting.ListenPort} → {targetIp}:{targetPort}");
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(SniSpoofingManager), ex);
            await StopAsync();
            await updateFunc?.Invoke(true, $"Failed to start SNI Spoofing: {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _activeProfileId = string.Empty;
        if (_process == null)
        {
            return;
        }
        await _process.StopAsync();
        _process.Dispose();
        _process = null;
    }

    private static async Task<string?> ResolveIpv4Async(string address)
    {
        if (IPAddress.TryParse(address, out var parsed))
        {
            return parsed.AddressFamily == AddressFamily.InterNetwork ? parsed.ToString() : null;
        }
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(address);
            return addresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string GetEngineDirectory() => Utils.GetBinPath(EngineFolder);
    private static string GetEnginePath() => Path.Combine(GetEngineDirectory(), EngineFile);
    private static string GetPythonExecutable()
    {
        if (Utils.IsWindows())
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var searchPaths = new List<string>
            {
                Path.Combine(progFiles, "PyManager", "python.exe"),
                Path.Combine(localApp, "Programs", "Python"),
                Path.Combine(localApp, "Python"),
                Path.Combine(progFiles, "Python")
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
                if (Directory.Exists(path))
                {
                    var py = Directory.GetDirectories(path, "*python*", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
                        .Select(d => Path.Combine(d, "python.exe"))
                        .FirstOrDefault(File.Exists);
                    if (!string.IsNullOrEmpty(py))
                    {
                        return py;
                    }
                }
            }
        }
        return "python";
    }
}
