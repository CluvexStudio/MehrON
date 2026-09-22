using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class AddAetherServerWindow : WindowBase<AddAetherServerViewModel>
{
    public AddAetherServerWindow()
    {
        InitializeComponent();

        btnCancel.Click += (s, e) => Close();

        this.WhenActivated(disposables =>
        {
            if (ViewModel != null)
            {
                cmbProtocol.ItemsSource = ViewModel.Protocols;
                cmbScanMode.ItemsSource = ViewModel.ScanModes;
                cmbNoize.ItemsSource = ViewModel.NoizeProfiles;
            }

            this.Bind(ViewModel, vm => vm.SelectedSource.Remarks, v => v.txtRemarks.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedProtocol, v => v.cmbProtocol.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedScanMode, v => v.cmbScanMode.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedNoize, v => v.cmbNoize.SelectedValue).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PeerEndpoint, v => v.txtPeer.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PreSocksPort, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DisplayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
