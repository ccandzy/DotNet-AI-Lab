using System.Threading.Channels;
using AiChatClient.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;

namespace AiChatClient.Services.SemanticKernel;

internal sealed class ToolInvocationEventFilter
    : IAutoFunctionInvocationFilter
{
    internal const int MaximumToolRounds = 8;

    private readonly ChannelWriter<ChatStreamEvent> _eventWriter;
    private readonly ILogger<ToolInvocationEventFilter> _logger;

    public ToolInvocationEventFilter(
        ChannelWriter<ChatStreamEvent> eventWriter,
        ILogger<ToolInvocationEventFilter>? logger = null)
    {
        _eventWriter = eventWriter;
        _logger = logger ?? NullLogger<ToolInvocationEventFilter>.Instance;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        EnsureWithinToolRoundLimit(context.RequestSequenceIndex);

        var toolName = string.IsNullOrWhiteSpace(context.Function.PluginName)
            ? context.Function.Name
            : $"{context.Function.PluginName}.{context.Function.Name}";
        var arguments = context.Arguments is null
            ? "<none>"
            : string.Join(
                ", ",
                context.Arguments.Select(argument =>
                    $"{argument.Key}={FormatForLog(argument.Value)}"));

        _logger.LogDebug(
            "SK tool call started. Tool={ToolName}, RequestSequence={RequestSequence}, " +
            "FunctionSequence={FunctionSequence}, Arguments={Arguments}",
            toolName,
            context.RequestSequenceIndex,
            context.FunctionSequenceIndex,
            arguments);

        await _eventWriter.WriteAsync(
            new ChatStreamEvent
            {
                Type = ChatStreamEventType.ToolCalling,
                ToolName = context.Function.Name
            },
            context.CancellationToken);

        try
        {
            await next(context);
            _logger.LogDebug(
                "SK tool call completed. Tool={ToolName}, Result={Result}",
                toolName,
                FormatForLog(context.Result));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "SK tool call failed. Tool={ToolName}, Arguments={Arguments}",
                toolName,
                arguments);
            throw;
        }
    }

    internal static void EnsureWithinToolRoundLimit(int requestSequenceIndex)
    {
        if (requestSequenceIndex >= MaximumToolRounds)
        {
            throw new InvalidOperationException(
                $"工具调用超过最大子轮次数 {MaximumToolRounds}。");
        }
    }

    private static string FormatForLog(object? value)
    {
        var text = value?.ToString() ?? "<null>";
        const int maximumLength = 8_000;

        return text.Length <= maximumLength
            ? text
            : $"{text[..maximumLength]}... [truncated, total length: {text.Length}]";
    }
}
