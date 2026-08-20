using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Models;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        private readonly IOptionsMonitor<AiOptions> _aiOptions;

        private readonly ObservableCollection<AIProviderOptions> _aiProviders = new();
        private readonly ObservableCollection<AIModelOptions> _aiModels = new();
        private AIProviderOptions? _selectedAIProvider;
        private AIModelOptions? _selectedAIModel;

        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;
        private readonly ObservableCollection<ChatMessage> _emptyMessages = new();
        private Conversation? _currentConversation;
        private AIRole? _selectedRole;
        private bool _isConversationConfigurationEditable = true;
        private bool _isSynchronizingAIConfiguration;
        private bool _isInitialized;
        // 保持用户快速修改会话配置时的写入顺序，与 DbContext 线程安全无关。
        private readonly SemaphoreSlim _conversationConfigurationPersistenceLock = new(1, 1);

        public MainViewModel(IChatService chatService, IConversationService conversationService,
           IChatMessageService chatMessageService,
            IAIRoleService aIRoleService, IDialogService dialogService,
            ILogger<MainViewModel> logger, IOptionsMonitor<AiOptions> aiOptions)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _aIRoleService = aIRoleService;
            _chatMessageService = chatMessageService;
            _dialogService = dialogService;
            _aiOptions = aiOptions;

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

        /// <summary>
        /// 从 appsettings.json 的 AI:Providers 节点加载的服务厂家列表。
        /// </summary>
        public ObservableCollection<AIProviderOptions> AIProviders => _aiProviders;

        /// <summary>
        /// 当前选中厂家下启用的模型列表。
        /// </summary>
        public ObservableCollection<AIModelOptions> AIModels => _aiModels;

        /// <summary>
        /// UI 上当前选中的 AI 厂家。切换厂家时刷新模型下拉框。
        /// </summary>
        public AIProviderOptions? SelectedAIProvider
        {
            get => _selectedAIProvider;
            set
            {
                if (!_isSynchronizingAIConfiguration && !IsConversationConfigurationEditable)
                {
                    return;
                }

                if (!SetProperty(ref _selectedAIProvider, value))
                {
                    return;
                }

                AIModels.Clear();

                if (value is not null)
                {
                    foreach (var model in value.Models.Where(model => model.IsEnabled))
                    {
                        AIModels.Add(model);
                    }
                }

                SelectedAIModel = AIModels.FirstOrDefault();
            }
        }

        /// <summary>
        /// UI 上当前选中的 AI 模型。会话开始前变更时，会同步并持久化到当前会话。
        /// </summary>
        public AIModelOptions? SelectedAIModel
        {
            get => _selectedAIModel;
            set
            {
                if (!_isSynchronizingAIConfiguration && !IsConversationConfigurationEditable)
                {
                    return;
                }

                if (!SetProperty(ref _selectedAIModel, value))
                {
                    return;
                }

                if (_isSynchronizingAIConfiguration || CurrentConversation is null)
                {
                    return;
                }

                var modelId = value?.ModelId ?? string.Empty;
                if (string.Equals(CurrentConversation.Model, modelId, StringComparison.Ordinal))
                {
                    return;
                }

                var conversation = CurrentConversation;
                var previousModelId = conversation.Model;
                conversation.Model = modelId;
                _ = PersistConversationModelAsync(conversation, previousModelId, modelId);
            }
        }

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
                    // 先刷新会话配置是否可编辑，供后续同步逻辑使用。
                    SubscribeMessagesChanged();
                    // 根据会话保存的模型同步厂家和模型下拉框。
                    SynchronizeSelectedAIConfiguration();
                    OnPropertyChanged(nameof(Temperature));
                    OnPropertyChanged(nameof(TopP));
                    OnPropertyChanged(nameof(UseProviderDefaultTemperature));
                    OnPropertyChanged(nameof(UseProviderDefaultTopP));
                    ClearCommand?.NotifyCanExecuteChanged();
                    DeleteConversationCommand?.NotifyCanExecuteChanged();
                    RenameConversationCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public double? Temperature
        {
            get => CurrentConversation?.GenerationSettings.Temperature;
            set
            {
                var settings = CurrentConversation?.GenerationSettings;
                if (settings is null || settings.Temperature == value)
                {
                    return;
                }

                settings.Temperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseProviderDefaultTemperature));
            }
        }

        public bool UseProviderDefaultTemperature
        {
            get => Temperature is null;
            set
            {
                if (value)
                {
                    Temperature = null;
                }
                else if (Temperature is null)
                {
                    Temperature = 0.5;
                }
            }
        }

        public double? TopP
        {
            get => CurrentConversation?.GenerationSettings.TopP;
            set
            {
                var settings = CurrentConversation?.GenerationSettings;
                if (settings is null || settings.TopP == value)
                {
                    return;
                }

                settings.TopP = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseProviderDefaultTopP));
            }
        }

        public bool UseProviderDefaultTopP
        {
            get => TopP is null;
            set
            {
                if (value)
                {
                    TopP = null;
                }
                else if (TopP is null)
                {
                    TopP = 0.5;
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

                // 会话开始后不允许再修改其配置。
                if (!IsConversationConfigurationEditable)
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
        /// 根据当前会话保存的 ModelId 同步模型及其所属厂家。
        /// 该同步只更新界面选中项，不修改会话中已保存的模型。
        /// </summary>
        private void SynchronizeSelectedAIConfiguration()
        {
            _isSynchronizingAIConfiguration = true;
            try
            {
                var conversationModelId = CurrentConversation?.Model;
                var provider = string.IsNullOrWhiteSpace(conversationModelId)
                    ? null
                    : AIProviders.FirstOrDefault(candidate => candidate.Models.Any(model =>
                        string.Equals(model.ModelId, conversationModelId, StringComparison.OrdinalIgnoreCase)));

                // 先设置厂家，以便通过其 setter 刷新 AIModels。
                SelectedAIProvider = provider ?? AIProviders.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(conversationModelId))
                {
                    return;
                }

                var selectedModel = AIModels.FirstOrDefault(model =>
                    string.Equals(model.ModelId, conversationModelId, StringComparison.OrdinalIgnoreCase));

                if (selectedModel is not null)
                {
                    SelectedAIModel = selectedModel;
                }
            }
            finally
            {
                _isSynchronizingAIConfiguration = false;
            }
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
            await _conversationConfigurationPersistenceLock.WaitAsync();

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
                _conversationConfigurationPersistenceLock.Release();
            }
        }

        /// <summary>
        /// 将用户为尚未开始的会话选择的新模型写入数据库。
        /// </summary>
        private async Task PersistConversationModelAsync(
            Conversation conversation,
            string previousModelId,
            string modelId)
        {
            await _conversationConfigurationPersistenceLock.WaitAsync();

            try
            {
                var updated = await _conversationService
                    .UpdateConversationModelAsync(conversation.Id, modelId);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        $"Conversation '{conversation.Id}' was not found while updating its model.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist model change for conversation {ConversationId}.",
                    conversation.Id);

                if (string.Equals(conversation.Model, modelId, StringComparison.Ordinal))
                {
                    conversation.Model = previousModelId;
                }

                if (ReferenceEquals(CurrentConversation, conversation))
                {
                    SynchronizeSelectedAIConfiguration();
                }
            }
            finally
            {
                _conversationConfigurationPersistenceLock.Release();
            }
        }

        /// <summary>
        /// 当前会话的厂家、模型和角色是否仍可修改。
        /// 会话中已有消息时，配置会被锁定以保持后续消息使用同一组设置。
        /// </summary>
        public bool IsConversationConfigurationEditable
        {
            get => _isConversationConfigurationEditable;
            private set => SetProperty(ref _isConversationConfigurationEditable, value);
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

            LoadAIConfiguration();

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

        /// <summary>
        /// 将当前配置快照复制到 UI 的厂家和模型下拉框数据源。
        /// </summary>
        private void LoadAIConfiguration()
        {
            AIProviders.Clear();

            foreach (var provider in _aiOptions.CurrentValue.Providers)
            {
                AIProviders.Add(provider);
            }

            SelectedAIProvider = AIProviders.FirstOrDefault();
        }
        private void SubscribeMessagesChanged()
        {
            // unsubscribe previous
            if (_currentConversation is null)
            {
                IsConversationConfigurationEditable = true;
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
            IsConversationConfigurationEditable = _currentConversation.Messages.Count == 0;
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 会话产生消息后锁定厂家、模型和角色配置。
            if (CurrentConversation is null)
            {
                IsConversationConfigurationEditable = true;
                return;
            }
            IsConversationConfigurationEditable = CurrentConversation.Messages.Count == 0;
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
                var conversationModel = CurrentConversation.Model;
                var request = new ChatRequest
                {
                    Messages = Messages,
                    Provider = SelectedAIProvider?.Name ?? string.Empty,
                    Model = string.IsNullOrWhiteSpace(conversationModel)
                        ? SelectedAIModel?.ModelId ?? string.Empty
                        : conversationModel,
                    Settings = new GenerationSettings
                    {
                        Temperature = CurrentConversation?.GenerationSettings?.Temperature,
                        MaxTokens = CurrentConversation?.GenerationSettings?.MaxTokens,
                        TopP = CurrentConversation?.GenerationSettings?.TopP
                    }
                };

                await foreach (var line in _chatService.SendStreamingAsync(request, _currentRequestCts.Token))
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

                Model = SelectedAIModel?.ModelId ?? string.Empty,

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
