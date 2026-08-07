using AiChatClient.Entities;
using AiChatClient.Models;

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

        return new ChatMessage(role, entity.Content, entity.Timestamp);
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
            Timestamp = model.Timestamp,
        };
    }
}
