using v2rayN.Base;
using v2rayN.Common;

namespace v2rayN.Views;

public partial class CloudflareScannerWindow : WindowBase<CloudflareScannerViewModel>
{
    public CloudflareScannerWindow()
    {
        InitializeComponent();
        btnClose.Click += (_, _) => Close();

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.StartScanCmd, v => v.btnStart).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StopScanCmd, v => v.btnStop).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CopySelectedCmd, v => v.btnCopySelected).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CopyAllWorkingCmd, v => v.btnCopyAllWorking).DisposeWith(disposables);

            ViewModel.SetClipboardDataInteraction.RegisterHandler(interaction =>
            {
                WindowsUtils.SetClipboardData(interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
        });
    }
}
