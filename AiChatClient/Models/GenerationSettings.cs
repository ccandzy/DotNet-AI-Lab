namespace AiChatClient.Models;

/// <summary>
/// 表示一次 AI 生成请求的可选参数。
/// 未指定的参数由具体 AI Provider 使用其默认行为。
/// </summary>
public class GenerationSettings
{
    public double? Temperature { get; set; }

    public int? MaxTokens { get; set; }

    public double? TopP { get; set; }
}
