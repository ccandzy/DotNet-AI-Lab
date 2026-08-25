namespace AiChatClient.Models.Tools;

/// <summary>
/// 描述工具的一个输入参数。
/// </summary>
public sealed class ToolParameterDefinition
{
    /// <summary>
    /// 参数名称，例如 <c>expression</c>。
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 提供给 AI 的参数说明。
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 参数类型，采用 JSON Schema 基本类型名称，例如 string、number、integer、boolean 或 object。
    /// </summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>
    /// 指示调用工具时是否必须提供该参数。
    /// </summary>
    public bool IsRequired { get; init; }
}
