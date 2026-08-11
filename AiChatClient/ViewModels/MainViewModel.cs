using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using AiChatClient.Models;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Repositories;
using Repositories.Impl;
using Services;
using Services.Impl;

namespace AiChatClient.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IChatService _chatService;
        private readonly IConversationService _conversationService;
        private readonly IAIRoleService _aIRoleService;
        private readonly IChatMessageService _chatMessageService;    
        private readonly IDialogService _dialogService;

        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;
        private readonly ObservableCollection<ChatMessage> _emptyMessages = new();
        private Conversation? _currentConversation;
        private AIRole? _selectedRole;
        private bool _isRoleSwitchEnabled = true;
        private bool _isInitialized;

        public MainViewModel(IChatService chatService, IConversationService conversationService,
           IChatMessageService chatMessageService,
            IAIRoleService aIRoleService, IDialogService dialogService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _aIRoleService = aIRoleService;
            _chatMessageService = chatMessageService;
            _dialogService = dialogService;

            _logger = logger;


            Conversations = _conversationService.Conversations;

            // Commands
            SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
            StopCommand = new RelayCommand(Stop, () => IsBusy);
            ClearCommand = new RelayCommand(ClearMessages, () => Messages.Count > 0);
            NewConversationCommand = new AsyncRelayCommand(NewConversation);
            DeleteConversationCommand = new AsyncRelayCommand(DeleteConversation, () => CurrentConversation is not null);
            RenameConversationCommand = new AsyncRelayCommand(RenameConversationAsync, () => CurrentConversation is not null);
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
                    DeleteConversationCommand?.NotifyCanExecuteChanged();
                    RenameConversationCommand?.NotifyCanExecuteChanged();
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
        /// <summary>
        /// 加载 AI 角色与会话数据。重入保护：无论被调用多少次，只执行一次。
        /// 由 <c>App.OnStartup</c> 在窗口显示前 await 调用，不在构造函数中触发。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;

            var roles = await _aIRoleService.GetRolesAsync();

            Roles.Clear();

            foreach (var role in roles)
            {
                Roles.Add(role);
            }

            SelectedRole = Roles.FirstOrDefault();

            await _conversationService.InitializeAsync();
            // ensure there is at least one conversation
            if (Conversations.Count == 0)
            {
                var c =await  _conversationService.CreateConversation(new Conversation
                {
                    Id = Guid.NewGuid(),
                    Title = "New Chat",
                    CreatedTime = DateTime.Now,
                    UpdatedTime = DateTime.Now,
                    Model = string.Empty,
                    Role = SelectedRole
                });
                // associate default role
                c.Role = SelectedRole;
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

        public IAsyncRelayCommand DeleteConversationCommand { get; }

        public IAsyncRelayCommand RenameConversationCommand { get; }

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
                    await _chatMessageService.AddMessageAsync(CurrentConversation.Id, systemMsg);
                }
            }

            var userMessage =  AddMessage(ChatRole.User, input);
            await _chatMessageService.AddMessageAsync(CurrentConversation.Id, userMessage);
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
                // save the assistant message to the database
                if (CurrentConversation is not null)
                {
                    await _chatMessageService.AddMessageAsync(CurrentConversation.Id, chatMessage);
                }
            }
            catch (OperationCanceledException)
            {
                chatMessage.Content = "��ֹͣ���ɡ�";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to AI service. UserInput: {UserInput}", input);
                chatMessage.Content = "AI ��������ʧ�ܣ����Ժ����ԡ�";

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

        private ChatMessage AddMessage(ChatRole role, string content)
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
            return msg;
        }

        private async Task NewConversation()
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),

                Title = "New Chat",

                CreatedTime = DateTime.Now,

                UpdatedTime = DateTime.Now,

                Model = string.Empty,

                Role = SelectedRole
            };


            var result =
                await _conversationService.CreateConversation(conversation);


            CurrentConversation = result;
        }
        private async Task DeleteConversation()
        {
            if (CurrentConversation is null) return;
            var id = CurrentConversation.Id;
            await _conversationService.DeleteConversationAsync(id);
            // pick another conversation if any
            CurrentConversation = Conversations.FirstOrDefault();
        }

        /// <summary>
        /// 重命名当前会话：通过对话框服务获取新名称，再交给服务层处理。
        /// </summary>
        private async Task RenameConversationAsync()
        {
            if (CurrentConversation is null) return;

            var newTitle = _dialogService.ShowInputDialog(
                "重命名会话",
                "请输入新的会话名称:",
                CurrentConversation.Title);

            if (string.IsNullOrWhiteSpace(newTitle)) return;

            var trimmedTitle = newTitle.Trim();

            // 名称未变化时无需处理
            if (trimmedTitle == CurrentConversation.Title) return;

            await _conversationService.RenameConversationAsync(CurrentConversation.Id, trimmedTitle);
        }
    }
}
