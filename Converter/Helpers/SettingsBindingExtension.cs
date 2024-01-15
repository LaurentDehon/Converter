using System.Windows.Data;

namespace Converter.Helpers;

public class SettingsExtension : Binding
{
    public SettingsExtension()
    {
        Initialize();
    }

    public SettingsExtension(string path) : base(path)
    {
        Initialize();
    }

    private void Initialize()
    {
        Source = Settings.Default;
        Mode = BindingMode.TwoWay;
    }
}
