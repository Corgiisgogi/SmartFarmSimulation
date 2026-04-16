using System.Windows;
using SmartFarmUI.Services;
using SmartFarmUI.ViewModels;

namespace SmartFarmUI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var settingsRepo = new SettingsRepository();
            var logService = new LogService();
            var plcService = new PlcService(logService);
            var flaskApi = new FlaskApiService();

            DataContext = new MainWindowViewModel(plcService, flaskApi, logService, settingsRepo);
        }
    }
}
