using AiChatClient.Data;
using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Impl;


public class ChatMessageRepository : IChatMessageRepository
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;


    public ChatMessageRepository(
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }


    public async Task AddAsync(
        ChatMessageEntity entity)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync();

        await context.ChatMessages.AddAsync(entity);

        await context.SaveChangesAsync();
    }


    public async Task<List<ChatMessageEntity>>
        GetByConversationIdAsync(Guid conversationId)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync();

        return await context.ChatMessages
            .AsNoTracking()
            .Where(x => x.ConversationId == conversationId)
            .OrderBy(x => x.Timestamp)
            .ToListAsync();
    }


    /// <summary>
    /// 删除指定会话下的全部聊天消息，并将变更持久化到数据库。
    /// </summary>
    public async Task DeleteByConversationIdAsync(Guid conversationId)
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync();

        await context.ChatMessages
            .Where(x => x.ConversationId == conversationId)
            .ExecuteDeleteAsync();
    }
}
