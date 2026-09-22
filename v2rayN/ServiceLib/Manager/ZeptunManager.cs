namespace ServiceLib.Manager;

public sealed class ZeptunManager
{
    private static readonly Lazy<ZeptunManager> _instance = new(() => new());
    public static ZeptunManager Instance => _instance.Value;

    private const string _tag = "ZeptunManager";

    public const int Fwmark = 0x2022;

    private const int MaxExcludePrefixes = 32;

    private ProcessService? _process;
    private int _sudoPid = -1;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    #region Static helpers

    public static bool IsSelectedEngine(Config? config)
    {
        return config?.TunnelingItem?.SelectedCore == TunnelingItem.CoreZeptun;
    }

    public static bool OwnsTunDevice(Config? config)
    {
        return config?.TunModeItem?.EnableTun == true && IsSelectedEngine(config);
    }

    public static string? GetExePath(Config? config = null)
    {
        config ??= AppManager.Instance.Config;

        var configured = config?.TunnelingItem?.ZeptunPath?.TrimEx();
        if (configured.IsNotEmpty() && File.Exists(configured))
        {
            return configured;
        }

        var exeName = Utils.GetExeName(Global.ZeptunCoreName);

        var inSubDir = Utils.GetBinPath(exeName, Global.ZeptunCoreName);
        if (File.Exists(inSubDir))
        {
            return inSubDir;
        }

        var inBin = Utils.GetBinPath(exeName);
        if (File.Exists(inBin))
        {
            return inBin;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv.IsNotEmpty())
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var candidate = Path.Combine(dir.Trim(), exeName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }
        }

        return null;
    }

    public static bool IsAvailable(Config? config = null) => GetExePath(config).IsNotEmpty();

    #endregion Static helpers

    #region Start / Stop

    public async Task<bool> StartAsync(CoreConfigContext context, int socksPort, Func<bool, string, Task>? updateFunc)
    {
        await StopAsync();

        var config = context.AppConfig;
        var item = config.TunnelingItem ?? new TunnelingItem();

        var exePath = GetExePath(config);
        if (exePath.IsNullOrEmpty())
        {
            await Notify(updateFunc, true, string.Format(ResUI.MsgZeptunNotFound, Global.ZeptunProjectUrl));
            return false;
        }

        if (Utils.IsWindows() && !Utils.IsAdministrator())
        {
            await Notify(updateFunc, true, ResUI.MsgZeptunNeedAdministrator);
            return false;
        }

        if (Utils.IsNonWindows() && AppManager.Instance.LinuxSudoPwd.IsNullOrEmpty())
        {
            await Notify(updateFunc, true, ResUI.MsgZeptunNeedAdministrator);
            return false;
        }

        if (Utils.IsNonWindows() && exePath.StartsWith(Utils.GetBinPath(""), StringComparison.Ordinal))
        {
            await Utils.SetLinuxChmod(exePath);
        }

        await KillStaleProcessesAsync(exePath, updateFunc);

        var arguments = await BuildArgumentsAsync(context, item, socksPort, updateFunc);

        var recentOutput = new List<string>();

        async Task captureFunc(bool isError, string msg)
        {
            if (msg.IsNotEmpty())
            {
                lock (recentOutput)
                {
                    recentOutput.Add(msg.TrimEx());
                    if (recentOutput.Count > 30)
                    {
                        recentOutput.RemoveAt(0);
                    }
                }
            }

            if (updateFunc != null)
            {
                await updateFunc(isError, msg);
            }
        }

        try
        {
            await Notify(updateFunc, false, $"[Zeptun] {exePath} {arguments}");

            if (Utils.IsWindows())
            {
                _process = new ProcessService(
                    fileName: exePath,
                    arguments: arguments,
                    workingDirectory: Path.GetDirectoryName(exePath) ?? Utils.GetBinConfigPath(),
                    displayLog: true,
                    redirectInput: false,
                    environmentVars: null,
                    updateFunc: captureFunc);
                await _process.StartAsync();
            }
            else
            {
                _process = await CoreAdminManager.Instance.RunCommandAsLinuxSudo(
                    "run_zeptun_as_sudo.sh",
                    $"{exePath.AppendQuotes()} {arguments}",
                    captureFunc);
                _sudoPid = _process?.Id ?? -1;
            }

            if (_process is null)
            {
                throw new Exception(ResUI.MsgZeptunStartFailed);
            }

            for (var i = 0; i < 20 && !_process.HasExited; i++)
            {
                await Task.Delay(100);
            }

            if (_process.HasExited)
            {
                await Task.Delay(200);
                string details;
                lock (recentOutput)
                {
                    details = string.Join(Environment.NewLine, recentOutput);
                }
                Logging.SaveLog($"{_tag} exited immediately: {details}");
                throw new Exception(details.IsNullOrEmpty() ? ResUI.MsgZeptunStartFailed : details);
            }

            _isRunning = true;
            await Notify(updateFunc, false, string.Format(ResUI.MsgZeptunStarted, socksPort));
            return true;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            await StopAsync();
            await Notify(updateFunc, true, $"{ResUI.MsgZeptunStartFailed}{Environment.NewLine}{ex.Message}");
            return false;
        }
    }

    public async Task StopAsync()
    {
        _isRunning = false;

        if (_sudoPid > 0)
        {
            await CoreAdminManager.Instance.KillPidAsLinuxSudo(_sudoPid);
            _sudoPid = -1;
        }

        if (_process != null)
        {
            try
            {
                await _process.StopAsync();
                _process.Dispose();
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            finally
            {
                _process = null;
            }
        }
    }

    #endregion Start / Stop

    #region Private

    private static async Task<string> BuildArgumentsAsync(
        CoreConfigContext context,
        TunnelingItem item,
        int socksPort,
        Func<bool, string, Task>? updateFunc)
    {
        var config = context.AppConfig;
        var tun = config.TunModeItem;

        var interfaceName = item.ZeptunInterfaceName.TrimEx().NullIfEmpty() ?? Global.ZeptunDefaultInterfaceName;
        if (interfaceName.Length > 15)
        {
            interfaceName = interfaceName[..15];
        }

        var stack = Utils.IsLinux() && Global.ZeptunStacks.Contains(item.ZeptunStack)
            ? item.ZeptunStack
            : Global.ZeptunStacks.First();

        var mtu = tun.Mtu is >= 576 and <= 65535 ? tun.Mtu : 1500;

        var args = new StringBuilder();
        args.Append("run");
        args.Append($" --tun {interfaceName}");
        args.Append($" --mtu {mtu}");
        args.Append($" --stack {stack}");
        args.Append($" --socks5 {Global.Loopback}:{socksPort}");

        var udpMode = Global.ZeptunUdpModes.Contains(item.ZeptunUdpMode)
            ? item.ZeptunUdpMode
            : Global.ZeptunUdpModes.First();
        args.Append($" --socks5-udp-mode {udpMode}");

        var address4 = tun.IPv4Address.NullIfEmpty() ?? Global.TunIPv4Address.First();
        args.Append($" --address {address4}");
        if (tun.EnableIPv6Address)
        {
            var address6 = tun.IPv6Address.NullIfEmpty() ?? Global.TunIPv6Address.First();
            args.Append($" --address {address6}");
        }

        if (tun.AutoRoute)
        {
            args.Append(" --auto-route");
        }
        if (tun.StrictRoute)
        {
            args.Append(" --strict-route");
        }
        if (item.ZeptunDnsHijack)
        {
            var upstream = item.ZeptunDnsUpstream.TrimEx().NullIfEmpty() ?? Global.ZeptunDefaultDnsUpstream;
            args.Append($" --dns-hijack --dns-upstream {upstream}");
        }
        if (item.ZeptunFakeIp)
        {
            args.Append(" --fake-ip");
        }

        var logLevel = Global.ZeptunLogLevels.Contains(item.ZeptunLogLevel) ? item.ZeptunLogLevel : "warn";
        args.Append($" --log-level {logLevel}");

        foreach (var prefix in await BuildExcludePrefixesAsync(context, updateFunc))
        {
            args.Append($" --exclude {prefix}");
        }

        var extra = item.ExtraArguments.TrimEx();
        if (extra.IsNotEmpty())
        {
            args.Append($" {extra}");
        }

        return args.ToString();
    }

    private static async Task<List<string>> BuildExcludePrefixesAsync(
        CoreConfigContext context,
        Func<bool, string, Task>? updateFunc)
    {
        var prefixes = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? prefix)
        {
            if (prefix.IsNullOrEmpty() || !seen.Add(prefix))
            {
                return;
            }
            prefixes.Add(prefix);
        }

        foreach (var addr in context.AppConfig.TunModeItem.RouteExcludeAddress ?? [])
        {
            var value = addr.TrimEx();
            if (value.IsNullOrEmpty())
            {
                continue;
            }
            try
            {
                IPNetwork2.Parse(value);
                Add(value);
            }
            catch
            {
            }
        }

        foreach (var host in CollectServerAddresses(context))
        {
            foreach (var ip in await ResolveAsync(host))
            {
                Add(ToHostPrefix(ip));
            }
        }

        if (prefixes.Count > MaxExcludePrefixes)
        {
            await Notify(updateFunc, false,
                string.Format(ResUI.MsgZeptunExcludeTruncated, prefixes.Count, MaxExcludePrefixes));
            prefixes = prefixes.Take(MaxExcludePrefixes).ToList();
        }

        return prefixes;
    }

    private static List<string> CollectServerAddresses(CoreConfigContext context)
    {
        var hosts = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var candidates = context.AllProxiesMap.Values.Select(p => p?.Address).ToList();
        candidates.Add(context.Node?.Address);

        foreach (var candidate in candidates)
        {
            var host = candidate?.TrimEx();
            if (host.IsNullOrEmpty() || host == Global.Loopback || host == "localhost")
            {
                continue;
            }
            if (IPAddress.TryParse(host, out var parsed)
                && (IPAddress.IsLoopback(parsed) || parsed.Equals(IPAddress.Any) || parsed.Equals(IPAddress.IPv6Any)))
            {
                continue;
            }
            if (seen.Add(host))
            {
                hosts.Add(host);
            }
        }

        return hosts;
    }

    private static async Task<List<IPAddress>> ResolveAsync(string host)
    {
        if (IPAddress.TryParse(host, out var literal))
        {
            return [literal];
        }

        if (!Utils.IsDomain(host))
        {
            return [];
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var addresses = await Dns.GetHostAddressesAsync(host, cts.Token);
            return [.. addresses];
        }
        catch (Exception ex)
        {
            Logging.SaveLog($"{_tag} resolve {host}", ex);
            return [];
        }
    }

    private static async Task KillStaleProcessesAsync(string exePath, Func<bool, string, Task>? updateFunc)
    {
        if (!Utils.IsLinux())
        {
            return;
        }

        foreach (var pid in FindProcessIds(exePath))
        {
            await Notify(updateFunc, false, string.Format(ResUI.MsgZeptunStaleProcess, pid));
            await CoreAdminManager.Instance.KillPidAsLinuxSudo(pid);
        }
    }

    private static List<int> FindProcessIds(string exePath)
    {
        var pids = new List<int>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories("/proc"))
            {
                var name = Path.GetFileName(dir);
                if (!int.TryParse(name, out var pid) || pid == Environment.ProcessId)
                {
                    continue;
                }
                try
                {
                    var cmdline = File.ReadAllText(Path.Combine(dir, "cmdline")).Replace('\0', ' ');
                    if (cmdline.Contains(exePath, StringComparison.Ordinal))
                    {
                        pids.Add(pid);
                    }
                }
                catch
                {
                }
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return pids;
    }

    private static string ToHostPrefix(IPAddress ip)
    {
        return ip.AddressFamily == AddressFamily.InterNetworkV6 ? $"{ip}/128" : $"{ip}/32";
    }

    private static async Task Notify(Func<bool, string, Task>? updateFunc, bool isError, string message)
    {
        Logging.SaveLog($"{_tag} {message}");
        if (updateFunc != null)
        {
            await updateFunc(isError, message + Environment.NewLine);
        }
        if (isError)
        {
            NoticeManager.Instance.Enqueue(message);
        }
    }

    #endregion Private
}
