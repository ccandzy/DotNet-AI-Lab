using AiChatClient.Models.Tools;
using Models;

namespace AiChatClient.Dtos;

/// <summary>
/// 一条发送给 AI Provider 的内部协议消息。
/// 支持普通聊天消息、assistant 工具调用和工具执行结果。
/// </summary>
public sealed class ChatRequestMessage
{
    public ChatRole Role { get; init; }

    public string? Content { get; init; }

    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = Array.Empty<ToolCall>();

    public string? ToolCallId { get; init; }
}
