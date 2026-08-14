namespace AiChatClient.Config;

/// <summary>
/// AI 服务提供商的配置节点。
/// </summary>
public class AIProviderOptions
{
    public string Name { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public List<AIModelOptions> Models { get; set; } = new();
}
