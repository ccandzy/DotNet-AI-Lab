using AiChatClient.Entities;
using AiChatClient.Models;

namespace AiChatClient.Mappers;

/// <summary>
/// ConversationEntity <-> Models.Conversation 映射
/// </summary>
public static class ConversationMapper
{
    public static Conversation ToModel(ConversationEntity entity)
    {
        var conversation = new Conversation
        {
            Id = entity.Id,
            Title = entity.Title,
            CreatedTime = entity.CreatedTime,
            UpdatedTime = entity.UpdatedTime,
            Model = entity.Model,
            GenerationSettings = new GenerationSettings
            {
                Temperature = entity.Temperature,
                TopP = entity.TopP,
                MaxTokens = entity.MaxTokens
            }
        };

        // 映射导航属性中的角色
        if (entity.AIRole != null)
        {
            conversation.Role = AIRoleMapper.ToModel(entity.AIRole);
        }

        // 映射消息列表
        if (entity.Messages.Count > 0)
        {
            foreach (var msgEntity in entity.Messages.OrderBy(m => m.Timestamp))
            {
                conversation.Messages.Add(ChatMessageMapper.ToModel(msgEntity));
            }
        }

        return conversation;
    }

    public static ConversationEntity ToEntity(Conversation model)
    {
        var entity = new ConversationEntity
        {
            Id = model.Id,
            Title = model.Title,
            CreatedTime = model.CreatedTime,
            UpdatedTime = model.UpdatedTime,
            Model = model.Model,
            Temperature = model.GenerationSettings.Temperature,
            TopP = model.GenerationSettings.TopP,
            MaxTokens = model.GenerationSettings.MaxTokens,
        };

        // 映射关联的角色
        if (model.Role != null)
        {
            entity.AIRoleId = model.Role.Id;
        }

        return entity;
    }

    /// <summary>
    /// 用 model 的值更新已有 entity
    /// </summary>
    public static void UpdateEntity(Conversation model, ConversationEntity entity)
    {
        entity.Title = model.Title;
        entity.UpdatedTime = model.UpdatedTime;
        entity.Model = model.Model;
        entity.Temperature = model.GenerationSettings.Temperature;
        entity.TopP = model.GenerationSettings.TopP;
        entity.MaxTokens = model.GenerationSettings.MaxTokens;

        if (model.Role != null)
        {
            entity.AIRoleId = model.Role.Id;
        }
    }
}
