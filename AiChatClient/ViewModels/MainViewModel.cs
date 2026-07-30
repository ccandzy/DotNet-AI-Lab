using System.Collections.ObjectModel;
using AiChatClient.Models;
using AiChatClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using AiChatClient.Settings;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Specialized;

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
        private AIRole? _selectedRole;
        private bool _isRoleSwitchEnabled = true;
        private readonly ObservableCollection<AIRole> _roles = new();

        public MainViewModel(IChatService chatService, IConversationService conversationService, ILogger<MainViewModel> logger)
        {
            _chatService = chatService;
            _conversationService = conversationService;
            _logger = logger;

            Conversations = _conversationService.Conversations;

            // initialize built-in roles
            Roles = _roles;
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = "普通助手",
                SystemPrompt = "你是一个通用的助手，帮助用户回答问题、提供建议并生成示例代码或文本。遇到不确定的问题要说明不确定性，优先提供简洁清晰的回答。",
                Model = string.Empty,
                Temperature = 0.2,
                CreateTime = DateTime.Now
            });
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = ".NET架构师",
                SystemPrompt = "你是一个资深的 .NET 架构师。回答时重点关注系统设计、可扩展性、性能和安全性。对于架构建议给出替代方案和权衡，提供示例代码时遵循最新的 .NET 最佳实践和异步编程模式。",
                Model = string.Empty,
                Temperature = 0.1,
                CreateTime = DateTime.Now
            });
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = "WPF专家",
                SystemPrompt = "你是熟练的 WPF 专家，擅长数据绑定、命令、样式和性能调优。回答包含具体的 XAML 示例、控件布局和常见问题的解决方案，说明版本兼容性和最佳实践。",
                Model = string.Empty,
                Temperature = 0.2,
                CreateTime = DateTime.Now
            });
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = "代码审查专家",
                SystemPrompt = "你是一个资深的代码审查专家。阅读代码时关注可读性、可维护性、安全和性能问题。给出具体的改进建议、重构方式和示例修复代码，并提供风险说明。",
                Model = string.Empty,
                Temperature = 0.2,
                CreateTime = DateTime.Now
            });
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = "医疗设备专家",
                SystemPrompt = "你是医疗设备领域的专家。回答时遵守医疗相关的伦理和法规，明确区分一般性建议与专业医疗诊断。对设备设计、合规性和风险管理提供专业建议，并在必要时提示寻求专业医生或合规顾问。",
                Model = string.Empty,
                Temperature = 0.2,
                CreateTime = DateTime.Now
            });
            _roles.Add(new AIRole
            {
                Id = Guid.NewGuid(),
                Name = "英语老师",
                SystemPrompt = "# Role\r\n你是一名专业英语老师，负责帮助中文用户提升英语能力。\r\n\r\n# Teaching Style\r\n- 使用中文解释复杂语法\r\n- 给出英文例句\r\n- 主动纠正错误\r\n- 根据用户水平调整难度\r\n\r\n# Interaction\r\n每次回答：\r\n1. 先回答用户问题\r\n2. 再补充学习建议\r\n3. 必要时给练习题",
                Model = string.Empty,
                Temperature = 0.3,
                CreateTime = DateTime.Now
            });

            // ensure there is at least one conversation
            if (Conversations.Count == 0)
            {
                var c = _conversationService.CreateConversation();
                // associate default role
                c.Role = Roles.FirstOrDefault();
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
                    // when conversation changes, update selected role to match
                    SelectedRole = CurrentConversation?.Role ?? Roles.FirstOrDefault();
                    // subscribe to collection changes to control role switching
                    SubscribeMessagesChanged();
                    ClearCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<AIRole> Roles { get; }

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
