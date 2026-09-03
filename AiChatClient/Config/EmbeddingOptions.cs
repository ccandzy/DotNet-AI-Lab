namespace AiChatClient.Config;

/// <summary>
/// OpenAI-compatible embedding endpoint used by the local RAG pipeline.
/// </summary>
public sealed class EmbeddingOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
