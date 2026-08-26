using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Entities;

namespace Repositories
{
    public interface IConversationRepository
    {
        Task<List<ConversationEntity>> GetAllAsync();

        Task<ConversationEntity?> GetByIdAsync(Guid id);

        Task AddAsync(ConversationEntity entity);

        Task UpdateAsync(ConversationEntity entity);

        Task DeleteAsync(Guid id);
    }
}
