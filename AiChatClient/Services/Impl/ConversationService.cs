using AiChatClient.Mappers;
using AiChatClient.Models;
using Repositories;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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
        /// �����ݿ������ʷ�Ự
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


        public async Task<Conversation> CreateConversation(Conversation conv)
        {
            if (conv.Role == null)
            {
                throw new InvalidOperationException(
                    "Conversation must have an AI Role.");
            }

            var entity = ConversationMapper.ToEntity(conv);

            await _conversationRepository.AddAsync(entity);

            Conversations.Add(conv);

            return conv;
        }


        public async Task DeleteConversationAsync(Guid id)
        {
            await _conversationRepository.DeleteAsync(id);

            var exist =
                Conversations.FirstOrDefault(x => x.Id == id);

            if (exist != null)
            {
                Conversations.Remove(exist);
            }
        }


        public async Task<bool> RenameConversationAsync(Guid id, string newTitle)
        {
            var exist =
                Conversations.FirstOrDefault(c => c.Id == id);

            if (exist is null)
                return false;


            // 持久化到数据库
            var entity =
                await _conversationRepository.GetByIdAsync(id);

            if (entity is null)
                return false;

            ConversationMapper.UpdateEntity(exist, entity);

            await _conversationRepository.UpdateAsync(entity);

            exist.Title = newTitle ?? string.Empty;
            exist.UpdatedTime = DateTime.Now;
            return true;
        }
        /// <summary>
        /// 更新会话关联的 AI 角色，并持久化到数据库。
        /// </summary>
        public async Task<bool> UpdateConversationRoleAsync(
            Guid conversationId,
            Guid roleId)
        {
            var conversation = Conversations
                .FirstOrDefault(c => c.Id == conversationId);

            if (conversation is null)
            {
                return false;
            }

            var entity = await _conversationRepository
                .GetByIdAsync(conversationId);

            if (entity is null)
            {
                return false;
            }

            entity.AIRoleId = roleId;
            entity.UpdatedTime = DateTime.Now;

            await _conversationRepository.UpdateAsync(entity);

            conversation.UpdatedTime = entity.UpdatedTime;
            return true;
        }
    }
}
