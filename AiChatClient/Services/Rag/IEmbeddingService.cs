namespace AiChatClient.Services.Rag;

public interface IEmbeddingService
{
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default);
}
