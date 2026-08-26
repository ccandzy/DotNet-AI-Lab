namespace AiChatClient.Dtos;

/// <summary>
/// 聊天流中发生的事件类型。
/// </summary>
public enum ChatStreamEventType
{
    TextDelta,
    ToolCalling
}

/// <summary>
/// 提供给界面的统一聊天流事件。
/// </summary>
public sealed class ChatStreamEvent
{
    public ChatStreamEventType Type { get; init; }

    public string Content { get; init; } = string.Empty;

    public string? ToolName { get; init; }
}
