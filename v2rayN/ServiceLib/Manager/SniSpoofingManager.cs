namespace ServiceLib.Manager;

/// <summary>
/// Hosts the official Patterniha SNI-Spoofing source runtime. It is deliberately
/// separate from the proxy core: the upstream implementation uses WinDivert to
/// observe the TCP handshake and inject its wrong-sequence ClientHello.
/// </summary>
public sealed class SniSpoofingManager
{
    private const string EngineFolder = "sni-spoofing";
    private const string EngineExe = "sni-spoofing.exe";
    private const string EngineScript = "main.py";
    private static readonly Lazy<SniSpoofingManager> _instance = new(() => new());
    public static SniSpoofingManager Instance => _instance.Value;

    private ProcessService? _process;
    private bool _isRunning;
    private string _activeProfileId = string.Empty;
    private string? _runningTargetIp;
    private int _runningTargetPort;

    public static bool CanUse(ProfileItem node)
    {
        return IsSupported(node)
               && Instance._isRunning
               && (Instance._activeProfileId.IsNullOrEmpty() || node.IndexId == Instance._activeProfileId);
    }

    public static bool IsSupported(ProfileItem? node)
    {
        var setting = AppManager.Instance.Config.SniSpoofingItem;
        if (!Utils.IsWindows() || !setting.Enabled)
        {
            return false;
        }
        if (!File.Exists(GetExePath()) && !File.Exists(GetScriptPath()))
        {
            return false;
        }
        if (node == null)
        {
            return true;
        }
        return node.ConfigType is EConfigType.VMess or EConfigType.VLESS or EConfigType.Trojan
               && (node.StreamSecurity == Global.StreamSecurity || node.Address == Global.Loopback);
    }

    public static string GetOutboundAddress(ProfileItem node) => CanUse(node) ? Global.Loopback : node.Address;

    public static int GetOutboundPort(ProfileItem node) => CanUse(node) ? AppManager.Instance.Config.SniSpoofingItem.ListenPort : node.Port;

    public async Task<bool> StartAsync(ProfileItem? node = null, Func<bool, string, Task>? updateFunc = null)
    {
        var setting = AppManager.Instance.Config.SniSpoofingItem;
        if (!setting.Enabled || !Utils.IsWindows())
        {
            await StopAsync();
            return true;
        }

        if (node != null && !IsSupported(node))
        {
            return true;
        }

        if (!File.Exists(GetExePath()) && !File.Exists(GetScriptPath()))
        {
            return false;
        }

        if (!Utils.IsAdministrator())
        {
            await SafeNotifyAsync(updateFunc, true, "SNI Spoofing requires MehrN to run as administrator. Approve the Windows prompt to continue.");
            if (ProcUtils.RebootAsAdmin())
            {
                await AppManager.Instance.AppExitAsync(true);
            }
            return false;
        }

        var targetIp = !setting.ConnectIp.IsNullOrEmpty()
            ? setting.ConnectIp.Trim()
            : (node != null && node.Address != Global.Loopback ? await ResolveIpv4Async(node.Address) : "188.114.98.0");

        if (string.IsNullOrEmpty(targetIp))
        {
            targetIp = "188.114.98.0";
        }

        var targetPort = !setting.ConnectIp.IsNullOrEmpty() && setting.ConnectPort is > 0 and <= 65535
            ? setting.ConnectPort
            : (node != null && node.Port > 0 ? node.Port : (setting.ConnectPort > 0 ? setting.ConnectPort : 443));

        if (_isRunning && _process != null && !_process.HasExited && _runningTargetIp == targetIp && _runningTargetPort == targetPort)
        {
            if (node != null)
            {
                _activeProfileId = node.IndexId;
            }
            return true;
        }

        await StopAsync();
        KillLingeringProcesses();
        EnsureDriverInstalled();

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

        var exePath = GetExePath();
        var scriptPath = GetScriptPath();

        if (File.Exists(exePath))
        {
            _process = new ProcessService(exePath, string.Empty, folder, true, false, null, updateFunc);
        }
        else if (File.Exists(scriptPath))
        {
            _process = new ProcessService(GetPythonExecutable(), "-X utf8 main.py", folder, true, false, null, updateFunc);
        }
        else
        {
            await SafeNotifyAsync(updateFunc, true, "SNI Spoofing engine binary or script was not found in bin/sni-spoofing.");
            return false;
        }

        try
        {
            await _process.StartAsync();
            for (var i = 0; i < 15; i++)
            {
                await Task.Delay(100);
                if (_process.HasExited)
                {
                    throw new InvalidOperationException("The SNI Spoofing engine exited unexpectedly.");
                }
            }

            _isRunning = true;
            _runningTargetIp = targetIp;
            _runningTargetPort = targetPort;
            _activeProfileId = node?.IndexId ?? string.Empty;
            await SafeNotifyAsync(updateFunc, false, $"SNI Spoofing enabled: {setting.ListenHost}:{setting.ListenPort} → {targetIp}:{targetPort}");
            return true;
        }
        catch (Win32Exception winEx) when (winEx.NativeErrorCode == 740)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = folder,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                _isRunning = true;
                _runningTargetIp = targetIp;
                _runningTargetPort = targetPort;
                _activeProfileId = node?.IndexId ?? string.Empty;
                await SafeNotifyAsync(updateFunc, false, $"SNI Spoofing enabled: {setting.ListenHost}:{setting.ListenPort} → {targetIp}:{targetPort}");
                return true;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(nameof(SniSpoofingManager), ex);
                await StopAsync();
                await SafeNotifyAsync(updateFunc, true, $"Failed to start SNI Spoofing: {ex.Message}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(SniSpoofingManager), ex);
            await StopAsync();
            await SafeNotifyAsync(updateFunc, true, $"Failed to start SNI Spoofing: {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _runningTargetIp = null;
        _runningTargetPort = 0;
        _activeProfileId = string.Empty;
        if (_process != null)
        {
            await _process.StopAsync();
            _process.Dispose();
            _process = null;
        }
        KillLingeringProcesses();
        await CleanupWinDivertAsync();
    }

    private static void EnsureDriverInstalled()
    {
        if (!Utils.IsWindows())
        {
            return;
        }
        try
        {
            var folder = GetEngineDirectory();
            var candidates = new[]
            {
                Path.Combine(folder, "WinDivert64.sys"),
                Path.Combine(folder, "_internal", "pydivert", "windivert_dll", "WinDivert64.sys"),
                Path.Combine(folder, "WinDivert.sys"),
                Path.Combine(folder, "_internal", "pydivert", "windivert_dll", "WinDivert.sys")
            };
            var source = candidates.FirstOrDefault(File.Exists);
            if (source != null)
            {
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var driversDir = Path.Combine(system32, "drivers");
                var driverDest = Path.Combine(driversDir, "WinDivert64.sys");
                try
                {
                    if (Directory.Exists(driversDir) && (!File.Exists(driverDest) || new FileInfo(source).Length != new FileInfo(driverDest).Length))
                    {
                        File.Copy(source, driverDest, true);
                    }
                }
                catch { }

                var sc = Path.Combine(system32, "sc.exe");
                if (File.Exists(sc))
                {
                    var driverPath = File.Exists(driverDest) ? driverDest : source;
                    var quoted = $"\"{driverPath}\"";
                    RunScCommand(sc, $"create WinDivert binPath= {quoted} type= kernel");
                    RunScCommand(sc, $"config WinDivert binPath= {quoted} type= kernel");
                    RunScCommand(sc, "start WinDivert");
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(SniSpoofingManager), ex);
        }
    }

    private static void KillLingeringProcesses()
    {
        if (!Utils.IsWindows())
        {
            return;
        }
        try
        {
            foreach (var p in Process.GetProcessesByName("sni-spoofing"))
            {
                try
                {
                    p.Kill(true);
                }
                catch { }
            }
        }
        catch { }
    }

    private static void RunScCommand(string scPath, string args)
    {
        try
        {
            using var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = scPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };
            proc.Start();
            proc.WaitForExit(3000);
        }
        catch { }
    }

    private static async Task SafeNotifyAsync(Func<bool, string, Task>? updateFunc, bool notify, string msg)
    {
        if (updateFunc == null)
        {
            return;
        }
        try
        {
            await updateFunc(notify, msg);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(SniSpoofingManager), ex);
        }
    }

    private static async Task CleanupWinDivertAsync()
    {
        if (!Utils.IsWindows())
        {
            return;
        }
        try
        {
            var sc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe");
            if (File.Exists(sc))
            {
                RunScCommand(sc, "stop WinDivert");
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(SniSpoofingManager), ex);
        }
        await Task.CompletedTask;
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
    private static string GetExePath() => Path.Combine(GetEngineDirectory(), EngineExe);
    private static string GetScriptPath() => Path.Combine(GetEngineDirectory(), EngineScript);
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
