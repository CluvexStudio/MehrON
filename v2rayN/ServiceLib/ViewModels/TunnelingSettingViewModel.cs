using ServiceLib.Manager;

namespace ServiceLib.ViewModels;

public partial class TunnelingSettingViewModel : MyReactiveObject, ICloseable
{
    public const string CoreSingboxLabel = TunnelingItem.CoreSingbox;
    public const string CoreZeptunLabel = TunnelingItem.CoreZeptun;

    public event EventHandler? RequestClose;

    private readonly TunnelingItem _settings;

    [Reactive] public partial string SelectedCore { get; set; } = CoreSingboxLabel;
    public List<string> Cores { get; } = [CoreSingboxLabel, CoreZeptunLabel];

    [Reactive] public partial bool IsZeptunSelected { get; set; }

    [Reactive] public partial string ZeptunInterfaceName { get; set; } = "zeptun0";
    [Reactive] public partial int ZeptunMtu { get; set; } = 1500;
    [Reactive] public partial string ZeptunStack { get; set; } = "userspace";
    public List<string> Stacks { get; } = ["userspace"];

    [Reactive] public partial bool ZeptunAutoRoute { get; set; } = true;
    [Reactive] public partial bool ZeptunStrictRoute { get; set; } = true;
    [Reactive] public partial string ExtraArguments { get; set; } = string.Empty;

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenZeptunUrlCmd { get; }

    public TunnelingSettingViewModel()
    {
        _config = AppManager.Instance.Config;
        _config.TunnelingItem ??= new();
        _settings = JsonUtils.DeepCopy(_config.TunnelingItem);

        SelectedCore = _settings.SelectedCore == CoreZeptunLabel ? CoreZeptunLabel : CoreSingboxLabel;
        IsZeptunSelected = SelectedCore == CoreZeptunLabel;

        ZeptunInterfaceName = string.IsNullOrWhiteSpace(_settings.ZeptunInterfaceName) ? "zeptun0" : _settings.ZeptunInterfaceName;
        ZeptunMtu = (_settings.ZeptunMtu > 0 && _settings.ZeptunMtu != 8500) ? _settings.ZeptunMtu : 1500;
        ZeptunStack = "userspace";
        ZeptunAutoRoute = _settings.ZeptunAutoRoute;
        ZeptunStrictRoute = _settings.ZeptunStrictRoute;
        ExtraArguments = _settings.ExtraArguments;

        this.WhenAnyValue(x => x.SelectedCore)
            .Subscribe(core =>
            {
                IsZeptunSelected = core == CoreZeptunLabel;
            });

        SaveCmd = ReactiveCommand.CreateFromTask(SaveAsync);
        OpenZeptunUrlCmd = ReactiveCommand.Create(() =>
        {
            ProcUtils.ProcessStart(ZeptunManager.ProjectUrl);
        });
    }

    private async Task SaveAsync()
    {
        if (ZeptunMtu is < 576 or > 65535)
        {
            NoticeManager.Instance.Enqueue("Enter a valid MTU value (between 576 and 65535).");
            return;
        }

        _settings.SelectedCore = SelectedCore == CoreZeptunLabel ? CoreZeptunLabel : CoreSingboxLabel;
        _settings.ZeptunInterfaceName = string.IsNullOrWhiteSpace(ZeptunInterfaceName) ? "zeptun0" : ZeptunInterfaceName.Trim();
        _settings.ZeptunMtu = ZeptunMtu;
        _settings.ZeptunStack = string.IsNullOrWhiteSpace(ZeptunStack) ? "userspace" : ZeptunStack.Trim();
        _settings.ZeptunAutoRoute = ZeptunAutoRoute;
        _settings.ZeptunStrictRoute = ZeptunStrictRoute;
        _settings.ExtraArguments = ExtraArguments?.Trim() ?? string.Empty;

        _config.TunnelingItem = _settings;

        if (await ConfigHandler.SaveConfig(_config) == 0)
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
