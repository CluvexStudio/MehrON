namespace ServiceLib.ViewModels;

public partial class DoubleTunnelViewModel : MyReactiveObject, ICloseable
{
    public event EventHandler? RequestClose;

    [Reactive]
    public partial ProfileItem? SelectedMiddleServer { get; set; }

    [Reactive]
    public partial ProfileItem? SelectedExitServer { get; set; }

    [Reactive]
    public partial string Remarks { get; set; } = string.Empty;

    [Reactive]
    public partial string CoreType { get; set; }

    public List<string> CoreTypes { get; } = [nameof(ECoreType.Xray), nameof(ECoreType.sing_box)];

    public BulkObservableCollection<ProfileItem> Servers { get; } = [];

    public ReactiveCommand<RxVoid, RxVoid> SaveCmd { get; }

    private bool _isCustomRemarks;

    public DoubleTunnelViewModel()
    {
        _config = AppManager.Instance.Config;
        CoreType = nameof(ECoreType.Xray);

        this.WhenAnyValue(x => x.SelectedMiddleServer, x => x.SelectedExitServer)
            .Subscribe(t =>
            {
                var (mid, exit) = t;
                if (!_isCustomRemarks)
                {
                    var midName = mid?.Remarks ?? "...";
                    var exitName = exit?.Remarks ?? "...";
                    Remarks = $"Double-Tunnel (Beta): {midName} -> {exitName}";
                }
            });

        this.WhenAnyValue(x => x.Remarks)
            .Subscribe(rem =>
            {
                if (!rem.IsNullOrEmpty() && !rem.StartsWith("Double-Tunnel (Beta):"))
                {
                    _isCustomRemarks = true;
                }
            });

        SaveCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await SaveDoubleTunnelAsync();
        });

        _ = Init();
    }

    public async Task Init()
    {
        var allProfiles = await AppManager.Instance.ProfileItems(string.Empty) ?? [];
        var validProfiles = allProfiles
            .Where(p => !p.ConfigType.IsGroupType() && p.ConfigType != EConfigType.Custom)
            .OrderBy(p => p.Remarks)
            .ToList();

        Servers.AddRange(validProfiles);
    }

    private async Task SaveDoubleTunnelAsync()
    {
        if (SelectedMiddleServer == null || SelectedExitServer == null)
        {
            NoticeManager.Instance.Enqueue(ResUI.DoubleTunnelSelectBothWarning);
            return;
        }

        if (SelectedMiddleServer.IndexId == SelectedExitServer.IndexId)
        {
            NoticeManager.Instance.Enqueue(ResUI.DoubleTunnelSameServerWarning);
            return;
        }

        var remarks = Remarks.Trim();
        if (remarks.IsNullOrEmpty())
        {
            NoticeManager.Instance.Enqueue(ResUI.PleaseFillRemarks);
            return;
        }

        var core = Enum.TryParse<ECoreType>(CoreType, out var parsedCore) ? parsedCore : ECoreType.Xray;

        var profileItem = new ProfileItem
        {
            ConfigType = EConfigType.ProxyChain,
            Remarks = remarks,
            CoreType = core,
            IsSub = false,
        };

        var protocolExtra = profileItem.GetProtocolExtra() with
        {
            ChildItems = $"{SelectedMiddleServer.IndexId},{SelectedExitServer.IndexId}",
            GroupType = nameof(EConfigType.ProxyChain),
        };
        profileItem.SetProtocolExtra(protocolExtra);

        if (await ConfigHandler.AddServerCommon(_config, profileItem) == 0)
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
