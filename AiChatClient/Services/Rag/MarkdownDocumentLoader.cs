using AiChatClient.Models.Rag;
using SemanticKernelDemo.Documents;

namespace AiChatClient.Services.Rag;

/// <summary>
/// Adapts the Markdown-aware chunker from SemanticKernelDemo to the application contract.
/// </summary>
public sealed class MarkdownDocumentLoader : IDocumentLoader
{
    public async Task<IReadOnlyList<DocumentTextChunk>> LoadAsync(
        string filePath,
        int maxChunkCharacters,
        int overlapCharacters,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                Path.GetExtension(filePath),
                ".md",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("第一阶段只支持 .md 文件。", nameof(filePath));
        }

        var chunks = await MarkdownChunker.ReadAndChunkMarkdownAsync(
            filePath,
            maxChunkCharacters,
            overlapCharacters,
            cancellationToken);

        return chunks
            .Select(chunk => new DocumentTextChunk(
                chunk.Index,
                chunk.SourcePath,
                chunk.HeadingPath,
                chunk.StartLine,
                chunk.EndLine,
                chunk.Content))
            .ToArray();
    }
}
