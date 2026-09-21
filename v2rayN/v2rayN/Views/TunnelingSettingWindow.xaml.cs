using v2rayN.Base;

namespace v2rayN.Views;

public partial class TunnelingSettingWindow : WindowBase<TunnelingSettingViewModel>
{
    public TunnelingSettingWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();

        btnBrowseZeptun.Click += (_, _) =>
        {
            if (UI.OpenFileDialog(out var fileName, "Zeptun Executable|*.exe|All Files|*.*") == true)
            {
                if (ViewModel != null)
                {
                    ViewModel.ZeptunPath = fileName;
                }
            }
        };

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
