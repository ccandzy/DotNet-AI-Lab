using CommunityToolkit.Mvvm.ComponentModel;
using Models;

namespace AiChatClient.Models
{


    public partial class ChatMessage:ObservableObject
    {
        public ChatMessage(ChatRole role, string content, DateTime timestamp)
        {
            Role = role;
            Content = content;
            Timestamp = timestamp;
        }

        public ChatRole Role { get; }

        [ObservableProperty]
        private string content="";

        public DateTime Timestamp { get; }

        public bool IsUser => Role == ChatRole.User;
    }
}
