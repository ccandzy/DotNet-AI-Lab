using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Settings;

namespace AiChatClient.Models
{
    public class Conversation
    {
        public Guid Id { get; init; }

        public string Title { get; set; } = "New Chat";

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public DateTime CreatedTime { get; init; }

        public DateTime UpdatedTime { get; set; }

        public string Model { get; set; } = "";

        /// <summary>
        /// 对话关联的角色
        /// </summary>
        public AIRole? Role { get; set; }
    }
}
