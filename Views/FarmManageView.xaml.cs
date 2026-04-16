using System.Windows;

namespace SmartFarmUI.Views
{
    public partial class FarmManageView : Window
    {
        public FarmManageView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is SmartFarmUI.ViewModels.FarmManageViewModel vm)
                    vm.CloseAction = result => { DialogResult = result; Close(); };
            };
        }
    }
}
