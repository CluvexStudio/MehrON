namespace ServiceLib.ViewModels;

public partial class SubSettingViewModel : MyReactiveObject
{
    public Interaction<string, bool> ShowYesNoInteraction { get; } = new();
    public Interaction<string, RxVoid> ShareSubInteraction { get; } = new();

    public BulkObservableCollection<SubItem> SubItems { get; } = [];

    [Reactive]
    public partial SubItem SelectedSource { get; set; }

    public IList<SubItem> SelectedSources { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> SubAddCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubDeleteCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubEditCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubShareCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> SubUpdateViaProxyCmd { get; }
    public bool IsModified { get; set; }

    public SubSettingViewModel()
    {
        _config = AppManager.Instance.Config;

        var canEditRemove = this.WhenAnyValue(
           x => x.SelectedSource,
           selectedSource => selectedSource != null && !selectedSource.Id.IsNullOrEmpty());

        SubAddCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(true);
        });
        SubDeleteCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await DeleteSubAsync();
        }, canEditRemove);
        SubEditCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await EditSubAsync(false);
        }, canEditRemove);
        SubShareCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await ShareSubInteraction.HandleSafe(SelectedSource?.Url);
        }, canEditRemove);
        SubUpdateCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSelectedSubscriptionAsync(false);
        }, canEditRemove);
        SubUpdateViaProxyCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await UpdateSelectedSubscriptionAsync(true);
        }, canEditRemove);

        _ = Init();
    }

    private async Task Init()
    {
        SelectedSource = new();

        await RefreshSubItems();
    }

    public async Task RefreshSubItems()
    {
        SubItems.ReplaceRange(await AppManager.Instance.SubItems());
    }

    public async Task EditSubAsync(bool blNew)
    {
        SubItem item;
        if (blNew)
        {
            item = new();
        }
        else
        {
            item = await AppManager.Instance.GetSubItem(SelectedSource?.Id);
            if (item is null)
            {
                return;
            }
        }
        var subEditViewModel = new SubEditViewModel(item);
        if (await AppManager.Instance.WindowDialog.ShowDialogAsync(subEditViewModel) == true)
        {
            await RefreshSubItems();
            IsModified = true;
        }
    }

    private async Task DeleteSubAsync()
    {
        if (await ShowYesNoInteraction.HandleSafe(ResUI.RemoveServer) == false)
        {
            return;
        }

        foreach (var it in SelectedSources ?? [SelectedSource])
        {
            await ConfigHandler.DeleteSubItem(_config, it.Id);
        }
        await RefreshSubItems();
        NoticeManager.Instance.Enqueue(ResUI.OperationSuccess);
        IsModified = true;
    }

    /// <summary>
    /// Updates only the subscription selected in the settings list.  Passing its ID to the
    /// subscription handler is important: an empty ID would update every subscription.
    /// </summary>
    private async Task UpdateSelectedSubscriptionAsync(bool viaProxy)
    {
        var subId = SelectedSource?.Id;
        if (subId.IsNullOrEmpty())
        {
            return;
        }

        await Task.Run(async () => await SubscriptionHandler.UpdateProcess(
            _config,
            subId,
            viaProxy,
            async (success, message) =>
            {
                Logging.SaveLog(message);
                if (success)
                {
                    NoticeManager.Instance.Enqueue(message);
                }
                await Task.CompletedTask;
            }));

        await RefreshSubItems();
        IsModified = true;
    }
}
