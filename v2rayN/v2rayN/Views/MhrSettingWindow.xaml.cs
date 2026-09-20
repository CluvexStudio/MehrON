using v2rayN.Base;

namespace v2rayN.Views;

public partial class MhrSettingWindow : WindowBase<MhrSettingViewModel>
{
    public MhrSettingWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();
        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.StopCmd, v => v.btnStop).DisposeWith(disposables);
        });
    }
}
