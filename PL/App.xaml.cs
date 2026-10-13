using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Markup;
//using Admin;

namespace PL
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App()
        {
            // Show every date/time in the UI the same way (dd/MM/yyyy, 24-hour clock),
            // regardless of the Windows regional settings of the machine running the app.
            FrameworkElement.LanguageProperty.OverrideMetadata(
                typeof(FrameworkElement),
                new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-GB")));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Open Login window as the first screen
           // new LoginWindow().Show();
        }
    }
}
