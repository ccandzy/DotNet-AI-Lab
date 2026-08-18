using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiChatClient.Dtos;

/// <summary>
/// DeepSeek Chat Completions API 的请求格式。
/// </summary>
public sealed class DeepSeekChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ModelChatMessage> Messages { get; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("temperature")]
    public double Temperature { get; set; }
}
