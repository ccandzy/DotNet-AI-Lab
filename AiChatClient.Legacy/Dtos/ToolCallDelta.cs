namespace AiChatClient.Dtos;

/// <summary>
/// 流式响应中一个尚未完整的工具调用片段。
/// </summary>
public sealed class ToolCallDelta
{
    public int Index { get; init; }

    public string? Id { get; init; }

    public string? Name { get; init; }

    public string? ArgumentsFragment { get; init; }
}
