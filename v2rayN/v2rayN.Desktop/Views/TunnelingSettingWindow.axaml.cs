using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;

namespace v2rayN.Desktop.Views;

public partial class TunnelingSettingWindow : WindowBase<TunnelingSettingViewModel>
{
    public TunnelingSettingWindow()
    {
        InitializeComponent();
        btnCancel.Click += (_, _) => Close();

        btnBrowseZeptun.Click += async (_, _) =>
        {
            var fileName = await UI.OpenFileDialog(null);
            if (!string.IsNullOrEmpty(fileName) && ViewModel != null)
            {
                ViewModel.ZeptunPath = fileName;
            }
        };

        this.WhenActivated(disposables =>
        {
            this.BindCommand(ViewModel, vm => vm.SaveCmd, v => v.btnSave).DisposeWith(disposables);
        });
    }
}
