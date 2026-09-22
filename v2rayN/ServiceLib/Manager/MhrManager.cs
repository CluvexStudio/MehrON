using ServiceLib.ViewModels;

namespace ServiceLib.Manager;

/// <summary>
/// Hosts the upstream MasterHttpRelayVPN or mhr-cfw Python relay.
/// Each start copies the selected upstream config.example.json to config.json,
/// then writes only the user-owned local credentials and listener ports.
/// </summary>
public sealed class MhrManager
{
    private static readonly Lazy<MhrManager> _instance = new(() => new());
    public static MhrManager Instance => _instance.Value;

    private ProcessService? _process;

    public async Task<bool> StartAsync(Func<bool, string, Task>? updateFunc)
    {
        var settings = AppManager.Instance.Config.MhrItem;
        if (!settings.Enabled)
        {
            await StopAsync();
            return true;
        }
        if (Utils.IsWindows() && !Utils.IsAdministrator())
        {
            await NotifyAsync(updateFunc, true, "MHR requires MehrON to run as administrator. Approve the Windows prompt to continue.");
            if (ProcUtils.RebootAsAdmin())
            {
                await AppManager.Instance.AppExitAsync(true);
            }
            return false;
        }

        var directory = GetVariantDirectory(settings.Variant);
        var main = Path.Combine(directory, "main.py");
        if (!File.Exists(main))
        {
            await NotifyAsync(updateFunc, true, "The selected MHR runtime is not bundled. Build a release package or restore the bin/mhr runtime files.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(settings.ScriptId) || string.IsNullOrWhiteSpace(settings.AuthKey))
        {
            await NotifyAsync(updateFunc, true, "MHR requires both the Apps Script Deployment ID and the matching AUTH_KEY.");
            return false;
        }

        if (!await WriteConfigAsync(settings, directory, updateFunc))
        {
            return false;
        }

        // Create/update a visible, selectable local SOCKS profile as soon as the
        // relay configuration is saved. This is intentionally before Python is
        // launched, so the user can see and select the connection even when the
        // host still needs Python or relay dependencies installed.
        try
        {
            await EnsureSocksProfileAsync(settings);
            await StopAsync();
            _process = new ProcessService(GetPythonExecutable(), "main.py --config config.json", directory, true, false, null, updateFunc);
            await _process.StartAsync();
            await Task.Delay(250);
            if (_process.HasExited)
            {
                throw new InvalidOperationException("The MHR process exited immediately. Install Python 3.10+ and the bundled requirements.");
            }
            await NotifyAsync(updateFunc, false, $"{GetDisplayName(settings.Variant)} started on 127.0.0.1:{settings.HttpPort} (SOCKS5 {settings.Socks5Port}).");
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(MhrManager), ex);
            await StopAsync();
            await NotifyAsync(updateFunc, true, $"Unable to start {GetDisplayName(settings.Variant)}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> WriteConfigAsync(MhrItem settings, string? directory = null, Func<bool, string, Task>? updateFunc = null)
    {
        directory ??= GetVariantDirectory(settings.Variant);
        var example = Path.Combine(directory, "config.example.json");
        var config = Path.Combine(directory, "config.json");
        if (!File.Exists(example))
        {
            await NotifyAsync(updateFunc, true, "The selected MHR config.example.json is not available.");
            return false;
        }

        try
        {
            File.Copy(example, config, true);
            var root = JsonNode.Parse(await File.ReadAllTextAsync(config))?.AsObject()
                ?? throw new InvalidOperationException("The selected config.example.json is invalid.");
            root["script_id"] = settings.ScriptId.Trim();
            root["auth_key"] = settings.AuthKey;
            if (settings.Variant == "mhr")
            {
                root["http_port"] = settings.HttpPort;
            }
            else
            {
                root["listen_port"] = settings.HttpPort;
            }
            root["socks5_port"] = settings.Socks5Port;
            await File.WriteAllTextAsync(config, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(nameof(MhrManager), ex);
            await NotifyAsync(updateFunc, true, $"Unable to create MHR config.json: {ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        if (_process == null)
        {
            return;
        }
        await _process.StopAsync();
        _process.Dispose();
        _process = null;
    }

    /// <summary>
    /// Stops the local relay and persists it as disabled, so it cannot be
    /// silently started again on the next elevated MehrON launch.
    /// </summary>
    public async Task StopAndDisableAsync()
    {
        await StopAsync();

        var config = AppManager.Instance.Config;
        config.MhrItem.Enabled = false;
        if (config.SystemProxyItem.SysProxyType != ESysProxyType.Unchanged)
        {
            config.SystemProxyItem.SysProxyType = ESysProxyType.ForcedClear;
            await ServiceLib.Handler.SysProxy.SysProxyHandler.UpdateSysProxy(config, false);
        }
        await ConfigHandler.SaveConfig(config);
    }

    private static async Task EnsureSocksProfileAsync(MhrItem settings)
    {
        var profile = new ProfileItem
        {
            IndexId = settings.ProfileId,
            ConfigType = EConfigType.SOCKS,
            Address = Global.Loopback,
            Port = settings.Socks5Port,
            Remarks = settings.Variant == "mhr-cfw" ? "MHR-CFW Local SOCKS5" : "MHR Local SOCKS5",
        };

        if (await ConfigHandler.AddSocksServer(AppManager.Instance.Config, profile) != 0)
        {
            throw new InvalidOperationException("Unable to create the local MHR SOCKS5 profile.");
        }

        settings.ProfileId = profile.IndexId;
        await ConfigHandler.SetDefaultServerIndex(AppManager.Instance.Config, profile.IndexId);
        StatusBarViewModel.Instance.SetDefaultServerRequested.Publish(profile.IndexId);
    }

    private static string GetVariantDirectory(string variant) => Utils.GetBinPath(Path.Combine("mhr", variant == "mhr-cfw" ? "mhr-cfw" : "mhr"));
    private static string GetDisplayName(string variant) => variant == "mhr-cfw" ? "MHR-CFW" : "MHR";
    private static string GetPythonExecutable()
    {
        if (Utils.IsWindows())
        {
            var pythonRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
            if (Directory.Exists(pythonRoot))
            {
                var python = Directory.GetDirectories(pythonRoot, "Python3*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, "python.exe"))
                    .FirstOrDefault(File.Exists);
                if (!string.IsNullOrEmpty(python))
                {
                    return python;
                }
            }
        }

        // Retain PATH lookup for portable and non-Windows installations.
        return "python";
    }
    private static Task NotifyAsync(Func<bool, string, Task>? updateFunc, bool notify, string message) => updateFunc?.Invoke(notify, message) ?? Task.CompletedTask;
}
