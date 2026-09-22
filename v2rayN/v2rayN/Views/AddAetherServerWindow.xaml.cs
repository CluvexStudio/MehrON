using System.Windows;
using ReactiveUI;
using ServiceLib.ViewModels;
using v2rayN.Common;

namespace v2rayN.Views;

public partial class AddAetherServerWindow
{
    public AddAetherServerWindow()
    {
        InitializeComponent();

        Loaded += Window_Loaded;
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
            this.Bind(ViewModel, vm => vm.SelectedProtocol, v => v.cmbProtocol.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedScanMode, v => v.cmbScanMode.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.SelectedNoize, v => v.cmbNoize.SelectedItem).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PeerEndpoint, v => v.txtPeer.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.PreSocksPort, v => v.txtPort.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.DisplayLog, v => v.togDisplayLog.IsChecked).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.SaveServerCmd, v => v.btnSave).DisposeWith(disposables);
        });

        WindowsUtils.SetDarkBorder(this, AppManager.Instance.Config.UiItem.CurrentTheme);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtRemarks.Focus();
    }
}
