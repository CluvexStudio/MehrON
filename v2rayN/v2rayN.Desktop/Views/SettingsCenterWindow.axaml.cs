using v2rayN.Desktop.Base;

namespace v2rayN.Desktop.Views;

public partial class SettingsCenterWindow : WindowBase<SettingsCenterViewModel>
{
    public SettingsCenterWindow()
    {
        InitializeComponent();

        btnClose.Click += (s, e) => Close();
        lstSections.DoubleTapped += (s, e) =>
        {
            _ = ViewModel?.OpenSelectedSectionAsync();
        };
    }
}
