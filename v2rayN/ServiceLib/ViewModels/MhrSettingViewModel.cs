namespace ServiceLib.ViewModels;

public partial class MhrSettingViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;
    private readonly MhrItem _settings;

    [Reactive] public partial bool Enabled { get; set; }
    [Reactive] public partial string Variant { get; set; } = "mhr";
    [Reactive] public partial string ScriptId { get; set; } = string.Empty;
    [Reactive] public partial string AuthKey { get; set; } = string.Empty;
    [Reactive] public partial int HttpPort { get; set; }
    [Reactive] public partial int Socks5Port { get; set; }
    public List<string> Variants { get; } = ["mhr", "mhr-cfw"];
    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> StopCmd { get; }

    public MhrSettingViewModel()
    {
        _config = AppManager.Instance.Config;
        _settings = JsonUtils.DeepCopy(_config.MhrItem);
        Enabled = _settings.Enabled;
        Variant = _settings.Variant;
        ScriptId = _settings.ScriptId;
        AuthKey = _settings.AuthKey;
        HttpPort = _settings.HttpPort;
        Socks5Port = _settings.Socks5Port;
        SaveCmd = ReactiveCommand.CreateFromTask(SaveAsync);
        StopCmd = ReactiveCommand.CreateFromTask(StopAsync);
    }

    private async Task SaveAsync()
    {
        if (HttpPort is < 1 or > 65535 || Socks5Port is < 1 or > 65535)
        {
            NoticeManager.Instance.Enqueue("Enter valid HTTP and SOCKS5 ports.");
            return;
        }
        if (Enabled && (string.IsNullOrWhiteSpace(ScriptId) || string.IsNullOrWhiteSpace(AuthKey)))
        {
            NoticeManager.Instance.Enqueue("Enter the Deployment ID and AUTH_KEY before enabling MHR.");
            return;
        }

        _settings.Enabled = Enabled;
        _settings.Variant = Variant;
        _settings.ScriptId = ScriptId.Trim();
        _settings.AuthKey = AuthKey;
        _settings.HttpPort = HttpPort;
        _settings.Socks5Port = Socks5Port;
        _config.MhrItem = _settings;
        if (await ConfigHandler.SaveConfig(_config) != 0)
        {
            NoticeManager.Instance.Enqueue(ResUI.OperationFailed);
            return;
        }
        if (!await MhrManager.Instance.StartAsync(null))
        {
            NoticeManager.Instance.Enqueue("MHR could not start. Check that Python 3.10+ and the selected runtime are available.");
            return;
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }

    private async Task StopAsync()
    {
        await MhrManager.Instance.StopAndDisableAsync();
        Enabled = false;
        _settings.Enabled = false;
        NoticeManager.Instance.Enqueue("MHR stopped and the system proxy was cleared.");
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
