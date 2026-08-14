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
using Models;
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
        // 仅用于保持用户快速连续选择角色时的写入顺序，与 DbContext 线程安全无关。
        private readonly SemaphoreSlim _rolePersistenceLock = new(1, 1);

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
            ClearCommand = new AsyncRelayCommand(ClearMessagesAsync, () => !IsBusy && Messages.Count > 0);
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
                    // 切换会话时同步显示其角色，不触发用户手动切换角色的限制。
                    SynchronizeSelectedRole();
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
                if (value is null)
                {
                    return;
                }

                // prevent switching when conversation already started
                if (CurrentConversation is not null && CurrentConversation.Messages.Count > 0)
                {
                    // ignore changes when not allowed
                    return;
                }

                if (SetProperty(ref _selectedRole, value))
                {
                    var conversation = CurrentConversation;

                    if (conversation is not null && conversation.Role?.Id != value.Id)
                    {
                        var previousRole = conversation.Role;
                        conversation.Role = value;
                        _ = PersistConversationRoleAsync(
                            conversation,
                            previousRole,
                            value.Id);
                    }
                }
            }
        }

        /// <summary>
        /// 按角色 ID 从下拉列表数据源中选取角色，确保切换会话时能正确显示其角色。
        /// 此同步不经过 <see cref="SelectedRole"/> 的 setter，避免被“已有消息时禁止手动切换”的规则拦截。
        /// </summary>
        private void SynchronizeSelectedRole()
        {
            var currentRoleId = CurrentConversation?.Role?.Id;
            var selectedRole = currentRoleId is null
                ? Roles.FirstOrDefault()
                : Roles.FirstOrDefault(role => role.Id == currentRoleId);

            if (CurrentConversation is not null && selectedRole is not null)
            {
                CurrentConversation.Role = selectedRole;
            }

            SetProperty(ref _selectedRole, selectedRole, nameof(SelectedRole));
        }

        /// <summary>
        /// 将用户为尚未开始的会话选择的新角色写入数据库。
        /// 通过互斥锁避免快速连续切换时并发使用同一个数据库上下文。
        /// </summary>
        private async Task PersistConversationRoleAsync(
            Conversation conversation,
            AIRole? previousRole,
            Guid roleId)
        {
            await _rolePersistenceLock.WaitAsync();

            try
            {
                var updated = await _conversationService
                    .UpdateConversationRoleAsync(conversation.Id, roleId);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        $"Conversation '{conversation.Id}' was not found while updating its role.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist role change for conversation {ConversationId}.",
                    conversation.Id);

                if (conversation.Role?.Id == roleId)
                {
                    conversation.Role = previousRole;
                }

                if (ReferenceEquals(CurrentConversation, conversation))
                {
                    SynchronizeSelectedRole();
                }
            }
            finally
            {
                _rolePersistenceLock.Release();
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
                CurrentConversation = c;
            }
            else
            {
                CurrentConversation = Conversations.FirstOrDefault();
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
                    ClearCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand SendCommand { get; }

        public IRelayCommand StopCommand { get; }

        public IAsyncRelayCommand ClearCommand { get; }

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
                chatMessage.Content = "已停止生成。";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send message to AI service. UserInput: {UserInput}", input);
                chatMessage.Content = "AI 响应生成失败，请稍后重试。";

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

        /// <summary>
        /// 删除当前会话的全部消息，并在数据库删除成功后同步清空页面消息列表。
        /// </summary>
        private async Task ClearMessagesAsync()
        {
            var conversation = CurrentConversation;

            if (conversation is null)
            {
                return;
            }

            await _chatMessageService
                .DeleteMessagesByConversationIdAsync(conversation.Id);

            conversation.Messages.Clear();
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
