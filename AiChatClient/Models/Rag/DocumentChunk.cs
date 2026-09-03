namespace AiChatClient.Models.Rag;

/// <summary>
/// Original Markdown text and its vector kept together in memory.
/// </summary>
public sealed record DocumentChunk(
    string Id,
    string FileName,
    int ChunkIndex,
    string SourcePath,
    string HeadingPath,
    int StartLine,
    int EndLine,
    string Content,
    ReadOnlyMemory<float> Embedding);
