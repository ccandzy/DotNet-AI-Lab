using System.Text;
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
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            if (viewModel is not null)
            {
                viewModel.Messages.CollectionChanged += Messages_CollectionChanged;
            }

            InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Scroll to bottom when new messages arrive
            Dispatcher.InvokeAsync(() =>
            {
                if (MessagesListBox.Items.Count > 0)
                {
                    var last = MessagesListBox.Items[MessagesListBox.Items.Count - 1];
                    MessagesListBox.ScrollIntoView(last);
                }
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
    }
}