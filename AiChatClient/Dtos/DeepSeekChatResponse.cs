using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiChatClient.Dtos;

/// <summary>
/// DeepSeek 流式响应中的一个 SSE 数据块。
/// </summary>
public sealed class DeepSeekChatResponse
{
    [JsonPropertyName("choices")]
    public List<DeepSeekChoice> Choices { get; set; } = new();
}

public sealed class DeepSeekChoice
{
    [JsonPropertyName("delta")]
    public DeepSeekDelta Delta { get; set; } = new();

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public sealed class DeepSeekDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
