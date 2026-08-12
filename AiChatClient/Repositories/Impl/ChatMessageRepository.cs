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


    /// <summary>
    /// 删除指定会话下的全部聊天消息，并将变更持久化到数据库。
    /// </summary>
    public async Task DeleteByConversationIdAsync(Guid conversationId)
    {
        var messages = await _context.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .ToListAsync();

        if (messages.Count == 0)
        {
            return;
        }

        _context.ChatMessages.RemoveRange(messages);

        await _context.SaveChangesAsync();
    }
}
