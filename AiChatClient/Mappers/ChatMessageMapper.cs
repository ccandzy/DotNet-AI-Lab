using AiChatClient.Entities;
using AiChatClient.Models;
using AiChatClient.Models.Rag;
using System.Text.Json;
using Models;

namespace AiChatClient.Mappers;

/// <summary>
/// ChatMessageEntity <-> Models.ChatMessage 映射
/// </summary>
public static class ChatMessageMapper
{
    public static ChatMessage ToModel(ChatMessageEntity entity)
    {
        var role = entity.Role switch
        {
            "User" => ChatRole.User,
            "Assistant" => ChatRole.Assistant,
            "System" => ChatRole.System,
            _ => ChatRole.User,
        };

        return new ChatMessage(role, entity.Content, entity.Timestamp)
        {
            Sources = DeserializeSources(entity.SourcesJson),
            RagWasEnabled = entity.RagWasEnabled
        };
    }

    public static ChatMessageEntity ToEntity(ChatMessage model, Guid conversationId)
    {
        return new ChatMessageEntity
        {
            Id = Guid.NewGuid(),
            ConversationId =conversationId,
            Role = model.Role switch
            {
                ChatRole.User => "User",
                ChatRole.Assistant => "Assistant",
                ChatRole.System => "System",
                _ => "User",
            },
            Content = model.Content,
            SourcesJson = model.Sources.Count == 0
                ? null
                : JsonSerializer.Serialize(model.Sources),
            RagWasEnabled = model.RagWasEnabled,
            Timestamp = model.Timestamp,
        };
    }

    private static IReadOnlyList<RagSourceReference> DeserializeSources(string? sourcesJson)
    {
        if (string.IsNullOrWhiteSpace(sourcesJson))
        {
            return Array.Empty<RagSourceReference>();
        }

        try
        {
            return JsonSerializer.Deserialize<RagSourceReference[]>(sourcesJson)
                ?? Array.Empty<RagSourceReference>();
        }
        catch (JsonException)
        {
            // Keep historical messages readable if citation metadata is damaged.
            return Array.Empty<RagSourceReference>();
        }
    }
}
