using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

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
            this.BindCommand(ViewModel, vm => vm.ApplyToProfileCmd, v => v.btnApply).DisposeWith(disposables);

            ViewModel.SetClipboardDataInteraction.RegisterHandler(async interaction =>
            {
                await AvaUtils.SetClipboardData(this, interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);
        });
    }
}
