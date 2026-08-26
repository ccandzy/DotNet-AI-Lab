namespace AiChatClient.Models.Tools;

/// <summary>
/// 描述可供 AI 调用的一项工具能力。
/// 这是 Provider 无关的声明，不包含工具的实际执行逻辑。
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>
    /// 工具的稳定标识，例如 <c>calculate</c>。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 提供给 AI 的工具用途说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 工具接受的参数定义。
    /// </summary>
    public IReadOnlyList<ToolParameterDefinition> Parameters { get; init; }
        = Array.Empty<ToolParameterDefinition>();
}
