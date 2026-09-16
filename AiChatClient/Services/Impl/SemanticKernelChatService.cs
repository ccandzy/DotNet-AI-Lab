using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using AiChatClient.Dtos;
using AiChatClient.Services.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiChatClient.Services.Impl;

/// <summary>
/// Uses Semantic Kernel for DeepSeek streaming chat and automatic function calling.
/// </summary>
public sealed class SemanticKernelChatService : IChatService
{
    private readonly IKernelFactory _kernelFactory;
    private readonly ILogger<SemanticKernelChatService> _logger;

    internal SemanticKernelChatService(
        IKernelFactory kernelFactory,
        ILogger<SemanticKernelChatService>? logger = null)
    {
        _kernelFactory = kernelFactory;
        _logger = logger ?? NullLogger<SemanticKernelChatService>.Instance;
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
            _logger.LogDebug(
                "AI request started. Provider={Provider}, Model={Model}, " +
                "EnableTools={EnableTools}, RequireToolCall={RequireToolCall}, " +
                "MessageCount={MessageCount}, Temperature={Temperature}, " +
                "TopP={TopP}, MaxTokens={MaxTokens}",
                request.Provider,
                request.Model,
                request.EnableTools,
                request.RequireToolCall,
                request.Messages.Count,
                request.Settings.Temperature,
                request.Settings.TopP,
                request.Settings.MaxTokens);

            var kernel = _kernelFactory.CreateKernel(request, writer);
            var chatCompletionService =
                kernel.GetRequiredService<IChatCompletionService>();
            var history =
                SemanticKernelRequestMapper.CreateChatHistory(
                    request.Messages,
                    request.RequireToolCall);

            for (var index = 0; index < history.Count; index++)
            {
                var message = history[index];
                _logger.LogDebug(
                    "AI outbound message[{MessageIndex}]. Role={Role}, Content={Content}",
                    index,
                    message.Role,
                    LimitForLog(message.Content, 8_000));
            }

            var executionSettings =
                SemanticKernelRequestMapper.CreateExecutionSettings(
                    request.Settings,
                    request.Provider,
                    request.EnableTools,
                    request.RequireToolCall);
            var responseBuilder = new StringBuilder();

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

                responseBuilder.Append(content.Content);

                await writer.WriteAsync(
                    new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.TextDelta,
                        Content = content.Content
                    },
                    cancellationToken);
            }

            _logger.LogDebug(
                "AI response completed. Content={Content}",
                LimitForLog(responseBuilder.ToString(), 16_000));

            writer.TryComplete();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "AI request failed.");
            writer.TryComplete(exception);
        }
    }

    private static string LimitForLog(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maximumLength
            ? value
            : $"{value[..maximumLength]}... [truncated, total length: {value.Length}]";
    }
}
