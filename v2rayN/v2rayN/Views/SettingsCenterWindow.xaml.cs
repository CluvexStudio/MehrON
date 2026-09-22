namespace v2rayN.Views;

public partial class SettingsCenterWindow
{
    public SettingsCenterWindow()
    {
        InitializeComponent();

        lstSections.MouseDoubleClick += (s, e) =>
        {
            _ = ViewModel?.OpenSelectedSectionAsync();
        };
    }
}
