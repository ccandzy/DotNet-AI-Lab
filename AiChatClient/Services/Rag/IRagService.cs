using AiChatClient.Models.Rag;

namespace AiChatClient.Services.Rag;

public interface IRagService
{
    Task<RagImportResult> ReplaceDocumentAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    int DeleteDocument(string sourcePath);

    bool IsDocumentIndexed(string sourcePath);

    Task<IReadOnlyList<VectorSearchResult>> RetrieveAsync(
        string question,
        CancellationToken cancellationToken = default);
}
