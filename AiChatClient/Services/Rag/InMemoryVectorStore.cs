using AiChatClient.Models.Rag;

namespace AiChatClient.Services.Rag;

/// <summary>
/// Process-local, exhaustive cosine-similarity vector store.
/// </summary>
public sealed class InMemoryVectorStore : IVectorStore
{
    private readonly object _syncRoot = new();
    private DocumentChunk[] _chunks = Array.Empty<DocumentChunk>();

    public int Count
    {
        get
        {
            lock (_syncRoot)
            {
                return _chunks.Length;
            }
        }
    }

    public void ReplaceDocument(
        string sourcePath,
        IReadOnlyCollection<DocumentChunk> chunks)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(chunks);

        var fullPath = Path.GetFullPath(sourcePath);
        var replacements = chunks.ToArray();

        if (replacements.Any(chunk => !string.Equals(
                Path.GetFullPath(chunk.SourcePath),
                fullPath,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "替换集合包含来自其他文档的 Chunk。",
                nameof(chunks));
        }

        lock (_syncRoot)
        {
            _chunks = _chunks
                .Where(chunk => !string.Equals(
                    Path.GetFullPath(chunk.SourcePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                .Concat(replacements)
                .ToArray();
        }
    }

    public int DeleteDocument(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        lock (_syncRoot)
        {
            var retained = _chunks
                .Where(chunk => !string.Equals(
                    Path.GetFullPath(chunk.SourcePath),
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var removedCount = _chunks.Length - retained.Length;
            _chunks = retained;
            return removedCount;
        }
    }

    public bool ContainsDocument(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        lock (_syncRoot)
        {
            return _chunks.Any(chunk => string.Equals(
                Path.GetFullPath(chunk.SourcePath),
                fullPath,
                StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<VectorSearchResult> Search(
        ReadOnlyMemory<float> queryEmbedding,
        int topK,
        double minimumSimilarity)
    {
        if (queryEmbedding.IsEmpty)
        {
            throw new ArgumentException("查询向量不能为空。", nameof(queryEmbedding));
        }

        if (topK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), "TopK 必须大于 0。");
        }

        if (minimumSimilarity is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumSimilarity),
                "相似度阈值必须介于 -1 和 1 之间。");
        }

        DocumentChunk[] snapshot;
        lock (_syncRoot)
        {
            snapshot = _chunks;
        }

        return snapshot
            .Select(chunk => new VectorSearchResult(
                chunk,
                CosineSimilarity(queryEmbedding.Span, chunk.Embedding.Span)))
            .Where(result => result.Score >= minimumSimilarity)
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToArray();
    }

    internal static double CosineSimilarity(
        ReadOnlySpan<float> left,
        ReadOnlySpan<float> right)
    {
        if (left.IsEmpty || right.IsEmpty)
        {
            throw new ArgumentException("参与余弦相似度计算的向量不能为空。");
        }

        if (left.Length != right.Length)
        {
            throw new ArgumentException(
                $"向量维度不一致：左侧 {left.Length}，右侧 {right.Length}。");
        }

        double dotProduct = 0;
        double leftSquaredMagnitude = 0;
        double rightSquaredMagnitude = 0;

        for (var index = 0; index < left.Length; index++)
        {
            dotProduct += left[index] * right[index];
            leftSquaredMagnitude += left[index] * left[index];
            rightSquaredMagnitude += right[index] * right[index];
        }

        if (leftSquaredMagnitude == 0 || rightSquaredMagnitude == 0)
        {
            throw new ArgumentException("零向量不能用于余弦相似度计算。");
        }

        return dotProduct /
            (Math.Sqrt(leftSquaredMagnitude) * Math.Sqrt(rightSquaredMagnitude));
    }
}
