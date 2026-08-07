using AiChatClient.Models;

namespace AiChatClient.Services;


public interface IChatMessageService
{
    Task AddMessageAsync(
        Guid conversationId,
        ChatMessage message);


    Task<List<ChatMessage>>
        GetMessagesAsync(Guid conversationId);
}