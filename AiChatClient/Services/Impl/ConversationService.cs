using AiChatClient.Models;
using System.Collections.ObjectModel;

namespace AiChatClient.Services.Impl
{
    public class ConversationService : IConversationService
    {
        public ObservableCollection<Conversation> Conversations { get; } = new ObservableCollection<Conversation>();

        public Conversation CreateConversation()
        {
            var conv = new Conversation
            {
                Id = Guid.NewGuid(),
                Title = "New Chat",
                CreatedTime = DateTime.Now,
                UpdatedTime = DateTime.Now,
                Model = string.Empty
            };
            Conversations.Add(conv);
            return conv;
        }

        public void DeleteConversation(Guid id)
        {
            var exist = Conversations.FirstOrDefault(c => c.Id == id);
            if (exist is not null)
            {
                Conversations.Remove(exist);
            }
        }

        public bool RenameConversation(Guid id, string newTitle)
        {
            var exist = Conversations.FirstOrDefault(c => c.Id == id);
            if (exist is null) return false;
            exist.Title = newTitle ?? string.Empty;
            exist.UpdatedTime = DateTime.Now;
            return true;
        }
    }
}
