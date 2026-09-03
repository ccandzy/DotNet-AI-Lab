using AiChatClient.Config;
using AiChatClient.Models.Rag;
using Microsoft.Extensions.Options;

namespace AiChatClient.Services.Rag;

/// <summary>
/// Coordinates Markdown loading, embedding, storage and query retrieval.
/// </summary>
public sealed class RagService : IRagService
{
    private readonly IDocumentLoader _documentLoader;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly RagOptions _options;

    public RagService(
        IDocumentLoader documentLoader,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IOptions<RagOptions> options)
    {
        _documentLoader = documentLoader;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _options = options.Value;
    }

    public async Task<RagImportResult> ImportMarkdownAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var chunks = await _documentLoader.LoadAsync(
            filePath,
            _options.MaxChunkCharacters,
            _options.OverlapCharacters,
            cancellationToken);

        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("Markdown 文件没有可向量化的文本内容。");
        }

        // Include the heading in the embedding input so section names also participate in retrieval.
        var embeddingInputs = chunks
            .Select(chunk => $"{chunk.HeadingPath}{Environment.NewLine}{chunk.Content}")
            .ToArray();
        var embeddings = await _embeddingService.GenerateAsync(
            embeddingInputs,
            cancellationToken);

        if (embeddings.Count != chunks.Count)
        {
            throw new InvalidOperationException(
                $"Chunk 数量与向量数量不一致：{chunks.Count} / {embeddings.Count}。");
        }

        var fullPath = Path.GetFullPath(filePath);
        var fileName = Path.GetFileName(fullPath);
        var embeddedChunks = chunks
            .Select((chunk, index) => new DocumentChunk(
                Id: $"{fullPath}#{chunk.Index}",
                FileName: fileName,
                ChunkIndex: chunk.Index,
                SourcePath: fullPath,
                HeadingPath: chunk.HeadingPath,
                StartLine: chunk.StartLine,
                EndLine: chunk.EndLine,
                Content: chunk.Content,
                Embedding: embeddings[index]))
            .ToArray();

        // Replace only after every embedding succeeded, keeping an older import intact on failure.
        _vectorStore.ReplaceDocument(fullPath, embeddedChunks);

        return new RagImportResult(fullPath, fileName, embeddedChunks.Length);
    }

    public async Task<IReadOnlyList<VectorSearchResult>> RetrieveAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question) || _vectorStore.Count == 0)
        {
            return Array.Empty<VectorSearchResult>();
        }

        var queryEmbeddings = await _embeddingService.GenerateAsync(
            [question],
            cancellationToken);

        if (queryEmbeddings.Count != 1)
        {
            throw new InvalidOperationException(
                $"查询应返回 1 个向量，实际返回 {queryEmbeddings.Count} 个。");
        }

        return _vectorStore.Search(
            queryEmbeddings[0],
            _options.TopK,
            _options.MinimumSimilarity);
    }
}
