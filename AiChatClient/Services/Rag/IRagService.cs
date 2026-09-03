using AiChatClient.Models.Rag;

namespace AiChatClient.Services.Rag;

public interface IRagService
{
    Task<RagImportResult> ImportMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VectorSearchResult>> RetrieveAsync(
        string question,
        CancellationToken cancellationToken = default);
}
