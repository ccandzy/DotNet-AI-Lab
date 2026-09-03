namespace AiChatClient.Models.Rag;

public sealed record VectorSearchResult(DocumentChunk Chunk, double Score);
