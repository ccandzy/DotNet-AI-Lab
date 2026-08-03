using System.Collections.ObjectModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using AiChatClient.Models;
using AiChatClient.Services;
using AiChatClient.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Services;
using Services.Impl;

namespace AiChatClient.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly IConversationService _conversationService;
        private readonly IAIRoleService _aIRoleService;
        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;
        private readonly ObservableCollection<ChatMessage> _emptyMessages = new();
        private Conversation? _currentConversation;
        private AIRole? _selectedRole;
        private bool _isRoleSwitchEnabled = true;

        public MainViewModel(IChatService chatService, IConversationService conversationService,IAIRoleService aIRoleService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _aIRoleService = aIRoleService;
            _logger = logger;


            Conversations = _conversationService.Conversations;

            // initialize built-in roles
            InitializeAsync();
            

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
                    // when conversation changes, update selected role to match
                    SelectedRole = CurrentConversation?.Role ?? Roles.FirstOrDefault();
                    // subscribe to collection changes to control role switching
                    SubscribeMessagesChanged();
                    ClearCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        [ObservableProperty]
        private ObservableCollection<AIRole> _roles = new ObservableCollection<AIRole>();
        public AIRole? SelectedRole
        {
            get => _selectedRole;
            set
            {
                // prevent switching when conversation already started
                if (CurrentConversation is not null && CurrentConversation.Messages.Count > 0)
                {
                    // ignore changes when not allowed
                    return;
                }

                if (SetProperty(ref _selectedRole, value))
                {
                    if (CurrentConversation is not null)
                    {
                        CurrentConversation.Role = value;
                    }
                }
            }
        }

        public bool IsRoleSwitchEnabled
        {
            get => _isRoleSwitchEnabled;
            private set => SetProperty(ref _isRoleSwitchEnabled, value);
        }
        public async Task InitializeAsync()
        {
            var roles = await _aIRoleService.GetRolesAsync();

            Roles.Clear();

            foreach (var role in roles)
            {
                Roles.Add(role);
            }

            SelectedRole = Roles.FirstOrDefault();

            // ensure there is at least one conversation
            if (Conversations.Count == 0)
            {
                var c = _conversationService.CreateConversation();
                // associate default role
                c.Role = Roles.FirstOrDefault();
                CurrentConversation = c;
            }
        }
        private void SubscribeMessagesChanged()
        {
            // unsubscribe previous
            if (_currentConversation is null)
            {
                IsRoleSwitchEnabled = true;
                return;
            }

            // try to unsubscribe old handler from other collection if any
            foreach (var conv in Conversations)
            {
                try
                {
                    conv.Messages.CollectionChanged -= Messages_CollectionChanged;
                }
                catch
                {
                    // ignore
                }
            }

            // subscribe new
            _currentConversation.Messages.CollectionChanged += Messages_CollectionChanged;

            // initial state
            IsRoleSwitchEnabled = _currentConversation.Messages.Count == 0;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // disable role switching once there is any message in the conversation
            if (CurrentConversation is null)
            {
                IsRoleSwitchEnabled = true;
                return;
            }
            IsRoleSwitchEnabled = CurrentConversation.Messages.Count == 0;
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
            // ensure system prompt from the selected role is present at the beginning of the conversation
            if (CurrentConversation is not null && CurrentConversation.Role is not null)
            {
                var hasSystem = CurrentConversation.Messages.Any(m => m.Role == ChatRole.System);
                if (!hasSystem && !string.IsNullOrWhiteSpace(CurrentConversation.Role.SystemPrompt))
                {
                    var systemMsg = new ChatMessage(ChatRole.System, CurrentConversation.Role.SystemPrompt, DateTime.Now);
                    CurrentConversation.Messages.Insert(0, systemMsg);
                }
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
            conv.Role = Roles.FirstOrDefault();
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
