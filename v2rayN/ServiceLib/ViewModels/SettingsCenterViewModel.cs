namespace ServiceLib.ViewModels;

public sealed class SettingSectionItem
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CategoryOrder { get; set; }
    public string Keywords { get; set; } = string.Empty;
    public ESettingLevel Level { get; set; } = ESettingLevel.Basic;
    public Func<Task<bool>>? OpenAsync { get; set; }
}

public partial class SettingsCenterViewModel : MyReactiveObject, ICloseable
{
    public const string KeyOption = "Option";
    public const string KeySub = "Sub";
    public const string KeyHotkey = "Hotkey";
    public const string KeyRouting = "Routing";
    public const string KeyDNS = "DNS";
    public const string KeySni = "Sni";
    public const string KeyMhr = "Mhr";
    public const string KeyTemplate = "Template";

    public event EventHandler? RequestClose;

    public ObservableCollection<SettingSectionItem> FilteredSections { get; } = [];

    public HashSet<string> ChangedSectionKeys { get; } = [];

    public ReactiveCommand<RxVoid, RxVoid> OpenSectionCmd { get; }

    [Reactive] public partial string SelectedTitle { get; set; }
    [Reactive] public partial string SelectedCategory { get; set; }
    [Reactive] public partial string SelectedKeywords { get; set; }

    private readonly List<SettingSectionItem> _allSections = [];

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            RefreshFilteredSections();
        }
    }

    private SettingSectionItem? _selectedSection;
    public SettingSectionItem? SelectedSection
    {
        get => _selectedSection;
        set
        {
            this.RaiseAndSetIfChanged(ref _selectedSection, value);
            SelectedTitle = value?.Title ?? string.Empty;
            SelectedCategory = value?.Category ?? string.Empty;
            SelectedKeywords = value?.Keywords ?? string.Empty;
        }
    }

    public SettingsCenterViewModel()
    {
        BuildSections();
        RefreshFilteredSections();
        SelectedSection = FilteredSections.FirstOrDefault();

        OpenSectionCmd = ReactiveCommand.CreateFromTask(async () =>
        {
            await OpenSelectedSectionAsync();
        });
    }

    public async Task OpenSelectedSectionAsync()
    {
        var section = SelectedSection;
        if (section?.OpenAsync is null)
        {
            return;
        }

        if (await section.OpenAsync() == true)
        {
            ChangedSectionKeys.Add(section.Key);
        }
    }

    private void BuildSections()
    {
        var catGeneral = ResUI.SettingsCategoryGeneral;
        var catNetwork = ResUI.SettingsCategoryNetwork;
        var catAdvanced = ResUI.SettingsCategoryAdvancedTools;

        _allSections.Add(new SettingSectionItem
        {
            Key = KeyOption,
            Title = ResUI.menuOptionSetting,
            Category = catGeneral,
            CategoryOrder = 0,
            Level = ESettingLevel.Basic,
            Keywords = "core, inbound, port, socks, lan, mux, fragment, tun, startup, appearance",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new OptionSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeySub,
            Title = ResUI.menuSubSetting,
            Category = catGeneral,
            CategoryOrder = 0,
            Level = ESettingLevel.Basic,
            Keywords = "subscription, sub, update, link, servers",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new SubSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeyHotkey,
            Title = ResUI.menuGlobalHotkeySetting,
            Category = catGeneral,
            CategoryOrder = 0,
            Level = ESettingLevel.Basic,
            Keywords = "hotkey, shortcut, keyboard",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new GlobalHotkeySettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeyRouting,
            Title = ResUI.menuRoutingSetting,
            Category = catNetwork,
            CategoryOrder = 1,
            Level = ESettingLevel.Basic,
            Keywords = "routing, rules, bypass, proxy, direct, geosite",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new RoutingSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeyDNS,
            Title = ResUI.menuDNSSetting,
            Category = catNetwork,
            CategoryOrder = 1,
            Level = ESettingLevel.Advanced,
            Keywords = "dns, hijack, fakeip, hosts, outbound",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new DNSSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeySni,
            Title = ResUI.menuSniSpoofingSetting,
            Category = catAdvanced,
            CategoryOrder = 2,
            Level = ESettingLevel.Advanced,
            Keywords = "sni, spoofing, dpi, fragment, tls",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new SniSpoofingSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeyMhr,
            Title = ResUI.menuMhrSetting,
            Category = catAdvanced,
            CategoryOrder = 2,
            Level = ESettingLevel.Advanced,
            Keywords = "mhr, relay, http, vpn",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new MhrSettingViewModel()),
        });
        _allSections.Add(new SettingSectionItem
        {
            Key = KeyTemplate,
            Title = ResUI.menuFullConfigTemplate,
            Category = catAdvanced,
            CategoryOrder = 2,
            Level = ESettingLevel.Advanced,
            Keywords = "config, template, outbound, custom, json",
            OpenAsync = async () => await AppManager.Instance.WindowDialog.ShowDialogAsync(new FullConfigTemplateViewModel()),
        });
    }

    private void RefreshFilteredSections()
    {
        var keyword = _searchText?.Trim() ?? string.Empty;
        var selectedKey = SelectedSection?.Key;

        FilteredSections.Clear();
        foreach (var it in _allSections
            .Where(it => keyword.Length == 0
                || it.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || it.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || it.Keywords.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderBy(it => it.CategoryOrder)
            .ThenBy(it => it.Title))
        {
            FilteredSections.Add(it);
        }

        var reselect = FilteredSections.FirstOrDefault(it => it.Key == selectedKey)
            ?? FilteredSections.FirstOrDefault();
        if (!Equals(SelectedSection, reselect))
        {
            SelectedSection = reselect;
        }
    }
}
