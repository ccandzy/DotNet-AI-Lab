using AiChatClient.Models.Tools;

namespace AiChatClient.Services.Tools;

/// <summary>
/// 定义一个可供 AI 调用的业务能力。
/// </summary>
public interface ITool
{
    /// <summary>
    /// 工具对外公开的名称、说明及参数约束。
    /// </summary>
    ToolDefinition Definition { get; }

    /// <summary>
    /// 执行一次工具调用，并返回与调用 ID 对应的结果。
    /// </summary>
    Task<ToolResult> ExecuteAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken = default);
}
