namespace AiChatClient.Models.Rag;

/// <summary>
/// A Markdown fragment before embedding is generated.
/// </summary>
public sealed record DocumentTextChunk(
    int Index,
    string SourcePath,
    string HeadingPath,
    int StartLine,
    int EndLine,
    string Content);
