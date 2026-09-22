namespace ServiceLib.ViewModels;

public partial class AddAetherServerViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial ProfileItem SelectedSource { get; set; }

    [Reactive]
    public partial string SelectedProtocol { get; set; }

    [Reactive]
    public partial string SelectedScanMode { get; set; }

    [Reactive]
    public partial string SelectedNoize { get; set; }

    [Reactive]
    public partial string PeerEndpoint { get; set; }

    [Reactive]
    public partial string PreSocksPort { get; set; }

    [Reactive]
    public partial bool DisplayLog { get; set; }

    public List<string> Protocols { get; } =
    [
        "WARP-in-WARP (gool)",
        "WireGuard",
        "MASQUE (QUIC / HTTP-3)",
        "MASQUE over TCP (HTTP/2)",
        "MASQUE-in-MASQUE (MIM)"
    ];

    public List<string> ScanModes { get; } =
    [
        "balanced",
        "turbo",
        "thorough",
        "stealth"
    ];

    public List<string> NoizeProfiles { get; } =
    [
        "balanced",
        "aggressive",
        "light",
        "off"
    ];

    public ReactiveCommand<RxVoid, RxVoid> SaveServerCmd { get; }

    public AddAetherServerViewModel(ProfileItem profileItem)
    {
        _config = AppManager.Instance.Config;
        SelectedSource = profileItem.IndexId.IsNullOrEmpty() ? profileItem : JsonUtils.DeepCopy(profileItem);

        var extra = SelectedSource.GetProtocolExtra();
        SelectedProtocol = ProtocolFromCode(extra?.AetherProtocol);
        SelectedScanMode = extra?.AetherScan.IsNullOrEmpty() == false ? extra.AetherScan : "balanced";
        SelectedNoize = extra?.AetherNoize.IsNullOrEmpty() == false ? extra.AetherNoize : "balanced";
        PeerEndpoint = extra?.AetherPeer ?? string.Empty;
        PreSocksPort = (SelectedSource.PreSocksPort is > 0 and <= 65535 ? SelectedSource.PreSocksPort.Value : 1819).ToString();
        DisplayLog = SelectedSource.DisplayLog;

        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            SelectedSource.Remarks = "Aether WARP";
        }

        SaveServerCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveServerAsync();
        });
    }

    private static string ProtocolFromCode(string? code) => code switch
    {
        "masque-h2" => "MASQUE over TCP (HTTP/2)",
        "mim" => "MASQUE-in-MASQUE (MIM)",
        "wg" => "WireGuard",
        "masque" => "MASQUE (QUIC / HTTP-3)",
        _ => "WARP-in-WARP (gool)",
    };

    private static string ProtocolToCode(string name) => name switch
    {
        "MASQUE over TCP (HTTP/2)" => "masque-h2",
        "MASQUE-in-MASQUE (MIM)" => "mim",
        "WireGuard" => "wg",
        "WARP-in-WARP (gool)" => "gool",
        _ => "masque",
    };

    private async Task SaveServerAsync()
    {
        if (SelectedSource.Remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        if (!int.TryParse(PreSocksPort, out var port) || port <= 0 || port > 65535)
        {
            port = 1819;
        }

        SelectedSource.ConfigType = EConfigType.Custom;
        SelectedSource.CoreType = ECoreType.aether;
        SelectedSource.PreSocksPort = port;
        SelectedSource.DisplayLog = DisplayLog;
        SelectedSource.Port = port;
        SelectedSource.Address = PeerEndpoint.IsNotEmpty() ? PeerEndpoint.TrimEx() : Global.Loopback;

        var extra = (SelectedSource.GetProtocolExtra() ?? new ProtocolExtraItem()) with
        {
            AetherProtocol = ProtocolToCode(SelectedProtocol),
            AetherScan = SelectedScanMode,
            AetherNoize = SelectedNoize,
            AetherPeer = PeerEndpoint.TrimEx(),
        };
        SelectedSource.SetProtocolExtra(extra);

        var result = await ConfigHandler.AddAetherServer(_config, SelectedSource);

        if (result == 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
