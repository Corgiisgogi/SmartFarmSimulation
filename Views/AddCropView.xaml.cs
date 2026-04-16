using System.Windows;

namespace SmartFarmUI.Views
{
    public partial class AddCropView : Window
    {
        public AddCropView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is SmartFarmUI.ViewModels.AddCropViewModel vm)
                    vm.CloseAction = result => { DialogResult = result; Close(); };
            };
        }
    }
}
