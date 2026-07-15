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
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;

        public MainViewModel(IChatService chatService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _logger = logger;
            Messages = new ObservableCollection<ChatMessage>();
            SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
            StopCommand = new RelayCommand(Stop, () => IsBusy);
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
                    StopCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand SendCommand { get; }

        public IRelayCommand StopCommand { get; }

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
            ChatMessage chatMessage = new ChatMessage(ChatRole.Assistant, string.Empty, DateTime.Now);
            _currentRequestCts = new CancellationTokenSource();
            try
            {
                
                Messages.Add(chatMessage);
                await foreach (var line in _chatService.SendStreamingAsync(input, _currentRequestCts.Token))
                {
                    chatMessage.Content += line;
                }
            }
            catch (OperationCanceledException)
            {
                chatMessage.Content = "已停止生成。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to AI service. UserInput: {UserInput}", input);
                chatMessage.Content = "AI 服务连接失败，请稍后重试。";

            }
            finally
            {
                _currentRequestCts?.Dispose();
                _currentRequestCts = null;
                IsBusy = false;
            }
        }

        private void Stop()
        {
            _currentRequestCts?.Cancel();
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
