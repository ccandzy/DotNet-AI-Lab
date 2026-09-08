using CommunityToolkit.Mvvm.ComponentModel;
using AiChatClient.Models.Rag;
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

        [ObservableProperty]
        private IReadOnlyList<RagSourceReference> sources = Array.Empty<RagSourceReference>();

        [ObservableProperty]
        private bool ragWasEnabled;

        public DateTime Timestamp { get; }

        public bool IsUser => Role == ChatRole.User;

        /// <summary>
        /// 仅用于界面呈现的临时消息，不应参与后续 API 请求或写入数据库。
        /// </summary>
        public bool IsTransient { get; set; }
    }
}
