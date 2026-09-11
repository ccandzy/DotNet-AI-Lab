using AiChatClient.Config;
using AiChatClient.Models.Rag;
using AiChatClient.Services.Rag;
using Microsoft.Extensions.Options;

namespace AiChatClient.Tests;

public sealed class RagStabilityTests
{
    [Fact]
    public async Task ImportTimeout_PreservesOldIndexAndLateCompletionCannotOverwrite()
    {
        var f = new Fixture();
        var importing = f.Service.ReplaceDocumentAsync(f.Path);
        await Assert.ThrowsAsync<TimeoutException>(() => importing);
        f.Embedding.Completion.SetResult([new float[] { 0, 1 }]);
        Assert.Equal("old", f.Store.Search(new float[] { 1, 0 }, 1, 0).Single().Chunk.Content);
        Assert.Equal(1, f.Store.Count);
    }

    [Fact]
    public async Task ImportCancellation_PreservesOldIndex()
    {
        var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var importing = f.Service.ReplaceDocumentAsync(f.Path, cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => importing);
        Assert.True(f.Service.IsDocumentIndexed(f.Path));
        Assert.Equal("old", f.Store.Search(new float[] { 1, 0 }, 1, 0).Single().Chunk.Content);
    }

    [Fact]
    public async Task RetrievalTimeout_IsDistinctFromUserCancellation()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<TimeoutException>(() => f.Service.RetrieveAsync("question"));
        using var cancellation = new CancellationTokenSource();
        var retrieving = f.Service.RetrieveAsync("question", cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retrieving);
    }

    private sealed class Fixture
    {
        public string Path = System.IO.Path.GetFullPath("demo.md");
        public InMemoryVectorStore Store = new();
        public DelayedEmbedding Embedding = new();
        public RagService Service;
        public Fixture()
        {
            Store.ReplaceDocument(Path, [new DocumentChunk("old", "demo.md", 0, Path, "heading", 1, 1, "old", new float[] { 1, 0 })]);
            Service = new RagService(new Loader(), Embedding, Store,
                Options.Create(new RagOptions { ImportTimeoutSeconds = 1, RetrievalTimeoutSeconds = 1 }));
        }
    }
    private sealed class Loader : IDocumentLoader
    {
        public Task<IReadOnlyList<DocumentTextChunk>> LoadAsync(string path, int max, int overlap, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DocumentTextChunk>>([new DocumentTextChunk(0, path, "heading", 1, 1, "new")]);
    }
    private sealed class DelayedEmbedding : IEmbeddingService
    {
        // Deliberately ignores cancellation to exercise timeout protection against a late provider.
        public TaskCompletionSource<IReadOnlyList<ReadOnlyMemory<float>>> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(IReadOnlyList<string> input, CancellationToken ct = default) => Completion.Task;
    }
}
