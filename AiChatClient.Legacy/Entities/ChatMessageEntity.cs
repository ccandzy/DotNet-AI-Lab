namespace AiChatClient.Entities;

/// <summary>
/// 聊天消息数据库实体，与 Models.ChatMessage 对应
/// </summary>
public class ChatMessageEntity
{
    public Guid Id { get; set; }

    public Guid ConversationId { get; set; }

    /// <summary>
    /// 消息角色，存储字符串：User / Assistant / System
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }

    // Navigation property
    public ConversationEntity Conversation { get; set; } = null!;
}
