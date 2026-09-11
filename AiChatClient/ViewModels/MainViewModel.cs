using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Models;
using AiChatClient.Models.Rag;
using AiChatClient.Services;
using AiChatClient.Services.Impl;
using AiChatClient.Services.Mcp;
using AiChatClient.Services.Rag;
using AiChatClient.Services.SemanticKernel;
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
        private readonly IRagService _ragService;
        // MCP Client 是单例服务，持有当前启动的 Server 子进程和 stdio 连接。
        private readonly IMcpClientService _mcpClientService;
        private readonly IOptionsMonitor<AiOptions> _aiOptions;

        private readonly ObservableCollection<AIProviderOptions> _aiProviders = new();
        private readonly ObservableCollection<AIModelOptions> _aiModels = new();
        private AIProviderOptions? _selectedAIProvider;
        private AIModelOptions? _selectedAIModel;

        private readonly ILogger<MainViewModel> _logger;
        private CancellationTokenSource? _currentRequestCts;
        private string _currentInput = string.Empty; 
        private bool _isBusy;
        private bool _isManagingConversation;
        private CancellationTokenSource? _knowledgeImportCts;
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }
        public bool CanManageConversations => !IsBusy && !_isManagingConversation;
        public IRelayCommand CancelKnowledgeImportCommand { get; }

        private void RefreshConversationCommands()
        {
            OnPropertyChanged(nameof(CanManageConversations));
            OnPropertyChanged(nameof(CanConfigureRequest));
            OnPropertyChanged(nameof(IsConversationConfigurationEditable));
            SendCommand?.NotifyCanExecuteChanged();
            NewConversationCommand?.NotifyCanExecuteChanged();
            DeleteConversationCommand?.NotifyCanExecuteChanged();
            RenameConversationCommand?.NotifyCanExecuteChanged();
            ClearCommand?.NotifyCanExecuteChanged();
        }
        private bool _isImportingKnowledge;
        private bool _isRagEnabled;
        private bool _isToolsEnabled;
        // MCP 区域的独立忙碌状态，不影响聊天区域的 IsBusy。
        private bool _isMcpBusy;
        private string _mcpConnectionStatus = "未连接";
        private string _mcpServerDisplayName = "—";
        private string _mcpResult = "连接 Server 后可以调用工具。";
        private McpToolInfo? _selectedMcpTool;
        private readonly ObservableCollection<ChatMessage> _emptyMessages = new();
        private Conversation? _currentConversation;
        private AIRole? _selectedRole;
        private bool _isConversationConfigurationEditable = true;
        private bool _isSynchronizingAIConfiguration;
        private bool _isInitialized;
        // 保持用户快速修改会话配置时的写入顺序，与 DbContext 线程安全无关。
        private readonly SemaphoreSlim _conversationConfigurationPersistenceLock = new(1, 1);
        private readonly Dictionary<Guid, long> _generationSettingsPersistenceVersions = new();

        public MainViewModel(IChatService chatService, IConversationService conversationService,
            IChatMessageService chatMessageService,
            IAIRoleService aIRoleService, IDialogService dialogService,
            ILogger<MainViewModel> logger, IOptionsMonitor<AiOptions> aiOptions,
            IRagService ragService, IMcpClientService mcpClientService)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _aIRoleService = aIRoleService;
            _chatMessageService = chatMessageService;
            _dialogService = dialogService;
            _ragService = ragService;
            _mcpClientService = mcpClientService;
            _aiOptions = aiOptions;
            _logger = logger;


            Conversations = _conversationService.Conversations;

            CancelKnowledgeImportCommand = new RelayCommand(
                () => _knowledgeImportCts?.Cancel(), () => IsImportingKnowledge);
            // Commands
            SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
            StopCommand = new RelayCommand(Stop, () => IsBusy);
            ClearCommand = new AsyncRelayCommand(ClearMessagesAsync, () => CanManageConversations && CurrentConversation is not null && Messages.Count > 0);
            NewConversationCommand = new AsyncRelayCommand(NewConversation, () => CanManageConversations);
            DeleteConversationCommand = new AsyncRelayCommand(DeleteConversation, () => CanManageConversations && CurrentConversation is not null);
            RenameConversationCommand = new AsyncRelayCommand(RenameConversationAsync, () => CanManageConversations && CurrentConversation is not null);
            AddKnowledgeFileCommand = new AsyncRelayCommand(
                AddKnowledgeFileAsync,
                () => !IsImportingKnowledge);
            RebuildKnowledgeFileCommand = new AsyncRelayCommand<KnowledgeFileItem>(
                RebuildKnowledgeFileAsync,
                CanManageKnowledgeFile);
            DeleteKnowledgeFileCommand = new AsyncRelayCommand<KnowledgeFileItem>(
                DeleteKnowledgeFileAsync,
                CanManageKnowledgeFile);
            ConnectMcpCommand = new AsyncRelayCommand(
                ConnectMcpAsync,
                () => !IsMcpBusy && !IsMcpConnected);
            DisconnectMcpCommand = new AsyncRelayCommand(
                DisconnectMcpAsync,
                () => !IsMcpBusy && IsMcpConnected);
            CallMcpToolCommand = new AsyncRelayCommand(
                CallMcpToolAsync,
                () => !IsMcpBusy && IsMcpConnected && SelectedMcpTool is not null);
        }

        public ObservableCollection<Conversation> Conversations { get; }

        public ObservableCollection<KnowledgeFileItem> KnowledgeFiles { get; } = new();

        /// <summary>
        /// MCP Server 通过 tools/list 返回的 Tool，供右侧列表绑定。
        /// </summary>
        public ObservableCollection<McpToolInfo> McpTools { get; } = new();

        public string McpConnectionStatus
        {
            get => _mcpConnectionStatus;
            private set => SetProperty(ref _mcpConnectionStatus, value);
        }

        public string McpServerDisplayName
        {
            get => _mcpServerDisplayName;
            private set => SetProperty(ref _mcpServerDisplayName, value);
        }

        public string McpResult
        {
            get => _mcpResult;
            private set => SetProperty(ref _mcpResult, value);
        }

        public McpToolInfo? SelectedMcpTool
        {
            get => _selectedMcpTool;
            set
            {
                if (SetProperty(ref _selectedMcpTool, value))
                {
                    CallMcpToolCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool IsMcpBusy
        {
            get => _isMcpBusy;
            private set
            {
                if (SetProperty(ref _isMcpBusy, value))
                {
                    RefreshMcpCommandStates();
                }
            }
        }

        /// <summary>
        /// 根据 Client 服务的真实状态判断是否已经完成 MCP 初始化。
        /// </summary>
        public bool IsMcpConnected =>
            _mcpClientService.State == McpConnectionState.Connected;

        public IAsyncRelayCommand ConnectMcpCommand { get; }

        public IAsyncRelayCommand DisconnectMcpCommand { get; }

        public IAsyncRelayCommand CallMcpToolCommand { get; }

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
                SendCommand?.NotifyCanExecuteChanged();
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

                SendCommand?.NotifyCanExecuteChanged();
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
                if (!CanManageConversations) return;
                SetCurrentConversation(value);
            }
        }

        private void SetCurrentConversation(Conversation? value)
        {
                if (SetProperty(ref _currentConversation, value, nameof(CurrentConversation)))
                {
                    // notify that Messages changed
                    OnPropertyChanged(nameof(Messages));
                    // 切换会话时同步显示其角色，不触发用户手动切换角色的限制。
                    SynchronizeSelectedRole();
                    // 先刷新会话配置是否可编辑，供后续同步逻辑使用。
                    SubscribeMessagesChanged();
                    // 根据会话保存的模型同步厂家和模型下拉框。
                    SynchronizeSelectedAIConfiguration();
                    NotifyGenerationSettingsChanged();
                    ClearCommand?.NotifyCanExecuteChanged();
                    DeleteConversationCommand?.NotifyCanExecuteChanged();
                    RenameConversationCommand?.NotifyCanExecuteChanged();
                    SendCommand?.NotifyCanExecuteChanged();
                    StatusMessage = value is null ? "暂无会话，请先新建会话。" : string.Empty;
                }
        }

        public double? Temperature
        {
            get => CurrentConversation?.GenerationSettings.Temperature;
            set
            {
                var conversation = CurrentConversation;
                if (!CanManageConversations || conversation is null)
                {
                    return;
                }

                var settings = conversation.GenerationSettings;
                if (settings.Temperature == value)
                {
                    return;
                }

                var previousSettings = CloneGenerationSettings(settings);
                settings.Temperature = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseProviderDefaultTemperature));
                ScheduleGenerationSettingsPersistence(conversation, previousSettings);
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
                var conversation = CurrentConversation;
                if (!CanManageConversations || conversation is null)
                {
                    return;
                }

                var settings = conversation.GenerationSettings;
                if (settings.TopP == value)
                {
                    return;
                }

                var previousSettings = CloneGenerationSettings(settings);
                settings.TopP = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseProviderDefaultTopP));
                ScheduleGenerationSettingsPersistence(conversation, previousSettings);
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

        public int? MaxTokens
        {
            get => CurrentConversation?.GenerationSettings.MaxTokens;
            set
            {
                var conversation = CurrentConversation;
                if (!CanManageConversations || conversation is null)
                {
                    return;
                }

                var settings = conversation.GenerationSettings;
                if (settings.MaxTokens == value)
                {
                    return;
                }

                var previousSettings = CloneGenerationSettings(settings);
                settings.MaxTokens = value;
                OnPropertyChanged();
                ScheduleGenerationSettingsPersistence(conversation, previousSettings);
            }
        }

        private void ScheduleGenerationSettingsPersistence(
            Conversation conversation,
            GenerationSettings previousSettings)
        {
            var version = _generationSettingsPersistenceVersions.TryGetValue(
                conversation.Id,
                out var currentVersion)
                ? currentVersion + 1
                : 1;

            _generationSettingsPersistenceVersions[conversation.Id] = version;

            _ = PersistConversationGenerationSettingsAsync(
                conversation,
                previousSettings,
                CloneGenerationSettings(conversation.GenerationSettings),
                version);
        }

        private async Task PersistConversationGenerationSettingsAsync(
            Conversation conversation,
            GenerationSettings previousSettings,
            GenerationSettings settingsSnapshot,
            long version)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(350));

            if (!IsLatestGenerationSettingsPersistence(conversation.Id, version))
            {
                return;
            }

            await _conversationConfigurationPersistenceLock.WaitAsync();

            try
            {
                if (!IsLatestGenerationSettingsPersistence(conversation.Id, version))
                {
                    return;
                }

                var updated = await _conversationService
                    .UpdateConversationConfigurationAsync(
                        conversation.Id,
                        settingsSnapshot);

                if (!updated)
                {
                    throw new InvalidOperationException(
                        $"Conversation '{conversation.Id}' was not found while updating generation settings.");
                }

                if (ReferenceEquals(CurrentConversation, conversation)
                    && IsLatestGenerationSettingsPersistence(conversation.Id, version))
                {
                    NotifyGenerationSettingsChanged();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist generation settings for conversation {ConversationId}.",
                    conversation.Id);

                if (IsLatestGenerationSettingsPersistence(conversation.Id, version))
                {
                    conversation.GenerationSettings = CloneGenerationSettings(previousSettings);

                    if (ReferenceEquals(CurrentConversation, conversation))
                    {
                        NotifyGenerationSettingsChanged();
                    }
                }
            }
            finally
            {
                _conversationConfigurationPersistenceLock.Release();
            }
        }

        private bool IsLatestGenerationSettingsPersistence(
            Guid conversationId,
            long version)
        {
            return _generationSettingsPersistenceVersions.TryGetValue(
                conversationId,
                out var latestVersion)
                && latestVersion == version;
        }

        private void NotifyGenerationSettingsChanged()
        {
            OnPropertyChanged(nameof(Temperature));
            OnPropertyChanged(nameof(TopP));
            OnPropertyChanged(nameof(MaxTokens));
            OnPropertyChanged(nameof(UseProviderDefaultTemperature));
            OnPropertyChanged(nameof(UseProviderDefaultTopP));
        }

        private static GenerationSettings CloneGenerationSettings(
            GenerationSettings settings)
        {
            return new GenerationSettings
            {
                Temperature = settings.Temperature,
                TopP = settings.TopP,
                MaxTokens = settings.MaxTokens
            };
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
                var selection = AIConfigurationSelectionResolver.Resolve(
                    AIProviders,
                    _selectedAIProvider,
                    conversationModelId);

                // Always rebuild the model collection. Calling the public provider setter here used
                // to return early when two conversations shared a provider, leaving a stale model in UI.
                SetProperty(
                    ref _selectedAIProvider,
                    selection.Provider,
                    nameof(SelectedAIProvider));

                AIModels.Clear();
                foreach (var model in selection.Models)
                {
                    AIModels.Add(model);
                }

                SetProperty(
                    ref _selectedAIModel,
                    selection.Model,
                    nameof(SelectedAIModel));
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
            get => _isConversationConfigurationEditable && CanManageConversations;
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

            foreach (var provider in _aiOptions.CurrentValue.Providers.Where(provider =>
                         AIProviderNames.IsSupported(provider.Name)))
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
                    RefreshConversationCommands();
                }
            }
        }

        public bool CanConfigureRequest => CanManageConversations;

        public bool IsRagEnabled
        {
            get => _isRagEnabled;
            set => SetProperty(ref _isRagEnabled, value);
        }

        public bool IsToolsEnabled
        {
            get => _isToolsEnabled;
            set => SetProperty(ref _isToolsEnabled, value);
        }

        public bool IsImportingKnowledge
        {
            get => _isImportingKnowledge;
            private set
            {
                if (SetProperty(ref _isImportingKnowledge, value))
                {
                    CancelKnowledgeImportCommand.NotifyCanExecuteChanged();
                    AddKnowledgeFileCommand.NotifyCanExecuteChanged();
                    RebuildKnowledgeFileCommand.NotifyCanExecuteChanged();
                    DeleteKnowledgeFileCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public IAsyncRelayCommand SendCommand { get; }

        public IRelayCommand StopCommand { get; }

        public IAsyncRelayCommand ClearCommand { get; }

        public IAsyncRelayCommand AddKnowledgeFileCommand { get; }

        public IAsyncRelayCommand<KnowledgeFileItem> RebuildKnowledgeFileCommand { get; }

        public IAsyncRelayCommand<KnowledgeFileItem> DeleteKnowledgeFileCommand { get; }

        /// <summary>
        /// 响应“连接”：启动 Server、完成 initialize，然后立即执行 tools/list。
        /// </summary>
        private async Task ConnectMcpAsync()
        {
            if (IsMcpBusy || IsMcpConnected) return;
            IsMcpBusy = true;
            McpConnectionStatus = "正在连接…";
            McpServerDisplayName = "—";
            McpResult = "正在启动本地 MCP Server…";
            McpTools.Clear();
            SelectedMcpTool = null;

            try
            {
                // 协议握手和 Tool 发现分成两个调用，便于观察 MCP 的两个阶段。
                await _mcpClientService.ConnectAsync();
                var tools = await _mcpClientService.ListToolsAsync();
                foreach (var tool in tools)
                {
                    McpTools.Add(tool);
                }

                SelectedMcpTool = McpTools.FirstOrDefault();
                var serverInfo = _mcpClientService.ServerInfo;
                McpServerDisplayName = serverInfo is null
                    ? "未知 Server"
                    : $"{serverInfo.Name}  v{serverInfo.Version}";
                McpConnectionStatus = "已连接";
                McpResult = McpTools.Count == 0
                    ? "Server 没有提供 Tool。"
                    : "请选择 Tool 并点击调用。";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "连接 MCP Server 失败。");
                try
                {
                    await _mcpClientService.DisconnectAsync();
                }
                catch (Exception disconnectException)
                {
                    _logger.LogWarning(disconnectException, "清理 MCP Server 连接失败。");
                }

                McpConnectionStatus = "连接失败";
                McpServerDisplayName = "—";
                McpResult = exception.Message;
                McpTools.Clear();
                SelectedMcpTool = null;
            }
            finally
            {
                IsMcpBusy = false;
                OnPropertyChanged(nameof(IsMcpConnected));
                RefreshMcpCommandStates();
            }
        }

        /// <summary>
        /// 响应“断开”：关闭 MCP 连接，并清空只对本次连接有效的 UI 数据。
        /// </summary>
        private async Task DisconnectMcpAsync()
        {
            if (IsMcpBusy || !IsMcpConnected) return;
            IsMcpBusy = true;
            McpConnectionStatus = "正在断开…";

            try
            {
                await _mcpClientService.DisconnectAsync();
                McpConnectionStatus = "未连接";
                McpServerDisplayName = "—";
                McpResult = "连接 Server 后可以调用工具。";
                McpTools.Clear();
                SelectedMcpTool = null;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "断开 MCP Server 失败。");
                McpConnectionStatus = "断开失败";
                McpResult = exception.Message;
            }
            finally
            {
                IsMcpBusy = false;
                OnPropertyChanged(nameof(IsMcpConnected));
                RefreshMcpCommandStates();
            }
        }

        /// <summary>
        /// 响应“调用选中 Tool”，把 tools/call 的文本结果显示到页面。
        /// </summary>
        private async Task CallMcpToolAsync()
        {
            if (IsMcpBusy || !IsMcpConnected) return;
            var tool = SelectedMcpTool;
            if (tool is null)
            {
                return;
            }

            IsMcpBusy = true;
            McpResult = $"正在调用 {tool.Name}…";
            try
            {
                McpResult = await _mcpClientService.CallToolAsync(tool.Name);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "调用 MCP Tool {ToolName} 失败。", tool.Name);
                McpResult = exception.Message;
                if (!IsMcpConnected)
                {
                    McpConnectionStatus = "连接已断开";
                    McpServerDisplayName = "—";
                    McpTools.Clear();
                    SelectedMcpTool = null;
                }
            }
            finally
            {
                IsMcpBusy = false;
                OnPropertyChanged(nameof(IsMcpConnected));
                RefreshMcpCommandStates();
            }
        }

        /// <summary>
        /// 连接状态或忙碌状态改变后，重新计算三个 MCP 按钮能否点击。
        /// </summary>
        private void RefreshMcpCommandStates()
        {
            ConnectMcpCommand.NotifyCanExecuteChanged();
            DisconnectMcpCommand.NotifyCanExecuteChanged();
            CallMcpToolCommand.NotifyCanExecuteChanged();
        }

        private bool CanSend() => CanManageConversations
            && CurrentConversation is not null
            && SelectedAIProvider is not null
            && AIProviderNames.IsSupported(SelectedAIProvider.Name)
            && SelectedAIModel is { IsEnabled: true }
            && AIModels.Contains(SelectedAIModel)
            && !string.IsNullOrWhiteSpace(SelectedAIModel.ModelId)
            && !string.IsNullOrWhiteSpace(CurrentInput);

        private async Task SendAsync()
        {
            if (!CanSend()) return;
            var conversation = CurrentConversation!;
            var input = CurrentInput.Trim();
            var provider = SelectedAIProvider!.Name;
            var model = SelectedAIModel!.ModelId;
            var settings = CloneGenerationSettings(conversation.GenerationSettings);
            var rolePrompt = conversation.Role?.SystemPrompt;
            var enableRag = IsRagEnabled;
            var enableTools = IsToolsEnabled;
            using var requestCts = new CancellationTokenSource();
            _currentRequestCts = requestCts;
            IsBusy = true;
            StatusMessage = string.Empty;
            ChatMessage? answer = null;
            var phase = "保存消息";
            try
            {
                var history = conversation.Messages.Where(message => !message.IsTransient)
                    .Where(message => message.Sources.Count == 0 || message.Sources.All(
                        source => _ragService.IsDocumentIndexed(source.SourcePath)))
                    .Select(message => new ChatRequestMessage { Role = message.Role, Content = message.Content })
                    .ToList();
                if (!history.Any(message => message.Role == ChatRole.System)
                    && !string.IsNullOrWhiteSpace(rolePrompt))
                {
                    var system = new ChatMessage(ChatRole.System, rolePrompt, DateTime.Now);
                    await _chatMessageService.AddMessageAsync(conversation.Id, system);
                    conversation.Messages.Insert(0, system);
                    history.Insert(0, new ChatRequestMessage { Role = system.Role, Content = system.Content });
                }
                requestCts.Token.ThrowIfCancellationRequested();
                var user = new ChatMessage(ChatRole.User, input, DateTime.Now);
                await _chatMessageService.AddMessageAsync(conversation.Id, user);
                conversation.Messages.Add(user);
                history.Add(new ChatRequestMessage { Role = user.Role, Content = input });
                if (CurrentInput.Trim() == input) CurrentInput = string.Empty;
                requestCts.Token.ThrowIfCancellationRequested();

                phase = "生成回答";
                IReadOnlyList<VectorSearchResult> results = Array.Empty<VectorSearchResult>();
                if (enableRag)
                {
                    if (!KnowledgeFiles.Any(item => _ragService.IsDocumentIndexed(item.SourcePath)))
                        StatusMessage = "知识库未导入有效文档，本次使用普通聊天。";
                    else
                    {
                        try
                        {
                            results = await _ragService.RetrieveAsync(input, requestCts.Token);
                            StatusMessage = results.Count == 0
                                ? "未找到匹配资料，本次使用普通聊天。"
                                : $"本次检索到 {results.Count} 条参考资料。";
                        }
                        catch (Exception ex) when (!requestCts.IsCancellationRequested)
                        {
                            _logger.LogWarning(ex, "Knowledge retrieval failed.");
                            StatusMessage = ex is TimeoutException
                                ? "知识库检索超时，本次使用普通聊天。"
                                : "知识库检索失败，本次使用普通聊天。";
                        }
                    }
                }
                requestCts.Token.ThrowIfCancellationRequested();
                var request = new ChatRequest
                {
                    Messages = enableRag ? RagPromptComposer.AddContextToLatestUserMessage(history, input, results) : history,
                    Provider = provider, Model = model, Settings = settings,
                    EnableTools = enableTools,
                    RequireToolCall = enableTools && ToolIntentDetector.RequiresToolCall(input)
                };
                answer = new ChatMessage(ChatRole.Assistant, string.Empty, DateTime.Now) { IsTransient = true };
                conversation.Messages.Add(answer);
                var displayingTool = false;
                await foreach (var ev in _chatService.SendStreamingAsync(request, requestCts.Token))
                {
                    if (ev.Type == ChatStreamEventType.ToolCalling)
                    {
                        answer.Content = $"正在调用工具：{ev.ToolName}…";
                        displayingTool = true;
                    }
                    else if (ev.Type == ChatStreamEventType.TextDelta)
                    {
                        if (displayingTool) { answer.Content = string.Empty; displayingTool = false; }
                        answer.Content += ev.Content;
                    }
                }
                requestCts.Token.ThrowIfCancellationRequested();
                if (displayingTool || string.IsNullOrWhiteSpace(answer.Content))
                    throw new InvalidOperationException("模型未返回有效回答，请重试或更换模型。");
                answer.Sources = results.Select(result => new RagSourceReference(
                    result.Chunk.FileName, result.Chunk.SourcePath, result.Chunk.HeadingPath,
                    result.Chunk.StartLine, result.Chunk.EndLine, result.Score)).ToArray();
                answer.RagWasEnabled = enableRag;
                phase = "保存回答";
                await _chatMessageService.AddMessageAsync(conversation.Id, answer);
                answer.IsTransient = false;
            }
            catch (OperationCanceledException) when (requestCts.IsCancellationRequested)
            {
                StatusMessage = "已停止生成，可继续发送。";
                if (answer is not null) answer.Content += "\n\n（已停止，未保存）";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat operation failed during {Phase}.", phase);
                StatusMessage = $"{phase}失败：{ex.Message}";
                if (answer is not null)
                    answer.Content += phase == "保存回答" ? "\n\n（回答未保存）" : "\n\n（生成失败，未保存）";
                _dialogService.ShowError($"{phase}失败", phase == "保存回答"
                    ? "回答仍显示在页面，但未保存，也不会作为后续对话历史。\n" + ex.Message
                    : ex.Message);
            }
            finally
            {
                _currentRequestCts = null;
                IsBusy = false;
            }
        }

        private void Stop()
        {
            _currentRequestCts?.Cancel();
        }

        private async Task AddKnowledgeFileAsync()
        {
            if (IsImportingKnowledge) return;
            try
            {
            var filePath = _dialogService.ShowMarkdownFileDialog();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var fullPath = Path.GetFullPath(filePath);
            var item = KnowledgeFiles.FirstOrDefault(candidate => string.Equals(
                candidate.SourcePath,
                fullPath,
                StringComparison.OrdinalIgnoreCase));

            if (item is null)
            {
                item = new KnowledgeFileItem(fullPath);
                KnowledgeFiles.Add(item);
            }

            await VectorizeKnowledgeFileAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Selecting knowledge file failed.");
                _dialogService.ShowError("添加知识文件失败", ex.Message);
            }
        }

        private bool CanManageKnowledgeFile([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] KnowledgeFileItem? item)
        {
            return item is not null && !IsImportingKnowledge && !item.IsProcessing;
        }

        private Task RebuildKnowledgeFileAsync(KnowledgeFileItem? item)
        {
            return !CanManageKnowledgeFile(item)
                ? Task.CompletedTask
                : VectorizeKnowledgeFileAsync(item);
        }

        private async Task VectorizeKnowledgeFileAsync(KnowledgeFileItem item)
        {
            if (!CanManageKnowledgeFile(item)) return;
            var hadVectors = item.IsVectorized;
            if (!File.Exists(item.SourcePath))
            {
                item.Status = hadVectors
                    ? "源文件不存在（保留原向量）"
                    : "源文件不存在";
                return;
            }

            item.Status = hadVectors
                ? "正在重新向量化…"
                : "正在读取并向量化…";
            using var importCts = new CancellationTokenSource();
            _knowledgeImportCts = importCts;
            item.IsProcessing = true;
            IsImportingKnowledge = true;

            try
            {
                var result = await _ragService.ReplaceDocumentAsync(item.SourcePath, importCts.Token);
                item.ChunkCount = result.ChunkCount;
                item.IsVectorized = true;
                item.Status = "已向量化";
            }
            catch (OperationCanceledException) when (importCts.IsCancellationRequested)
            {
                item.IsVectorized = hadVectors;
                item.Status = hadVectors ? "已取消（保留原向量）" : "已取消";
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to import Markdown knowledge file. Path: {FilePath}",
                    item.SourcePath);
                item.IsVectorized = hadVectors;
                item.Status = hadVectors
                    ? $"更新失败（保留原向量）：{exception.Message}"
                    : $"失败：{exception.Message}";
            }
            finally
            {
                _knowledgeImportCts = null;
                item.IsProcessing = false;
                IsImportingKnowledge = false;
            }
        }

        private Task DeleteKnowledgeFileAsync(KnowledgeFileItem? item)
        {
            if (!CanManageKnowledgeFile(item))
            {
                return Task.CompletedTask;
            }

            item.Status = "正在移出知识库…";
            item.IsProcessing = true;
            IsImportingKnowledge = true;

            try
            {
                _ragService.DeleteDocument(item.SourcePath);
                KnowledgeFiles.Remove(item);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to remove Markdown knowledge file. Path: {FilePath}",
                    item.SourcePath);
                item.Status = $"删除失败：{exception.Message}";
            }
            finally
            {
                _knowledgeImportCts = null;
                item.IsProcessing = false;
                IsImportingKnowledge = false;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 删除当前会话的全部消息，并在数据库删除成功后同步清空页面消息列表。
        /// </summary>
        private async Task RunConversationOperationAsync(string title, Func<Task> operation)
        {
            if (!CanManageConversations) return;
            _isManagingConversation = true;
            RefreshConversationCommands();
            try { await operation(); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Conversation operation failed: {Operation}", title);
                StatusMessage = $"{title}失败：{ex.Message}";
                _dialogService.ShowError($"{title}失败", ex.Message);
            }
            finally
            {
                _isManagingConversation = false;
                RefreshConversationCommands();
            }
        }

        private Task ClearMessagesAsync()
        {
            var conversation = CurrentConversation;
            if (conversation is null) return Task.CompletedTask;
            return RunConversationOperationAsync("清空会话", async () =>
            {
                await _chatMessageService.DeleteMessagesByConversationIdAsync(conversation.Id);
                conversation.Messages.Clear();
                StatusMessage = "已清空会话。";
            });
        }

        private Task NewConversation() => RunConversationOperationAsync("新建会话", async () =>
        {
            var conversation = new Conversation
            {
                Id = Guid.NewGuid(), Title = "New Chat", CreatedTime = DateTime.Now,
                UpdatedTime = DateTime.Now, Model = SelectedAIModel?.ModelId ?? string.Empty,
                Role = SelectedRole
            };
            var result = await _conversationService.CreateConversation(conversation);
            SetCurrentConversation(result);
        });

        private Task DeleteConversation()
        {
            var conversation = CurrentConversation;
            if (conversation is null) return Task.CompletedTask;
            return RunConversationOperationAsync("删除会话", async () =>
            {
                await _conversationService.DeleteConversationAsync(conversation.Id);
                SetCurrentConversation(Conversations.FirstOrDefault());
            });
        }

        private Task RenameConversationAsync()
        {
            var conversation = CurrentConversation;
            if (conversation is null) return Task.CompletedTask;
            return RunConversationOperationAsync("重命名会话", async () =>
            {
                var title = _dialogService.ShowInputDialog("重命名会话", "请输入新的会话名称:", conversation.Title)?.Trim();
                if (string.IsNullOrWhiteSpace(title) || title == conversation.Title) return;
                if (!await _conversationService.RenameConversationAsync(conversation.Id, title))
                    throw new InvalidOperationException("会话不存在，请重新选择。");
                StatusMessage = "会话已重命名。";
            });
        }
    }
}
