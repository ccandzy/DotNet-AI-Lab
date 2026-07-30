namespace AiChatClient.Entities;

/// <summary>
/// AI 角色数据库实体，与 Settings.AIRole 对应
/// </summary>
public class AIRoleEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Avatar { get; set; } = string.Empty;

    public string SystemPrompt { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public double Temperature { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreateTime { get; set; }

    // Navigation property
    public ICollection<ConversationEntity> Conversations { get; set; } = new List<ConversationEntity>();
}
