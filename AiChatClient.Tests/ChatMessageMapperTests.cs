using AiChatClient.Entities;
using AiChatClient.Mappers;
using AiChatClient.Models;
using AiChatClient.Models.Rag;
using Models;

namespace AiChatClient.Tests;

public sealed class ChatMessageMapperTests
{
    [Fact]
    public void Mapper_RoundTripsRagSources()
    {
        var conversationId = Guid.NewGuid();
        var message = new ChatMessage(ChatRole.Assistant, "answer", DateTime.UtcNow)
        {
            RagWasEnabled = true,
            Sources =
            [
                new RagSourceReference(
                    "knowledge.md",
                    Path.GetFullPath("knowledge.md"),
                    "Protocol > Frame",
                    120,
                    145,
                    0.82)
            ]
        };

        var entity = ChatMessageMapper.ToEntity(message, conversationId);
        var restored = ChatMessageMapper.ToModel(entity);

        var source = Assert.Single(restored.Sources);
        Assert.Equal("knowledge.md", source.FileName);
        Assert.Equal("Protocol > Frame", source.HeadingPath);
        Assert.Equal(120, source.StartLine);
        Assert.Equal(145, source.EndLine);
        Assert.Equal(0.82, source.Similarity);
        Assert.True(restored.RagWasEnabled);
    }

    [Fact]
    public void Mapper_InvalidSourcesJsonFallsBackToEmptySources()
    {
        var entity = new ChatMessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId = Guid.NewGuid(),
            Role = "Assistant",
            Content = "answer",
            SourcesJson = "not-json",
            Timestamp = DateTime.UtcNow
        };

        var restored = ChatMessageMapper.ToModel(entity);

        Assert.Empty(restored.Sources);
        Assert.False(restored.RagWasEnabled);
    }
}
