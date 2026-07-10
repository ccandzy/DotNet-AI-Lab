namespace AiChatClient.Models
{
    public enum ChatRole
    {
        User,
        Assistant,
        System
    }

    public class ChatMessage
    {
        public ChatMessage(ChatRole role, string content, DateTime timestamp)
        {
            Role = role;
            Content = content;
            Timestamp = timestamp;
        }

        public ChatRole Role { get; }

        public string Content { get; }

        public DateTime Timestamp { get; }

        public bool IsUser => Role == ChatRole.User;
    }
}
