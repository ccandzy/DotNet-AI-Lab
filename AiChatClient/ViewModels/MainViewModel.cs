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
        private readonly IConversationService _conversationService;
        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;
        private readonly ObservableCollection<ChatMessage> _emptyMessages = new();
        private Conversation? _currentConversation;

        public MainViewModel(IChatService chatService, IConversationService conversationService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _logger = logger;

            Conversations = _conversationService.Conversations;

            // ensure there is at least one conversation
            if (Conversations.Count == 0)
            {
                var c = _conversationService.CreateConversation();
                CurrentConversation = c;
            }

            // Commands
            SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
            StopCommand = new RelayCommand(Stop, () => IsBusy);
            ClearCommand = new RelayCommand(ClearMessages, () => Messages.Count > 0);
            NewConversationCommand = new RelayCommand(NewConversation);
            DeleteConversationCommand = new RelayCommand(DeleteConversation, () => CurrentConversation is not null);
            RenameConversationCommand = new RelayCommand<string>(RenameConversation);
        }

        public ObservableCollection<Conversation> Conversations { get; }

        public Conversation? CurrentConversation
        {
            get => _currentConversation;
            set
            {
                if (SetProperty(ref _currentConversation, value))
                {
                    // notify that Messages changed
                    OnPropertyChanged(nameof(Messages));
                    ClearCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<ChatMessage> Messages => CurrentConversation?.Messages ?? _emptyMessages;

        public IRelayCommand NewConversationCommand { get; }

        public IRelayCommand DeleteConversationCommand { get; }

        public IRelayCommand<string> RenameConversationCommand { get; }

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
                await foreach (var line in _chatService.SendStreamingAsync(Messages, _currentRequestCts.Token))
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
            CurrentConversation?.Messages.Clear();
            ClearCommand.NotifyCanExecuteChanged();
        }

        private void AddMessage(ChatRole role, string content)
        {
            var msg = new ChatMessage(role, content, DateTime.Now);
            if (CurrentConversation is not null)
            {
                CurrentConversation.Messages.Add(msg);
            }
            else
            {
                // fallback
                _emptyMessages.Add(msg);
            }
            ClearCommand.NotifyCanExecuteChanged();
        }

        private void NewConversation()
        {
            var conv = _conversationService.CreateConversation();
            CurrentConversation = conv;
        }

        private void DeleteConversation()
        {
            if (CurrentConversation is null) return;
            var id = CurrentConversation.Id;
            _conversationService.DeleteConversation(id);
            // pick another conversation if any
            CurrentConversation = Conversations.FirstOrDefault();
        }

        private void RenameConversation(string? newTitle)
        {
            if (CurrentConversation is null || string.IsNullOrWhiteSpace(newTitle)) return;
            _conversationService.RenameConversation(CurrentConversation.Id, newTitle.Trim());
            // update binding
            OnPropertyChanged(nameof(Conversations));
        }
    }
}
