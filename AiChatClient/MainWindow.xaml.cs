using System.Text;
using Microsoft.VisualBasic;
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

        private void RenameConversation_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm || vm.CurrentConversation is null) return;

            var currentTitle = vm.CurrentConversation.Title ?? string.Empty;
            var input = Interaction.InputBox("重命名会话:", "重命名", currentTitle);
            if (!string.IsNullOrWhiteSpace(input))
            {
                // call VM rename command
                if (vm.RenameConversationCommand.CanExecute(input))
                {
                    vm.RenameConversationCommand.Execute(input);
                }
            }
        }
    }
}