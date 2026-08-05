using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiChatClient.Data;
using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Impl
{
    /// <summary>
    /// <see cref="IConversationRepository"/> 的 EF Core 实现。
    /// 负责对话（Conversation）实体的 CRUD 及列表查询。
    /// </summary>
    public class ConversationRepository : IConversationRepository
    {
        private readonly AppDbContext _context;

        /// <summary>
        /// 构造 <see cref="ConversationRepository"/>。
        /// </summary>
        /// <param name="context">应用数据库上下文，通过 DI 注入。</param>
        public ConversationRepository(AppDbContext context)
        {
            _context = context;
        }

        #region 查询

        /// <summary>
        /// 获取所有对话，按 <see cref="ConversationEntity.UpdatedTime"/> 降序排列。
        /// 级联加载关联的 <see cref="ConversationEntity.AIRole"/> 及消息集合。
        /// </summary>
        /// <returns>对话实体列表，若无数据则返回空列表。</returns>
        public async Task<List<ConversationEntity>> GetAllAsync()
        {
            return await _context.Conversations
                .Include(x => x.AIRole)
                .Include(x => x.Messages)
                .OrderByDescending(x => x.UpdatedTime)
                .ToListAsync();
        }

        /// <summary>
        /// 根据主键 ID 获取单个对话。
        /// 级联加载关联的 <see cref="ConversationEntity.AIRole"/> 及消息集合。
        /// </summary>
        /// <param name="id">对话唯一标识。</param>
        /// <returns>若存在则返回对应 <see cref="ConversationEntity"/>，否则返回 <c>null</c>。</returns>
        public async Task<ConversationEntity?> GetByIdAsync(Guid id)
        {
            return await _context.Conversations
                .Include(x => x.AIRole)
                .Include(x => x.Messages)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        #endregion

        #region 写操作

        /// <summary>
        /// 新增一条对话记录。
        /// <see cref="ConversationEntity.Id"/> 由调用方在插入前生成（Guid 主键，ValueGeneratedNever）。
        /// </summary>
        /// <param name="entity">待新增的对话实体。</param>
        /// <returns>表示异步写操作的 <see cref="Task"/>。</returns>
        public async Task AddAsync(ConversationEntity entity)
        {
            await _context.Conversations.AddAsync(entity);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 更新一条已有的对话记录。
        /// EF Core 会追踪实体状态变更并生成相应的 UPDATE 语句。
        /// </summary>
        /// <param name="entity">包含最新属性的对话实体（Id 必须与既有记录一致）。</param>
        /// <returns>表示异步写操作的 <see cref="Task"/>。</returns>
        /// <exception cref="InvalidOperationException">当传入实体未被上下文追踪时。</exception>
        public async Task UpdateAsync(ConversationEntity entity)
        {
            // 先 Attach 确保实体被上下文追踪，再标记为 Modified 触发全属性更新
            _context.Entry(entity).State = EntityState.Modified;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// 根据主键 ID 删除一条对话记录。
        /// 级联删除由数据库外键约束（OnDelete(DeleteBehavior.Cascade)）接管。
        /// </summary>
        /// <param name="id">待删除对话的唯一标识。</param>
        /// <returns>表示异步写操作的 <see cref="Task"/>。</returns>
        /// <exception cref="KeyNotFoundException">当指定 ID 的记录不存在时。</exception>
        public async Task DeleteAsync(Guid id)
        {
            var entity = await _context.Conversations
                .FirstOrDefaultAsync(x => x.Id == id);

            if (entity is null)
            {
                throw new KeyNotFoundException(
                    $"Conversation with Id '{id}' was not found and cannot be deleted.");
            }

            _context.Conversations.Remove(entity);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}
