namespace AiChatClient.Models.Tools;

/// <summary>
/// 表示 AI 请求执行一次工具调用。
/// </summary>
public sealed class ToolCall
{
    /// <summary>
    /// 本次调用的唯一关联标识，用于将结果对应回原始调用。
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 被调用工具的名称。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 调用参数的原始 JSON 对象文本。
    /// 参数尚未在此模型中解析或执行。
    /// </summary>
    public string ArgumentsJson { get; init; } = "{}";
}
