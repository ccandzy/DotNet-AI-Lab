namespace AiChatClient.Config;

/// <summary>
/// AIChatClient 当前支持的聊天服务提供商名称。
/// </summary>
internal static class AIProviderNames
{
    public const string DeepSeek = "DeepSeek";

    public const string Ollama = "Ollama";

    public static bool IsSupported(string? providerName)
    {
        return string.Equals(
                   providerName,
                   DeepSeek,
                   StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   providerName,
                   Ollama,
                   StringComparison.OrdinalIgnoreCase);
    }
}
