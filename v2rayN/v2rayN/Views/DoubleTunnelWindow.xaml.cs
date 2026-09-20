using v2rayN.Base;

namespace v2rayN.Views;

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
