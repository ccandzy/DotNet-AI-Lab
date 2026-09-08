using AiChatClient.Models.Rag;

namespace AiChatClient.Services.Rag;

public interface IVectorStore
{
    int Count { get; }

    void ReplaceDocument(
        string sourcePath,
        IReadOnlyCollection<DocumentChunk> chunks);

    int DeleteDocument(string sourcePath);

    bool ContainsDocument(string sourcePath);

    IReadOnlyList<VectorSearchResult> Search(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        double minimumSimilarity);
}
