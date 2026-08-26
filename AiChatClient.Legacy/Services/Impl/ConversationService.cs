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
            var conversation =
                Conversations.FirstOrDefault(c => c.Id == id);

            if (conversation is null)
                return false;


            // 持久化到数据库
            var entity =
                await _conversationRepository.GetByIdAsync(id);

            if (entity is null)
                return false;

            // 先保存数据库实体；保存成功后再更新内存模型，避免界面显示未持久化的名称。
            entity.Title = newTitle ?? string.Empty;
            entity.UpdatedTime = DateTime.Now;

            await _conversationRepository.UpdateAsync(entity);

            conversation.Title = entity.Title;
            conversation.UpdatedTime = entity.UpdatedTime;
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

        /// <summary>
        /// 更新会话锁定使用的模型，并持久化到数据库。
        /// </summary>
        public async Task<bool> UpdateConversationModelAsync(
            Guid conversationId,
            string modelId)
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

            entity.Model = modelId ?? string.Empty;
            entity.UpdatedTime = DateTime.Now;

            await _conversationRepository.UpdateAsync(entity);

            conversation.Model = entity.Model;
            conversation.UpdatedTime = entity.UpdatedTime;
            return true;
        }

        /// <summary>
        /// 一次性更新会话的完整生成参数快照，并同步内存中的会话状态。
        /// </summary>
        public async Task<bool> UpdateConversationConfigurationAsync(
            Guid conversationId,
            GenerationSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

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

            entity.Temperature = settings.Temperature;
            entity.TopP = settings.TopP;
            entity.MaxTokens = settings.MaxTokens;
            entity.UpdatedTime = DateTime.Now;

            await _conversationRepository.UpdateAsync(entity);

            conversation.GenerationSettings = new GenerationSettings
            {
                Temperature = entity.Temperature,
                TopP = entity.TopP,
                MaxTokens = entity.MaxTokens
            };
            conversation.UpdatedTime = entity.UpdatedTime;
            return true;
        }
    }
}
