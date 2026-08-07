using AiChatClient.Mappers;
using AiChatClient.Models;
using Repositories;


namespace AiChatClient.Services.Impl;


public class ChatMessageService
    : IChatMessageService
{

    private readonly IChatMessageRepository
        _repository;


    public ChatMessageService(
        IChatMessageRepository repository)
    {
        _repository = repository;
    }



    public async Task AddMessageAsync(
        Guid conversationId,
        ChatMessage message)
    {

        var entity =
            ChatMessageMapper.ToEntity(
                message,
                conversationId);


        await _repository.AddAsync(entity);
    }



    public async Task<List<ChatMessage>>
        GetMessagesAsync(Guid conversationId)
    {

        var entities =
            await _repository
            .GetByConversationIdAsync(conversationId);


        return entities
            .Select(ChatMessageMapper.ToModel)
            .ToList();
    }
}