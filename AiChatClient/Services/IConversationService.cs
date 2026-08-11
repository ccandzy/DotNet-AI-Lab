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
    }
}
