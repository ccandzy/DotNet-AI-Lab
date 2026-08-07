using AiChatClient.Entities;

namespace Repositories;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessageEntity entity);

    Task<List<ChatMessageEntity>> GetByConversationIdAsync(Guid conversationId);
}