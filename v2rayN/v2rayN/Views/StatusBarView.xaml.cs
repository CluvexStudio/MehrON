using System.Windows.Media;
using v2rayN.Manager;

namespace v2rayN.Views;

public partial class StatusBarView
{
    private static Config _config;

    public StatusBarView()
    {
        InitializeComponent();
        _config = AppManager.Instance.Config;
        ApplyGlassTheme();

        menuExit.Click += menuExit_Click;
        txtRunningServerDisplay.PreviewMouseDown += txtRunningInfoDisplay_MouseDoubleClick;
        txtRunningInfoDisplay.PreviewMouseDown += txtRunningInfoDisplay_MouseDoubleClick;

        this.WhenActivated(disposables =>
        {
            //system proxy
            this.OneWayBind(ViewModel, vm => vm.BlSystemProxyClear, v => v.menuSystemProxyClear2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, viewModelToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlSystemProxySet, v => v.menuSystemProxySet2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, viewModelToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlSystemProxyNothing, v => v.menuSystemProxyNothing2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, viewModelToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlSystemProxyPac, v => v.menuSystemProxyPac2.Visibility, conversionHint: BooleanToVisibilityHint.UseHidden, viewModelToViewConverterOverride: new BooleanToVisibilityTypeConverter()).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SystemProxyClearCmd, v => v.menuSystemProxyClear).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SystemProxySetCmd, v => v.menuSystemProxySet).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SystemProxyNothingCmd, v => v.menuSystemProxyNothing).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SystemProxyPacCmd, v => v.menuSystemProxyPac).DisposeWith(disposables);

            //routings and servers
            this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings.SelectedItem).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.menuRoutings.Visibility).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.sepRoutings.Visibility).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.Servers, v => v.cmbServers.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedServer, v => v.cmbServers.SelectedItem).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlServers, v => v.cmbServers.Visibility).DisposeWith(disposables);

            //tray menu
            this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate2).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy2).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.CopyProxyCmdToClipboardCmd, v => v.menuCopyProxyCmdToClipboard).DisposeWith(disposables);

            this.OneWayBind(ViewModel, vm => vm.RunningServerToolTipText, v => v.tbNotify.ToolTipText).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.NotifyLeftClickCmd, v => v.tbNotify.LeftClickCommand).DisposeWith(disposables);

            //status bar
            this.OneWayBind(ViewModel, vm => vm.InboundDisplay, v => v.txtInboundDisplay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.InboundLanDisplay, v => v.txtInboundLanDisplay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RunningServerDisplay, v => v.txtRunningServerDisplay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RunningInfoDisplay, v => v.txtRunningInfoDisplay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.SpeedProxyDisplay, v => v.txtSpeedProxyDisplay.Text).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.SpeedDirectDisplay, v => v.txtSpeedDirectDisplay.Text).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ToggleConnectCmd, v => v.btnConnect).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ShowBottomLog, v => v.togShowLog.IsChecked).DisposeWith(disposables);

            this.Bind(ViewModel, vm => vm.SystemProxySelected, v => v.cmbSystemProxy.SelectedIndex).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.RoutingItems, v => v.cmbRoutings2.ItemsSource).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedRouting, v => v.cmbRoutings2.SelectedItem).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlRouting, v => v.cmbRoutings2.Visibility).DisposeWith(disposables);

            ViewModel.SetClipboardDataInteraction.RegisterHandler(interaction =>
            {
                var strData = interaction.Input;
                WindowsUtils.SetClipboardData(strData);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);

            ViewModel.DispatcherRefreshIconInteraction.RegisterHandler(interaction =>
            {
                Application.Current?.Dispatcher.Invoke(async () => await RefreshIcon(), DispatcherPriority.Normal);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
        });

        _ = RefreshIcon();
    }

    private async Task RefreshIcon()
    {
        tbNotify.Icon = await WindowsManager.Instance.GetNotifyIcon(_config);
        Application.Current.MainWindow?.Icon = WindowsManager.Instance.GetAppIcon(_config);
    }

    private async void menuExit_Click(object sender, RoutedEventArgs e)
    {
        tbNotify.Dispose();
        await AppManager.Instance.AppExitAsync(true);
    }

    private void txtRunningInfoDisplay_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel?.TestServerAvailability();
    }

    private void ApplyGlassTheme()
    {
        bool isLight = _config?.UiItem?.CurrentTheme == nameof(ETheme.Light);
        if (isLight)
        {
            var bg = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xF2, 0xFF, 0xFF, 0xFF), 0.0));
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xE6, 0xED, 0xF2, 0xF9), 0.5));
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xDC, 0xE0, 0xE8, 0xF2), 1.0));
            borderGlass.Background = bg;

            var border = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            border.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xE6, 0xFF, 0xFF, 0xFF), 0.0));
            border.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x80, 0x70, 0x80, 0x90), 1.0));
            borderGlass.BorderBrush = border;
        }
        else
        {
            var bg = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xEB, 0x26, 0x2B, 0x3A), 0.0));
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xE0, 0x1E, 0x21, 0x2D), 0.5));
            bg.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0xD9, 0x16, 0x18, 0x22), 1.0));
            borderGlass.Background = bg;

            var border = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(0, 1)
            };
            border.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF), 0.0));
            border.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromArgb(0x38, 0xFF, 0xFF, 0xFF), 1.0));
            borderGlass.BorderBrush = border;
        }
    }
}
