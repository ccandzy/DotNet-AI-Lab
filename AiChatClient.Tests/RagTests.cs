using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Models.Rag;
using AiChatClient.Services.Rag;
using Microsoft.Extensions.Options;
using Models;

namespace AiChatClient.Tests;

public sealed class RagTests
{
    [Fact]
    public async Task MarkdownLoader_PreservesHeadingSourceAndOriginalText()
    {
        var filePath = Path.Combine(
            Path.GetTempPath(),
            $"rag-{Guid.NewGuid():N}.md");

        try
        {
            await File.WriteAllTextAsync(
                filePath,
                "# Channels\n\n## Channel<T>\n\nChannel 是异步生产者/消费者队列。");

            var chunks = await new MarkdownDocumentLoader().LoadAsync(
                filePath,
                maxChunkCharacters: 300,
                overlapCharacters: 50);

            Assert.NotEmpty(chunks);
            Assert.All(chunks, chunk => Assert.Equal(Path.GetFullPath(filePath), chunk.SourcePath));
            Assert.Contains(chunks, chunk =>
                chunk.HeadingPath.Contains("Channel<T>")
                && chunk.Content.Contains("异步生产者/消费者队列"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void VectorStore_Search_ReturnsTopKAboveThresholdInScoreOrder()
    {
        var store = new InMemoryVectorStore();
        var sourcePath = Path.Combine(Path.GetTempPath(), "knowledge.md");
        store.ReplaceDocument(sourcePath,
        [
            CreateChunk(sourcePath, 1, "most relevant", new float[] { 1f, 0f }),
            CreateChunk(sourcePath, 2, "second", new float[] { 0.8f, 0.2f }),
            CreateChunk(sourcePath, 3, "unrelated", new float[] { 0f, 1f })
        ]);

        var results = store.Search(
            new float[] { 1f, 0f },
            topK: 2,
            minimumSimilarity: 0.5);

        Assert.Equal(2, results.Count);
        Assert.Equal("most relevant", results[0].Chunk.Content);
        Assert.Equal("second", results[1].Chunk.Content);
        Assert.True(results[0].Score >= results[1].Score);
    }

    [Fact]
    public void PromptComposer_ChangesOnlyInternalLatestUserMessage()
    {
        IReadOnlyList<ChatRequestMessage> messages =
        [
            new() { Role = ChatRole.System, Content = "system" },
            new() { Role = ChatRole.User, Content = "Channel<T> 是什么？" }
        ];
        var sourcePath = Path.Combine(Path.GetTempPath(), "channels.md");
        IReadOnlyList<VectorSearchResult> results =
        [
            new(
                CreateChunk(
                    sourcePath,
                    1,
                    "Channel 是异步生产者/消费者队列。",
                    new float[] { 1f, 0f }),
                0.91)
        ];

        var augmented = RagPromptComposer.AddContextToLatestUserMessage(
            messages,
            "Channel<T> 是什么？",
            results);

        Assert.Equal("Channel<T> 是什么？", messages[1].Content);
        Assert.Equal("system", augmented[0].Content);
        Assert.Contains("Reference Context:", augmented[1].Content);
        Assert.Contains("Channel 是异步生产者/消费者队列。", augmented[1].Content);
        Assert.EndsWith("Channel<T> 是什么？", augmented[1].Content);
    }

    [Fact]
    public void PromptComposer_NoResults_LeavesNormalChatRequestUnchanged()
    {
        IReadOnlyList<ChatRequestMessage> messages =
        [
            new() { Role = ChatRole.User, Content = "内部约定是什么？" }
        ];

        var augmented = RagPromptComposer.AddContextToLatestUserMessage(
            messages,
            "内部约定是什么？",
            []);

        Assert.Same(messages, augmented);
        Assert.Equal("内部约定是什么？", augmented[0].Content);
    }

    [Fact]
    public async Task RagService_ImportsThenEmbedsQueryAndRetrievesOriginalText()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "channels.md");
        var loader = new StubDocumentLoader(
        [
            new DocumentTextChunk(
                1,
                sourcePath,
                "Channels > 基础",
                3,
                8,
                "Channel 是异步生产者/消费者队列。")
        ]);
        var embedding = new StubEmbeddingService(new float[] { 1f, 0f });
        var store = new InMemoryVectorStore();
        var service = new RagService(
            loader,
            embedding,
            store,
            Options.Create(new RagOptions
            {
                TopK = 3,
                MinimumSimilarity = 0.5
            }));

        var import = await service.ReplaceDocumentAsync(sourcePath);
        var results = await service.RetrieveAsync("Channel<T> 是什么？");

        Assert.Equal(1, import.ChunkCount);
        Assert.Equal(1, store.Count);
        Assert.Single(results);
        Assert.Equal("Channel 是异步生产者/消费者队列。", results[0].Chunk.Content);
        Assert.Equal(2, embedding.CallCount);
        Assert.Contains("Channels > 基础", embedding.InputBatches[0][0]);
        Assert.Equal("Channel<T> 是什么？", embedding.InputBatches[1][0]);
    }

    [Fact]
    public void VectorStore_DeleteDocument_RemovesOnlyMatchingSourcePath()
    {
        var store = new InMemoryVectorStore();
        var firstPath = Path.Combine(Path.GetTempPath(), "first.md");
        var secondPath = Path.Combine(Path.GetTempPath(), "second.md");
        store.ReplaceDocument(firstPath,
        [
            CreateChunk(firstPath, 1, "first-1", new float[] { 1f, 0f }),
            CreateChunk(firstPath, 2, "first-2", new float[] { 1f, 0f })
        ]);
        store.ReplaceDocument(secondPath,
        [
            CreateChunk(secondPath, 1, "second", new float[] { 1f, 0f })
        ]);

        var removedCount = store.DeleteDocument(firstPath);
        var results = store.Search(new float[] { 1f, 0f }, 10, -1);

        Assert.Equal(2, removedCount);
        Assert.False(store.ContainsDocument(firstPath));
        Assert.True(store.ContainsDocument(secondPath));
        Assert.Single(results);
        Assert.Equal(Path.GetFullPath(secondPath), results[0].Chunk.SourcePath);
    }

    [Fact]
    public async Task RagService_FailedReplacement_PreservesPreviousDocument()
    {
        var sourcePath = Path.Combine(Path.GetTempPath(), "replacement.md");
        var store = new InMemoryVectorStore();
        store.ReplaceDocument(sourcePath,
        [
            CreateChunk(sourcePath, 1, "original", new float[] { 1f, 0f })
        ]);
        var service = new RagService(
            new StubDocumentLoader(
            [
                new DocumentTextChunk(1, sourcePath, "Heading", 1, 1, "replacement")
            ]),
            new ThrowingEmbeddingService(),
            store,
            Options.Create(new RagOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ReplaceDocumentAsync(sourcePath));

        var results = store.Search(new float[] { 1f, 0f }, 10, -1);
        Assert.Single(results);
        Assert.Equal("original", results[0].Chunk.Content);
    }

    [Fact]
    public async Task RagService_EmptyStore_DoesNotCallEmbeddingService()
    {
        var embedding = new StubEmbeddingService(new float[] { 1f, 0f });
        var service = new RagService(
            new StubDocumentLoader([]),
            embedding,
            new InMemoryVectorStore(),
            Options.Create(new RagOptions()));

        var results = await service.RetrieveAsync("ordinary question");

        Assert.Empty(results);
        Assert.Equal(0, embedding.CallCount);
    }

    private static DocumentChunk CreateChunk(
        string sourcePath,
        int index,
        string content,
        ReadOnlyMemory<float> embedding)
    {
        return new DocumentChunk(
            $"{sourcePath}#{index}",
            Path.GetFileName(sourcePath),
            index,
            sourcePath,
            "Heading",
            index,
            index,
            content,
            embedding);
    }

    private sealed class StubDocumentLoader : IDocumentLoader
    {
        private readonly IReadOnlyList<DocumentTextChunk> _chunks;

        public StubDocumentLoader(IReadOnlyList<DocumentTextChunk> chunks)
        {
            _chunks = chunks;
        }

        public Task<IReadOnlyList<DocumentTextChunk>> LoadAsync(
            string filePath,
            int maxChunkCharacters,
            int overlapCharacters,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_chunks);
        }
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        private readonly ReadOnlyMemory<float> _embedding;

        public StubEmbeddingService(ReadOnlyMemory<float> embedding)
        {
            _embedding = embedding;
        }

        public int CallCount { get; private set; }

        public List<IReadOnlyList<string>> InputBatches { get; } = new();

        public Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            InputBatches.Add(inputs.ToArray());
            IReadOnlyList<ReadOnlyMemory<float>> result = inputs
                .Select(_ => _embedding)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingEmbeddingService : IEmbeddingService
    {
        public Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
            IReadOnlyList<string> inputs,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("embedding failed");
        }
    }
}
