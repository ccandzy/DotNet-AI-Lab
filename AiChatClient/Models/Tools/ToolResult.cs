namespace AiChatClient.Models.Tools;

/// <summary>
/// 表示一次工具调用的执行结果。
/// </summary>
public sealed class ToolResult
{
    /// <summary>
    /// 对应 <see cref="ToolCall.Id"/> 的调用标识。
    /// </summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>
    /// 返回给 AI 的结果文本。
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 指示工具调用是否以错误结束。
    /// </summary>
    public bool IsError { get; init; }
}
