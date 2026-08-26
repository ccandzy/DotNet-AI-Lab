using System.Threading.Channels;
using AiChatClient.Dtos;
using Microsoft.SemanticKernel;

namespace AiChatClient.Services.SemanticKernel;

internal sealed class ToolInvocationEventFilter
    : IAutoFunctionInvocationFilter
{
    internal const int MaximumToolRounds = 8;

    private readonly ChannelWriter<ChatStreamEvent> _eventWriter;

    public ToolInvocationEventFilter(
        ChannelWriter<ChatStreamEvent> eventWriter)
    {
        _eventWriter = eventWriter;
    }

    public async Task OnAutoFunctionInvocationAsync(
        AutoFunctionInvocationContext context,
        Func<AutoFunctionInvocationContext, Task> next)
    {
        EnsureWithinToolRoundLimit(context.RequestSequenceIndex);

        await _eventWriter.WriteAsync(
            new ChatStreamEvent
            {
                Type = ChatStreamEventType.ToolCalling,
                ToolName = context.Function.Name
            },
            context.CancellationToken);

        await next(context);
    }

    internal static void EnsureWithinToolRoundLimit(int requestSequenceIndex)
    {
        if (requestSequenceIndex >= MaximumToolRounds)
        {
            throw new InvalidOperationException(
                $"工具调用超过最大子轮次数 {MaximumToolRounds}。");
        }
    }
}
