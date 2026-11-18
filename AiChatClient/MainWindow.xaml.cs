using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using AiChatClient.ViewModels;

namespace AiChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double LeftPanelWidth = 290;
        private const double RightPanelWidth = 300;
        private bool _isLeftPanelOpen = true;
        private bool _isRightPanelOpen = true;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
        }

        private void LeftPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isLeftPanelOpen = !_isLeftPanelOpen;
            AnimatePanel(LeftPanel, _isLeftPanelOpen, LeftPanelWidth);
            UpdatePanelToggleGlyphs();
        }

        private void RightPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            _isRightPanelOpen = !_isRightPanelOpen;
            AnimatePanel(RightPanel, _isRightPanelOpen, RightPanelWidth);
            UpdatePanelToggleGlyphs();
        }

        private static void AnimatePanel(Border panel, bool open, double targetWidth)
        {
            if (open)
            {
                panel.Visibility = Visibility.Visible;
            }

            var animation = new DoubleAnimation
            {
                To = open ? targetWidth : 0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (!open)
            {
                animation.Completed += (_, _) => panel.Visibility = Visibility.Collapsed;
            }

            panel.BeginAnimation(WidthProperty, animation);
        }

        /// <summary>
        /// 箭头始终指向面板将要展开的方向，关闭后指向面板原来的位置。
        /// </summary>
        private void UpdatePanelToggleGlyphs()
        {
            LeftPanelToggleButton.Content = _isLeftPanelOpen ? "«" : "»";
            RightPanelToggleButton.Content = _isRightPanelOpen ? "»" : "«";
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
