using AiChatClient.Mappers;
using AiChatClient.Models;
using Repositories;
using System.Collections.ObjectModel;

namespace AiChatClient.Services.Impl
{
    public class ConversationService : IConversationService
    {
        public ObservableCollection<Conversation> Conversations { get; }
            = new ObservableCollection<Conversation>();

        private readonly IConversationRepository _conversationRepository;


        public ConversationService(
            IConversationRepository conversationRepository)
        {
            _conversationRepository = conversationRepository;
        }


        /// <summary>
        /// 从数据库加载历史会话
        /// </summary>
        public async Task InitializeAsync()
        {
            var entities =
                await _conversationRepository.GetAllAsync();


            Conversations.Clear();


            foreach (var entity in entities)
            {
                Conversations.Add(
                    ConversationMapper.ToModel(entity));
            }
        }


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
            var exist =
                Conversations.FirstOrDefault(c => c.Id == id);

            if (exist is not null)
            {
                Conversations.Remove(exist);
            }
        }


        public bool RenameConversation(Guid id, string newTitle)
        {
            var exist =
                Conversations.FirstOrDefault(c => c.Id == id);

            if (exist is null)
                return false;


            exist.Title = newTitle ?? string.Empty;
            exist.UpdatedTime = DateTime.Now;

            return true;
        }
    }
}