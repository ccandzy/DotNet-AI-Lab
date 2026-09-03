using AiChatClient.Models.Rag;

namespace AiChatClient.Services.Rag;

public interface IDocumentLoader
{
    Task<IReadOnlyList<DocumentTextChunk>> LoadAsync(
        string filePath,
        int maxChunkCharacters,
        int overlapCharacters,
        CancellationToken cancellationToken = default);
}
