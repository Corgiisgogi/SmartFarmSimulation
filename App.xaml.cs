using System.Windows;
using SmartFarmUI.Infrastructure;

namespace SmartFarmUI
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherHelper.Initialize(Dispatcher);
        }
    }
}
