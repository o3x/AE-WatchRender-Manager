using AEWatchRenderManager.ViewModels;
using System.Windows;

namespace AEWatchRenderManager.Views
{
    public partial class SettingsDialog : Window
    {
        public SettingsDialog(SettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;
        private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}
