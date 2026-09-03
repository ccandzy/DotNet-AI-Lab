using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiChatClient.Dtos;
using AiChatClient.Services.Impl;
using AiChatClient.Services.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiChatClient.Tests;

public sealed class SemanticKernelChatServiceTests
{
    [Fact]
    public async Task SendStreamingAsync_PreservesToolAndTextEventOrder()
    {
        var chatCompletionService = new ScriptedChatCompletionService(
            StreamText("A", "B"));
        var service = new SemanticKernelChatService(
            new ScriptedKernelFactory(
                chatCompletionService,
                emitToolEvent: true));

        var events = await ReadAllAsync(
            service.SendStreamingAsync(new ChatRequest()));

        Assert.Collection(
            events,
            item =>
            {
                Assert.Equal(ChatStreamEventType.ToolCalling, item.Type);
                Assert.Equal("calculate", item.ToolName);
            },
            item =>
            {
                Assert.Equal(ChatStreamEventType.TextDelta, item.Type);
                Assert.Equal("A", item.Content);
            },
            item =>
            {
                Assert.Equal(ChatStreamEventType.TextDelta, item.Type);
                Assert.Equal("B", item.Content);
            });
    }

    [Fact]
    public async Task SendStreamingAsync_PropagatesProviderFailure()
    {
        var service = new SemanticKernelChatService(
            new ScriptedKernelFactory(
                new ScriptedChatCompletionService(StreamFailure()),
                emitToolEvent: false));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReadAllAsync(
                service.SendStreamingAsync(new ChatRequest())));

        Assert.Equal("scripted failure", exception.Message);
    }

    [Fact]
    public async Task SendStreamingAsync_HonorsCancellation()
    {
        var service = new SemanticKernelChatService(
            new ScriptedKernelFactory(
                new ScriptedChatCompletionService(StreamUntilCancelled()),
                emitToolEvent: false));
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ReadAllAsync(
                service.SendStreamingAsync(
                    new ChatRequest(),
                    cancellation.Token)));
    }

    private static async Task<List<ChatStreamEvent>> ReadAllAsync(
        IAsyncEnumerable<ChatStreamEvent> source)
    {
        var results = new List<ChatStreamEvent>();

        await foreach (var item in source)
        {
            results.Add(item);
        }

        return results;
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamText(
        params string[] chunks)
    {
        foreach (var chunk in chunks)
        {
            await Task.Yield();
            yield return new StreamingChatMessageContent(
                AuthorRole.Assistant,
                chunk);
        }
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamFailure()
    {
        await Task.Yield();
        throw new InvalidOperationException("scripted failure");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }

    private static async IAsyncEnumerable<StreamingChatMessageContent> StreamUntilCancelled(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield break;
    }

    private sealed class ScriptedKernelFactory : IKernelFactory
    {
        private readonly IChatCompletionService _chatCompletionService;
        private readonly bool _emitToolEvent;

        public ScriptedKernelFactory(
            IChatCompletionService chatCompletionService,
            bool emitToolEvent)
        {
            _chatCompletionService = chatCompletionService;
            _emitToolEvent = emitToolEvent;
        }

        public Kernel CreateKernel(
            ChatRequest request,
            ChannelWriter<ChatStreamEvent> eventWriter)
        {
            if (_emitToolEvent)
            {
                eventWriter.TryWrite(
                    new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.ToolCalling,
                        ToolName = "calculate"
                    });
            }

            var builder = Kernel.CreateBuilder();
            builder.Services.AddSingleton(_chatCompletionService);
            return builder.Build();
        }
    }

    private sealed class ScriptedChatCompletionService
        : IChatCompletionService
    {
        private readonly IAsyncEnumerable<StreamingChatMessageContent> _stream;

        public ScriptedChatCompletionService(
            IAsyncEnumerable<StreamingChatMessageContent> stream)
        {
            _stream = stream;
        }

        public IReadOnlyDictionary<string, object?> Attributes { get; }
            = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public async IAsyncEnumerable<StreamingChatMessageContent>
            GetStreamingChatMessageContentsAsync(
                ChatHistory chatHistory,
                PromptExecutionSettings? executionSettings = null,
                Kernel? kernel = null,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in _stream.WithCancellation(cancellationToken))
            {
                yield return item;
            }
        }
    }
}
