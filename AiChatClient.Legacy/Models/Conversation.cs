using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiChatClient.Models
{
    public class Conversation : ObservableObject
    {
        public Guid Id { get; init; }

        private string _title = "New Chat";

        /// <summary>
        /// 会话标题。变更时通知 UI（如列表项）刷新。
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public DateTime CreatedTime { get; init; }

        public DateTime UpdatedTime { get; set; }

        public string Model { get; set; } = "";

        public GenerationSettings GenerationSettings { get; set; } = new();
        /// <summary>
        /// 对话关联的角色
        /// </summary>
        public AIRole? Role { get; set; }
    }
}
