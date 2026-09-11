using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Models;
using AiChatClient.Models.Rag;
using AiChatClient.Services;
using AiChatClient.Services.Mcp;
using AiChatClient.Services.Rag;
using AiChatClient.Settings;
using AiChatClient.ViewModels;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;
using Services;

namespace AiChatClient.Tests;

public class MainViewModelStabilityTests
{
    [Fact]
    public async Task DeleteLastConversation_DisablesSendAndNewConversationRecovers()
    {
        var f = await Fixture.Create();
        var notifications = new List<string?>();
        f.Vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);
        await f.Vm.DeleteConversationCommand.ExecuteAsync(null);
        Assert.Contains(nameof(MainViewModel.CurrentConversation), notifications);
        f.Vm.CurrentInput = "hello";
        Assert.False(f.Vm.SendCommand.CanExecute(null));
        await f.Vm.SendCommand.ExecuteAsync(null); // Also guard calls bypassing CanExecute.
        Assert.Empty(f.Chat.Requests);
        Assert.Contains("新建", f.Vm.StatusMessage);
        await ((IAsyncRelayCommand)f.Vm.NewConversationCommand).ExecuteAsync(null);
        Assert.True(f.Vm.SendCommand.CanExecute(null));
    }

    [Fact]
    public async Task NoSelectedModel_DisablesSend()
    {
        var f = await Fixture.Create();
        f.Vm.SelectedAIModel = null;
        Assert.False(f.Vm.SendCommand.CanExecute(null));
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.Empty(f.Messages.Saved);
    }

    [Fact]
    public async Task Streaming_LocksConversationAndIgnoresDuplicateSend()
    {
        var f = await Fixture.Create();
        var origin = f.Vm.CurrentConversation!;
        var other = new Conversation { Id = Guid.NewGuid(), Role = origin.Role };
        f.Conversations.Conversations.Add(other);
        f.Chat.Gate = new TaskCompletionSource();
        var sending = f.Vm.SendCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsBusy);
        Assert.False(f.Vm.CanManageConversations);
        f.Vm.CurrentConversation = other;
        await f.Vm.DeleteConversationCommand.ExecuteAsync(null);
        await f.Vm.ClearCommand.ExecuteAsync(null);
        await ((IAsyncRelayCommand)f.Vm.NewConversationCommand).ExecuteAsync(null);
        f.Vm.CurrentInput = "duplicate";
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.Same(origin, f.Vm.CurrentConversation);
        Assert.Single(f.Chat.Requests);
        f.Chat.Gate.SetResult();
        await sending;
        Assert.All(f.Messages.Saved, item => Assert.Equal(origin.Id, item.Id));
        Assert.Empty(other.Messages);
        Assert.False(f.Vm.IsBusy);
        Assert.Equal("duplicate", f.Vm.CurrentInput);
    }

    [Fact]
    public async Task UserSaveFailure_KeepsInputAndDoesNotCallModel()
    {
        var f = await Fixture.Create();
        f.Messages.FailRole = ChatRole.User;
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.Equal("hello", f.Vm.CurrentInput);
        Assert.Empty(f.Chat.Requests);
        Assert.Empty(f.Vm.Messages);
        Assert.False(f.Vm.IsBusy);
        Assert.Single(f.Dialog.Errors);
        f.Messages.FailRole = null;
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.Single(f.Chat.Requests);
    }

    [Fact]
    public async Task SaveInFlight_LocksBeforeFirstAwait()
    {
        var f = await Fixture.Create();
        f.Messages.Gate = new TaskCompletionSource();
        var sending = f.Vm.SendCommand.ExecuteAsync(null);
        Assert.True(f.Vm.IsBusy);
        await f.Vm.SendCommand.ExecuteAsync(null);
        await f.Vm.DeleteConversationCommand.ExecuteAsync(null);
        Assert.Single(f.Conversations.Conversations);
        f.Messages.Gate.SetResult();
        await sending;
        Assert.Single(f.Chat.Requests);
    }

    [Fact]
    public async Task AssistantSaveFailure_PreservesAnswerAndExcludesItFromNextRequest()
    {
        var f = await Fixture.Create();
        f.Messages.FailRole = ChatRole.Assistant;
        await f.Vm.SendCommand.ExecuteAsync(null);
        var answer = f.Vm.Messages.Last();
        Assert.True(answer.IsTransient);
        Assert.Contains("answer", answer.Content);
        Assert.Contains("未保存", answer.Content);
        f.Messages.FailRole = null;
        f.Vm.CurrentInput = "again";
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.DoesNotContain(f.Chat.Requests.Last().Messages, m => m.Role == ChatRole.Assistant);
    }

    [Fact]
    public async Task StopThenSend_Recovers()
    {
        var f = await Fixture.Create();
        f.Chat.Gate = new TaskCompletionSource();
        var sending = f.Vm.SendCommand.ExecuteAsync(null);
        f.Vm.StopCommand.Execute(null);
        await sending;
        Assert.False(f.Vm.IsBusy);
        Assert.Contains("停止", f.Vm.StatusMessage);
        f.Chat.Gate = null;
        f.Vm.CurrentInput = "again";
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.False(f.Vm.Messages.Last().IsTransient);
    }

    [Theory]
    [InlineData("new")]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("clear")]
    public async Task DatabaseMutationFailure_PreservesUiAndRestoresCommands(string operation)
    {
        var f = await Fixture.Create();
        var origin = f.Vm.CurrentConversation!;
        origin.Messages.Add(new ChatMessage(ChatRole.User, "existing", DateTime.Now));
        f.Conversations.Fail = true;
        f.Messages.FailDelete = true;
        IAsyncRelayCommand command = operation switch
        {
            "new" => (IAsyncRelayCommand)f.Vm.NewConversationCommand,
            "delete" => f.Vm.DeleteConversationCommand,
            "rename" => f.Vm.RenameConversationCommand,
            _ => f.Vm.ClearCommand
        };
        await command.ExecuteAsync(null);
        Assert.Same(origin, f.Vm.CurrentConversation);
        Assert.Single(f.Vm.Messages);
        Assert.Single(f.Conversations.Conversations);
        Assert.Single(f.Dialog.Errors);
        Assert.True(f.Vm.CanManageConversations);
        f.Conversations.Fail = false;
        f.Messages.FailDelete = false;
        await command.ExecuteAsync(null);
        Assert.Single(f.Dialog.Errors);
    }

    [Theory]
    [InlineData(false, false, "未导入")]
    [InlineData(true, false, "未找到")]
    [InlineData(true, true, "检索失败")]
    public async Task RagFallback_IsVisible(bool indexed, bool fails, string expected)
    {
        var f = await Fixture.Create();
        f.Vm.IsRagEnabled = true;
        f.Rag.Indexed = indexed;
        f.Rag.Fail = fails;
        f.Vm.KnowledgeFiles.Add(new KnowledgeFileItem(Path.GetFullPath("demo.md")));
        await f.Vm.SendCommand.ExecuteAsync(null);
        Assert.Contains(expected, f.Vm.StatusMessage);
        Assert.Contains("普通聊天", f.Vm.StatusMessage);
        Assert.Single(f.Chat.Requests);
    }

    [Fact]
    public async Task ImportCancellation_RestoresButtonsAndKeepsOldIndex()
    {
        var f = await Fixture.Create();
        var file = Path.GetTempFileName();
        try
        {
            var item = new KnowledgeFileItem(file) { IsVectorized = true };
            f.Vm.KnowledgeFiles.Add(item);
            f.Rag.ImportGate = new TaskCompletionSource();
            var importing = f.Vm.RebuildKnowledgeFileCommand.ExecuteAsync(item);
            Assert.True(f.Vm.IsImportingKnowledge);
            f.Vm.CancelKnowledgeImportCommand.Execute(null);
            await importing;
            Assert.False(f.Vm.IsImportingKnowledge);
            Assert.True(item.IsVectorized);
            Assert.Contains("已取消", item.Status);
            Assert.True(f.Vm.AddKnowledgeFileCommand.CanExecute(null));
        }
        finally { File.Delete(file); }
    }

    private sealed class Fixture
    {
        public Chat Chat = new();
        public Messages Messages = new();
        public ConversationStore Conversations = new();
        public Dialog Dialog = new();
        public Rag Rag = new();
        public MainViewModel Vm = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var options = new AiOptions { Providers = [new AIProviderOptions
            {
                Name = "DeepSeek", Models = [new AIModelOptions { ModelId = "demo", Name = "demo", IsEnabled = true }]
            }] };
            f.Vm = new MainViewModel(f.Chat, f.Conversations, f.Messages, new Roles(),
                f.Dialog, NullLogger<MainViewModel>.Instance, new Monitor(options), f.Rag, new Mcp());
            await f.Vm.InitializeAsync();
            f.Vm.CurrentInput = "hello";
            return f;
        }
    }
    private sealed class Chat : IChatService
    {
        public List<ChatRequest> Requests = [];
        public TaskCompletionSource? Gate;
        public async IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken);
            yield return new ChatStreamEvent { Type = ChatStreamEventType.TextDelta, Content = "answer" };
        }
    }
    private sealed class Messages : IChatMessageService
    {
        public List<(Guid Id, ChatMessage Message)> Saved = [];
        public ChatRole? FailRole;
        public bool FailDelete;
        public TaskCompletionSource? Gate;
        public async Task AddMessageAsync(Guid id, ChatMessage message)
        {
            if (Gate is not null) await Gate.Task;
            if (message.Role == FailRole) throw new IOException("save failed");
            Saved.Add((id, message));
        }
        public Task<List<ChatMessage>> GetMessagesAsync(Guid id) => Task.FromResult(new List<ChatMessage>());
        public Task DeleteMessagesByConversationIdAsync(Guid id) => FailDelete
            ? Task.FromException(new IOException("delete failed")) : Task.CompletedTask;
    }
    private sealed class ConversationStore : IConversationService
    {
        public ObservableCollection<Conversation> Conversations { get; } = new();
        public bool Fail;
        private void Check() { if (Fail) throw new IOException("database failed"); }
        public Task InitializeAsync() => Task.CompletedTask;
        public Task<Conversation> CreateConversation(Conversation c) { Check(); Conversations.Add(c); return Task.FromResult(c); }
        public Task DeleteConversationAsync(Guid id) { Check(); Conversations.Remove(Conversations.Single(c => c.Id == id)); return Task.CompletedTask; }
        public Task<bool> RenameConversationAsync(Guid id, string title) { Check(); Conversations.Single(c => c.Id == id).Title = title; return Task.FromResult(true); }
        public Task<bool> UpdateConversationRoleAsync(Guid id, Guid role) => Task.FromResult(true);
        public Task<bool> UpdateConversationModelAsync(Guid id, string model) => Task.FromResult(true);
        public Task<bool> UpdateConversationConfigurationAsync(Guid id, GenerationSettings settings) => Task.FromResult(true);
    }
    private sealed class Roles : IAIRoleService
    {
        public Task<List<AIRole>> GetRolesAsync() => Task.FromResult(new List<AIRole> { new() { Id = Guid.NewGuid(), Name = "demo", SystemPrompt = "" } });
    }
    private sealed class Dialog : IDialogService
    {
        public List<string> Errors = [];
        public void ShowError(string title, string message) => Errors.Add(message);
        public string? ShowInputDialog(string title, string prompt, string defaultValue = "") => "renamed";
        public string? ShowMarkdownFileDialog() => null;
    }
    private sealed class Monitor(AiOptions value) : IOptionsMonitor<AiOptions>
    {
        public AiOptions CurrentValue => value;
        public AiOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<AiOptions, string?> listener) => null;
    }
    private sealed class Rag : IRagService
    {
        public bool Indexed, Fail;
        public TaskCompletionSource? ImportGate;
        public async Task<RagImportResult> ReplaceDocumentAsync(string path, CancellationToken ct = default)
        {
            if (ImportGate is not null) await ImportGate.Task.WaitAsync(ct);
            return new(path, Path.GetFileName(path), 1);
        }
        public int DeleteDocument(string path) => 0;
        public bool IsDocumentIndexed(string path) => Indexed;
        public Task<IReadOnlyList<VectorSearchResult>> RetrieveAsync(string question, CancellationToken ct = default) => Fail
            ? Task.FromException<IReadOnlyList<VectorSearchResult>>(new IOException("offline"))
            : Task.FromResult<IReadOnlyList<VectorSearchResult>>(Array.Empty<VectorSearchResult>());
    }
    private sealed class Mcp : IMcpClientService
    {
        public McpConnectionState State => McpConnectionState.Disconnected;
        public McpServerInfo? ServerInfo => null;
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<McpToolInfo>>(Array.Empty<McpToolInfo>());
        public Task<string> CallToolAsync(string name, CancellationToken ct = default) => Task.FromResult("");
        public void Dispose() { }
    }
}
