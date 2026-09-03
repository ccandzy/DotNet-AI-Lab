namespace AiChatClient.Config;

/// <summary>
/// First-stage Markdown RAG tuning values.
/// </summary>
public sealed class RagOptions
{
    public int MaxChunkCharacters { get; set; } = 1200;

    public int OverlapCharacters { get; set; } = 120;

    public int TopK { get; set; } = 3;

    public double MinimumSimilarity { get; set; } = 0.45;
}
