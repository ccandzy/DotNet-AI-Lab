using AiChatClient.Models;

namespace AiChatClient.Services;


public interface IChatMessageService
{
    Task AddMessageAsync(
        Guid conversationId,
        ChatMessage message);


    Task<List<ChatMessage>>
        GetMessagesAsync(Guid conversationId);

    /// <summary>
    /// 删除指定会话下的全部聊天消息。
    /// </summary>
    Task DeleteMessagesByConversationIdAsync(Guid conversationId);
}
