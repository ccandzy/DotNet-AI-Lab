using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AiChatClient.Dtos;
using AiChatClient.Services.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiChatClient.Services.Impl;

/// <summary>
/// Uses Semantic Kernel for DeepSeek streaming chat and automatic function calling.
/// </summary>
public sealed class SemanticKernelChatService : IChatService
{
    private readonly IKernelFactory _kernelFactory;

    internal SemanticKernelChatService(IKernelFactory kernelFactory)
    {
        _kernelFactory = kernelFactory;
    }

    public async IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var channel = Channel.CreateUnbounded<ChatStreamEvent>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        var producer = ProduceEventsAsync(
            request,
            channel.Writer,
            cancellationToken);

        await foreach (var streamEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return streamEvent;
        }

        await producer;
    }

    private async Task ProduceEventsAsync(
        ChatRequest request,
        ChannelWriter<ChatStreamEvent> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            var kernel = _kernelFactory.CreateKernel(request, writer);
            var chatCompletionService =
                kernel.GetRequiredService<IChatCompletionService>();
            var history =
                SemanticKernelRequestMapper.CreateChatHistory(request.Messages);
            var executionSettings =
                SemanticKernelRequestMapper.CreateExecutionSettings(
                    request.Settings,
                    request.Provider);

            await foreach (var content in chatCompletionService
                               .GetStreamingChatMessageContentsAsync(
                                   history,
                                   executionSettings,
                                   kernel,
                                   cancellationToken))
            {
                if (string.IsNullOrEmpty(content.Content))
                {
                    continue;
                }

                await writer.WriteAsync(
                    new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.TextDelta,
                        Content = content.Content
                    },
                    cancellationToken);
            }

            writer.TryComplete();
        }
        catch (Exception exception)
        {
            writer.TryComplete(exception);
        }
    }
}
