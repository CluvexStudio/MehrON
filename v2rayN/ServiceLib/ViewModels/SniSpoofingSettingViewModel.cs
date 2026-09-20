namespace ServiceLib.ViewModels;

public partial class SniSpoofingSettingViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    private readonly SniSpoofingItem _settings;

    [Reactive] public partial bool Enabled { get; set; }
    [Reactive] public partial string ListenHost { get; set; } = "127.0.0.1";
    [Reactive] public partial int ListenPort { get; set; }
    [Reactive] public partial string ConnectIp { get; set; } = string.Empty;
    [Reactive] public partial int ConnectPort { get; set; }
    [Reactive] public partial string FakeSni { get; set; } = string.Empty;

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    public SniSpoofingSettingViewModel()
    {
        _config = AppManager.Instance.Config;
        _settings = JsonUtils.DeepCopy(_config.SniSpoofingItem);
        Enabled = _settings.Enabled;
        ListenHost = _settings.ListenHost;
        ListenPort = _settings.ListenPort;
        ConnectIp = _settings.ConnectIp;
        ConnectPort = _settings.ConnectPort;
        FakeSni = _settings.FakeSni;

        SaveCmd = ReactiveCommand.CreateFromTask(SaveAsync);
    }

    private async Task SaveAsync()
    {
        if (ListenPort is < 1 or > 65535 || ConnectPort is < 1 or > 65535 || string.IsNullOrWhiteSpace(ListenHost) || string.IsNullOrWhiteSpace(FakeSni))
        {
            NoticeManager.Instance.Enqueue("Enter valid listener, target port, and fake SNI values.");
            return;
        }
        if (!string.IsNullOrWhiteSpace(ConnectIp) && !IPAddress.TryParse(ConnectIp.Trim(), out _))
        {
            NoticeManager.Instance.Enqueue("Connect IP must be a valid IP address, or leave it blank for automatic resolution.");
            return;
        }

        _settings.Enabled = Enabled;
        _settings.ListenHost = ListenHost.Trim();
        _settings.ListenPort = ListenPort;
        _settings.ConnectIp = ConnectIp.Trim();
        _settings.ConnectPort = ConnectPort;
        _settings.FakeSni = FakeSni.Trim();
        _config.SniSpoofingItem = _settings;

        if (await ConfigHandler.SaveConfig(_config) == 0)
        {
            if (Enabled && Utils.IsWindows() && !Utils.IsAdministrator())
            {
                NoticeManager.Instance.Enqueue("SNI Spoofing requires administrator rights. Approve the Windows prompt to restart MehrN as administrator.");
                if (ProcUtils.RebootAsAdmin())
                {
                    await AppManager.Instance.AppExitAsync(true);
                }
                return;
            }

            if (Utils.IsWindows())
            {
                if (Enabled && Utils.IsAdministrator())
                {
                    _ = SniSpoofingManager.Instance.StartAsync(null, null);
                }
                else if (!Enabled)
                {
                    _ = SniSpoofingManager.Instance.StopAsync();
                }
            }

            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
        }
    }
}
