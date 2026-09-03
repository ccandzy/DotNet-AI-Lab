using AiChatClient.Entities;

namespace Repositories;

public interface IChatMessageRepository
{
    Task AddAsync(ChatMessageEntity entity);

    Task<List<ChatMessageEntity>> GetByConversationIdAsync(Guid conversationId);

    /// <summary>
    /// 删除指定会话下的全部聊天消息。
    /// </summary>
    Task DeleteByConversationIdAsync(Guid conversationId);
}
