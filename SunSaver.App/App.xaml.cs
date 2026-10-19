using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace SunSaver.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Format angka Indonesia di seluruh aplikasi: "42,8 kWh", "Rp 62.300".
        // Binding WPF memakai FrameworkElement.Language (default en-US), bukan CurrentCulture,
        // jadi keduanya diset.
        var id = new CultureInfo("id-ID");
        CultureInfo.DefaultThreadCurrentCulture = id;
        CultureInfo.DefaultThreadCurrentUICulture = id;
        Thread.CurrentThread.CurrentCulture = id;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(id.IetfLanguageTag)));

        base.OnStartup(e);
    }
}
