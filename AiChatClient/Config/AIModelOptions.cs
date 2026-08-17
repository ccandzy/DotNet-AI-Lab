namespace AiChatClient.Config;

/// <summary>
/// AI 服务提供商下的模型配置节点。
/// </summary>
public class AIModelOptions
{
    public string Name { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
