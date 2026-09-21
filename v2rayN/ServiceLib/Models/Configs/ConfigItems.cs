namespace ServiceLib.Models.Configs;

[Serializable]
public class CoreBasicItem
{
    public bool LogEnabled { get; set; }

    public string Loglevel { get; set; }

    public string DefFingerprint { get; set; }

    public string DefUserAgent { get; set; }

    public string? SendThrough { get; set; }

    public string? BindInterface { get; set; }

    public bool EnableFragment { get; set; }

    public bool EnableFinalFragment { get; set; }

    public bool EnableCacheFile4Sbox { get; set; } = true;
}

[Serializable]
public class InItem
{
    public int LocalPort { get; set; }
    public string Protocol { get; set; }
    public bool UdpEnabled { get; set; }
    public bool SniffingEnabled { get; set; } = true;
    // PattN: quic sniffing is on by default
    public List<string>? DestOverride { get; set; } = ["http", "tls", "quic"];
    public bool RouteOnly { get; set; }
    public bool AllowLANConn { get; set; }
    public bool NewPort4LAN { get; set; }
    public string User { get; set; }
    public string Pass { get; set; }
    public bool SecondLocalPortEnabled { get; set; }
}

[Serializable]
public class KcpItem
{
    public int Mtu { get; set; }

    public int Tti { get; set; }

    public int UplinkCapacity { get; set; }

    public int DownlinkCapacity { get; set; }

    public int CwndMultiplier { get; set; }

    public int MaxSendingWindow { get; set; }
}

[Serializable]
public class GrpcItem
{
    public int? IdleTimeout { get; set; }
    public int? HealthCheckTimeout { get; set; }
    public bool? PermitWithoutStream { get; set; }
    public int? InitialWindowsSize { get; set; }
}

[Serializable]
public class GUIItem
{
    public bool AutoRun { get; set; }
    public bool EnableStatistics { get; set; }
    public bool DisplayRealTimeSpeed { get; set; }
    public bool KeepOlderDedupl { get; set; }
    public int AutoUpdateInterval { get; set; }
    public int TrayMenuServersLimit { get; set; } = 20;
    public bool EnableHWA { get; set; } = false;
    public bool EnableLog { get; set; } = true;
    public string? RootCertProvider { get; set; }
}

[Serializable]
public class MsgUIItem
{
    public string? MainMsgFilter { get; set; }
    public bool? AutoRefresh { get; set; }
}

[Serializable]
public class UIItem
{
    public bool EnableAutoAdjustMainLvColWidth { get; set; }
    public int MainGirdHeight1 { get; set; }
    public int MainGirdHeight2 { get; set; }
    public EGirdOrientation MainGirdOrientation { get; set; } = EGirdOrientation.Vertical;
    public string? ColorPrimaryName { get; set; }
    public string? CurrentTheme { get; set; }
    public string CurrentLanguage { get; set; }
    public string CurrentFontFamily { get; set; }
    public int CurrentFontSize { get; set; }
    public bool EnableDragDropSort { get; set; }
    public bool DoubleClick2Activate { get; set; }
    public bool AutoHideStartup { get; set; }
    public bool Hide2TrayWhenClose { get; set; }
    public bool MacOSShowInDock { get; set; }
    public List<ColumnItem> MainColumnItem { get; set; }
    public List<WindowSizeItem> WindowSizeItem { get; set; }
    public bool HideColumnIpInfo { get; set; }
    public bool ShowBottomLog { get; set; } = true;
}

[Serializable]
public class ConstItem
{
    public string? SubConvertUrl { get; set; }
    public string? GeoSourceUrl { get; set; }
    public string? SrsSourceUrl { get; set; }
    public string? RouteRulesTemplateSourceUrl { get; set; }
}

[Serializable]
public class KeyEventItem
{
    public EGlobalHotkey EGlobalHotkey { get; set; }

    public bool Alt { get; set; }

    public bool Control { get; set; }

    public bool Shift { get; set; }

    public int? KeyCode { get; set; }
}

[Serializable]
public class CoreTypeItem
{
    public EConfigType ConfigType { get; set; }

    public ECoreType CoreType { get; set; }
}

[Serializable]
public class TunModeItem
{
    public bool EnableTun { get; set; }
    public bool AutoRoute { get; set; } = true;
    public bool StrictRoute { get; set; } = true;
    public string Stack { get; set; }
    public int Mtu { get; set; }
    public bool EnableIPv6Address { get; set; }
    public string IcmpRouting { get; set; }
    public bool EnableLegacyProtect { get; set; } = true;
    public List<string>? RouteExcludeAddress { get; set; }
    public string IPv4Address { get; set; }
    public string IPv6Address { get; set; }
}

[Serializable]
public class SpeedTestItem
{
    public int SpeedTestTimeout { get; set; }
    public string SpeedTestUrl { get; set; }
    public string SpeedPingTestUrl { get; set; }
    public int MixedConcurrencyCount { get; set; }
    public string IPAPIUrl { get; set; }
    public string UdpTestTarget { get; set; }
    public int? SpeedTestPageSize { get; set; }
    public int? SpeedTestDelayInterval { get; set; }
}

[Serializable]
public class RoutingBasicItem
{
    public string DomainStrategy { get; set; }
    public string DomainStrategy4Singbox { get; set; }
    public string RoutingIndexId { get; set; }
    public bool? DefaultRoutingMigratedToGlobal { get; set; }
}

[Serializable]
public class ColumnItem
{
    public string Name { get; set; }
    public int Width { get; set; }
    public int Index { get; set; }
}

[Serializable]
public class Mux4RayItem
{
    public int? Concurrency { get; set; }
    public int? XudpConcurrency { get; set; }
    public string? XudpProxyUDP443 { get; set; }
}

[Serializable]
public class Mux4SboxItem
{
    public string Protocol { get; set; }
    public int MaxConnections { get; set; }
    public bool? Padding { get; set; }
}

[Serializable]
public class HysteriaItem
{
    public int UpMbps { get; set; }
    public int DownMbps { get; set; }
    public int HopInterval { get; set; } = Global.Hysteria2DefaultHopInt;
}

[Serializable]
public class ClashUIItem
{
    public bool EnableIPv6 { get; set; }
    public bool EnableMixinContent { get; set; }
    public int ProxiesSorting { get; set; }
    public bool ProxiesAutoRefresh { get; set; }
    public int ProxiesRefreshInterval { get; set; } = 2;
    public bool ConnectionsAutoRefresh { get; set; }
    public int ConnectionsRefreshInterval { get; set; } = 2;
    public List<ColumnItem> ConnectionsColumnItem { get; set; }
}

[Serializable]
public class SystemProxyItem
{
    public ESysProxyType SysProxyType { get; set; }
    public string SystemProxyExceptions { get; set; }
    public bool NotProxyLocalAddress { get; set; } = true;
    public string SystemProxyAdvancedProtocol { get; set; }
    public string? CustomSystemProxyPacPath { get; set; }
    public string? CustomSystemProxyScriptPath { get; set; }
}

[Serializable]
public class WebDavItem
{
    public string? Url { get; set; }
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string? DirName { get; set; }
}

[Serializable]
public class CheckUpdateItem
{
    public bool CheckPreReleaseUpdate { get; set; }
    public bool UpdateViaProxy { get; set; } = true;
    public List<string>? SelectedCoreTypes { get; set; }
}

[Serializable]
public class Fragment4RayItem
{
    public string? Packets { get; set; }
    public List<string>? Lengths { get; set; }
    public List<string>? Delays { get; set; }
    public string? MaxSplit { get; set; }

    // For migration from old version, remove those properties in the future
    public string? Length { get; set; }

    public string? Interval { get; set; }
    // migration end
}

[Serializable]
public class WindowSizeItem
{
    public string TypeName { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

[Serializable]
public class SimpleDNSItem
{
    public bool? UseSystemHosts { get; set; }
    public bool? AddCommonHosts { get; set; }
    public bool? FakeIP { get; set; }
    public bool? GlobalFakeIp { get; set; }
    public string? FakeIPRange { get; set; }
    public bool? BlockBindingQuery { get; set; }
    public bool? BlockAAAAQuery { get; set; }
    public string? DirectDNS { get; set; }
    public string? RemoteDNS { get; set; }
    public string? BootstrapDNS { get; set; }
    public string? Strategy4Freedom { get; set; }
    public string? Strategy4Proxy { get; set; }
    public string? Strategy4ProxyDial { get; set; }
    public bool? ServeStale { get; set; }
    public bool? ParallelQuery { get; set; }
    public string? Hosts { get; set; }
    public string? DirectExpectedIPs { get; set; }
    public bool? EnableHappyEyeballs { get; set; }
}

[Serializable]
public class HappyEyeballs4RayItem
{
    public int? TryDelayMs { get; set; }
    public bool? PrioritizeIPv6 { get; set; }
    public int? Interleave { get; set; }
    public int? MaxConcurrentTry { get; set; }
}

/// <summary>
/// Settings for the bundled Patterniha SNI-Spoofing forwarder.
/// The forwarder is Windows-only because it uses WinDivert to inject the
/// intentionally out-of-window TLS ClientHello.
/// </summary>
[Serializable]
public class SniSpoofingItem
{
    public bool Enabled { get; set; } = false;
    public string Engine { get; set; } = "Rust";
    public string ListenHost { get; set; } = "127.0.0.1";
    public int ListenPort { get; set; } = 40443;
    public string ConnectIp { get; set; } = "188.114.98.0";
    public int ConnectPort { get; set; } = 443;
    public string FakeSni { get; set; } = "auth.vercel.com";
}

[Serializable]
public class MhrItem
{
    public bool Enabled { get; set; }
    public string Variant { get; set; } = "mhr";
    public string ScriptId { get; set; } = string.Empty;
    public string AuthKey { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 8085;
    public int Socks5Port { get; set; } = 1080;
    // Identifies the SOCKS profile owned by the local MHR runtime. Keeping this
    // stable lets saving settings update the same profile instead of adding one
    // on every start.
    public string ProfileId { get; set; } = string.Empty;
}

[Serializable]
public class TunnelingItem
{
    public const string CoreSingbox = "Built-in (sing-box)";
    public const string CoreZeptun = "Zeptun (Noisemux/zeptun)";

    public string SelectedCore { get; set; } = CoreSingbox;
    public string ZeptunPath { get; set; } = string.Empty;
    public string ZeptunInterfaceName { get; set; } = "zeptun0";
    public int ZeptunMtu { get; set; } = 1500;
    public string ZeptunStack { get; set; } = "userspace";
    public bool ZeptunAutoRoute { get; set; } = true;
    public bool ZeptunStrictRoute { get; set; } = false;
    public bool ZeptunDnsHijack { get; set; } = true;
    public bool ZeptunFakeIp { get; set; } = true;
    public string ExtraArguments { get; set; } = string.Empty;
}
