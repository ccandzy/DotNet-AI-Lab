namespace AiChatClient.Entities;

/// <summary>
/// 对话数据库实体，与 Models.Conversation 对应
/// </summary>
public class ConversationEntity
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "New Chat";

    public Guid AIRoleId { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime UpdatedTime { get; set; }

    public string Model { get; set; } = string.Empty;

    // Navigation properties
    public AIRoleEntity AIRole { get; set; } = null!;

    public ICollection<ChatMessageEntity> Messages { get; set; } = new List<ChatMessageEntity>();
}
