using System.Windows;
using System.Windows.Input;
using System.Collections.Specialized;
using AiChatClient.ViewModels;

namespace AiChatClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyCollectionChangedEventHandler? _messagesHandler;
        private System.ComponentModel.PropertyChangedEventHandler? _propertyHandler;
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            if (viewModel is not null)
            {
                // watch for CurrentConversation changes
                _propertyHandler = new System.ComponentModel.PropertyChangedEventHandler((s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.CurrentConversation))
                    {
                        AttachToCurrentConversation(viewModel);
                    }
                });

                viewModel.PropertyChanged += _propertyHandler;

                // initial attach
                AttachToCurrentConversation(viewModel);
            }

            InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
        }

        private void AttachToCurrentConversation(MainViewModel vm)
        {
            // detach previous
            if (_messagesHandler is not null)
            {
                try
                {
                    // find previous conversation
                    // detach from all to be safe
                }
                catch { }
            }

            // attach to new
            var conv = vm.CurrentConversation;
            if (conv is not null)
            {
                _messagesHandler = new NotifyCollectionChangedEventHandler(Messages_CollectionChanged);
                conv.Messages.CollectionChanged += _messagesHandler;
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Scroll to bottom when new messages arrive
            Dispatcher.InvokeAsync(() =>
            {
                // Use ScrollViewer for scrolling instead of ListBox
                MessagesScrollViewer.ScrollToBottom();
            }, System.Windows.Threading.DispatcherPriority.Background);
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
