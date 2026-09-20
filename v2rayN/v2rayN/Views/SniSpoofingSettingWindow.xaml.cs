using v2rayN.Base;

namespace v2rayN.Views;

public partial class SniSpoofingSettingWindow : WindowBase<SniSpoofingSettingViewModel>
{
    public SniSpoofingSettingWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();
        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
