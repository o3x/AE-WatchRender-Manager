// Date: Thu Mar 19 18:52:42 JST 2026
// Version: 1.0.0

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace AEWatchRenderManager.Views
{
    public partial class ScanCycleDialog : Window
    {
        /// <summary>OKボタンで確定された秒数。ShowDialog() == true のときのみ有効。</summary>
        public int ResultSeconds { get; private set; }

        public ScanCycleDialog(int currentSeconds)
        {
            InitializeComponent();
            IntervalTextBox.Text = currentSeconds.ToString();

            // ウィンドウ表示後にフォーカスを当てて全選択
            Loaded += (_, _) =>
            {
                IntervalTextBox.Focus();
                IntervalTextBox.SelectAll();
            };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(IntervalTextBox.Text, out int seconds) && seconds >= 1)
            {
                ResultSeconds = seconds;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show(
                    "1 以上の整数を入力してください。",
                    "入力エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>数字以外のキー入力を弾く。</summary>
        private void IntervalTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$");
        }
    }
}
