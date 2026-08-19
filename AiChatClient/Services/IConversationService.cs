using AiChatClient.Models;
using System.Collections.ObjectModel;

namespace AiChatClient.Services
{
    public interface IConversationService
    {
        Task InitializeAsync();
        ObservableCollection<Conversation> Conversations { get; }

         Task<Conversation> CreateConversation(Conversation conversation);

        Task DeleteConversationAsync(Guid id);

        /// <summary>
        /// 重命名会话（同时更新内存集合与数据库）。
        /// </summary>
        /// <returns>会话存在且重命名成功时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        Task<bool> RenameConversationAsync(Guid id, string newTitle);

        /// <summary>
        /// 更新会话关联的 AI 角色，并持久化到数据库。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="roleId">新角色的唯一标识。</param>
        /// <returns>会话存在且更新成功时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        Task<bool> UpdateConversationRoleAsync(Guid conversationId, Guid roleId);

        /// <summary>
        /// 更新会话锁定使用的模型，并持久化到数据库。
        /// </summary>
        /// <param name="conversationId">会话唯一标识。</param>
        /// <param name="modelId">新模型的标识。</param>
        /// <returns>会话存在且更新成功时返回 <c>true</c>；否则返回 <c>false</c>。</returns>
        Task<bool> UpdateConversationModelAsync(Guid conversationId, string modelId);
    }
}
