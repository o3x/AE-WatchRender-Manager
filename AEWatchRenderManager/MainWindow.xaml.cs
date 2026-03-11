using AEWatchRenderManager.ViewModels;
using System.Windows;

namespace AEWatchRenderManager
{
    // Date: Wed Mar 11 12:49:00 JST 2026
    // Version: 1.1.0
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (DataContext is MainViewModel vm)
                {
                    if (vm.DropFilesCommand.CanExecute(files))
                    {
                        vm.DropFilesCommand.Execute(files);
                    }
                }
            }
        }
    }
}