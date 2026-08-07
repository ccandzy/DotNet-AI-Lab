using AiChatClient.Data;
using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Impl;


public class ChatMessageRepository : IChatMessageRepository
{
    private readonly AppDbContext _context;


    public ChatMessageRepository(
        AppDbContext context)
    {
        _context = context;
    }


    public async Task AddAsync(
        ChatMessageEntity entity)
    {
        await _context.ChatMessages.AddAsync(entity);

        await _context.SaveChangesAsync();
    }


    public async Task<List<ChatMessageEntity>>
        GetByConversationIdAsync(Guid conversationId)
    {
        return await _context.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();
    }
}