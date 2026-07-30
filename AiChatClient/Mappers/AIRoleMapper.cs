using AiChatClient.Entities;
using AiChatClient.Settings;

namespace AiChatClient.Mappers;

/// <summary>
/// AIRoleEntity <-> Settings.AIRole 映射
/// </summary>
public static class AIRoleMapper
{
    public static AIRole ToModel(AIRoleEntity entity)
    {
        return new AIRole
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            Avatar = entity.Avatar,
            SystemPrompt = entity.SystemPrompt,
            Model = entity.Model,
            Temperature = entity.Temperature,
            IsEnabled = entity.IsEnabled,
            CreateTime = entity.CreateTime,
        };
    }

    public static AIRoleEntity ToEntity(AIRole model)
    {
        return new AIRoleEntity
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            Avatar = model.Avatar,
            SystemPrompt = model.SystemPrompt,
            Model = model.Model,
            Temperature = model.Temperature,
            IsEnabled = model.IsEnabled,
            CreateTime = model.CreateTime,
        };
    }

    /// <summary>
    /// 用 model 的值更新已有 entity（用于 Update 场景）
    /// </summary>
    public static void UpdateEntity(AIRole model, AIRoleEntity entity)
    {
        entity.Name = model.Name;
        entity.Description = model.Description;
        entity.Avatar = model.Avatar;
        entity.SystemPrompt = model.SystemPrompt;
        entity.Model = model.Model;
        entity.Temperature = model.Temperature;
        entity.IsEnabled = model.IsEnabled;
        entity.CreateTime = model.CreateTime;
    }
}
