using AiChatClient.Models;
using System.Collections.ObjectModel;

namespace AiChatClient.Services
{
    public interface IConversationService
    {
        ObservableCollection<Conversation> Conversations { get; }

        Conversation CreateConversation();

        void DeleteConversation(Guid id);

        bool RenameConversation(Guid id, string newTitle);
    }
}
