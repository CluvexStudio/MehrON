using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class DoubleTunnelWindow : WindowBase<DoubleTunnelViewModel>
{
    public DoubleTunnelWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();
        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
