using System.Windows;
using System.Windows.Input;
using AiChatClient.ViewModels;

namespace AiChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
        }

        private void InputTextBox_PreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                e.Handled = true;
                if (DataContext is MainViewModel vm && vm.SendCommand.CanExecute(null))
                {
                    vm.SendCommand.Execute(null);
                }
            }
        }

        /// <summary>
        /// 最小化当前窗口。该操作只负责窗口外观控制，不参与业务逻辑。
        /// </summary>
        private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 最大化当前窗口。窗口已最大化时不重复执行状态切换。
        /// </summary>
        private void MaximizeWindowButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
            {
                WindowState = WindowState.Maximized;
            }
        }

        /// <summary>
        /// 将当前窗口恢复到普通状态。
        /// </summary>
        private void RestoreWindowButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Normal;
        }

        /// <summary>
        /// 关闭当前窗口。
        /// </summary>
        private void CloseWindowButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
