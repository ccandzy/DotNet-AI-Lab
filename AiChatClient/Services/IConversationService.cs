using AiChatClient.Models;
using System.Collections.ObjectModel;

namespace AiChatClient.Services
{
    public interface IConversationService
    {
        Task InitializeAsync();
        ObservableCollection<Conversation> Conversations { get; }

         Task<Conversation> CreateConversation(Conversation conversation);

        void DeleteConversation(Guid id);

        bool RenameConversation(Guid id, string newTitle);
    }
}
