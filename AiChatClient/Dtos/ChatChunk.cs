namespace AiChatClient.Dtos;

/// <summary>
/// Provider 流式响应中的一个统一数据块。
/// </summary>
public sealed class ChatChunk
{
    public string Content { get; init; } = string.Empty;

    public IReadOnlyList<ToolCallDelta> ToolCallDeltas { get; init; } = Array.Empty<ToolCallDelta>();

    public ChatCompletionReason CompletionReason { get; init; }
}

/// <summary>
/// 一次聊天请求结束的原因。
/// </summary>
public enum ChatCompletionReason
{
    None,
    Stop,
    ToolCalls
}
