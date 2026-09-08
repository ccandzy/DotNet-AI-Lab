namespace AiChatClient.Models.Rag;

/// <summary>
/// Display-only snapshot describing a chunk supplied to the model.
/// </summary>
public sealed record RagSourceReference(
    string FileName,
    string SourcePath,
    string HeadingPath,
    int StartLine,
    int EndLine,
    double Similarity);
