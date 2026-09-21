using v2rayN.Base;

namespace v2rayN.Views;

public partial class TunnelingSettingWindow : WindowBase<TunnelingSettingViewModel>
{
    public TunnelingSettingWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();

        this.WhenActivated(disposables =>
        {
            this.Bind(ViewModel, vm => vm.IsZeptunCore, v => v.radZeptun.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsSingboxCore, v => v.radSingbox.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunInterfaceName, v => v.txtZeptunInterfaceName.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunMtu, v => v.txtZeptunMtu.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunStack, v => v.cmbZeptunStack.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ExtraArguments, v => v.txtExtraArguments.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunAutoRoute, v => v.chkZeptunAutoRoute.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunStrictRoute, v => v.chkZeptunStrictRoute.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunDnsHijack, v => v.chkZeptunDnsHijack.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.ZeptunFakeIp, v => v.chkZeptunFakeIp.IsChecked).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.IsZeptunSelected, v => v.grpZeptun.IsEnabled).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenZeptunUrlCmd, v => v.btnOpenZeptunUrl).DisposeWith(disposables);
        });
    }
}
