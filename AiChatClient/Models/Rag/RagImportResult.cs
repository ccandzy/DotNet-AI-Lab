namespace AiChatClient.Models.Rag;

public sealed record RagImportResult(
    string SourcePath,
    string FileName,
    int ChunkCount);
