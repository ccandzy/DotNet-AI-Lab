using System.Collections.ObjectModel;
using AiChatClient.Models;
using AiChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AiChatClient.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly ILogger<MainViewModel> _logger;
        private string _currentInput = string.Empty;
        private bool _isBusy;

        public MainViewModel(IChatService chatService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _logger = logger;
            Messages = new ObservableCollection<ChatMessage>();
            SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
            ClearCommand = new RelayCommand(ClearMessages, () => Messages.Count > 0);
        }

        public ObservableCollection<ChatMessage> Messages { get; }

        public string CurrentInput
        {
            get => _currentInput;
            set
            {
                if (SetProperty(ref _currentInput, value))
                {
                    SendCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    SendCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand SendCommand { get; }

        public IRelayCommand ClearCommand { get; }

        private bool CanSend()
        {
            return !IsBusy && !string.IsNullOrWhiteSpace(CurrentInput);
        }

        private async Task SendAsync()
        {
            var input = CurrentInput.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            AddMessage(ChatRole.User, input);
            CurrentInput = string.Empty;
            IsBusy = true;

            try
            {
                var reply = await _chatService.SendAsync(input);
                AddMessage(ChatRole.Assistant, reply);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to AI service. UserInput: {UserInput}", input);
                AddMessage(ChatRole.Assistant, "AI 服务连接失败，请稍后重试。");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearMessages()
        {
            Messages.Clear();
            ClearCommand.NotifyCanExecuteChanged();
        }

        private void AddMessage(ChatRole role, string content)
        {
            Messages.Add(new ChatMessage(role, content, DateTime.Now));
            ClearCommand.NotifyCanExecuteChanged();
        }
    }
}
