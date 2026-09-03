using System.Windows;
using System.Windows.Controls;
using AiChatClient.Models;

namespace AiChatClient.Helpers
{
    public class ChatMessageTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? UserTemplate { get; set; }
        public DataTemplate? AssistantTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object item, DependencyObject container)
        {
            if (item is ChatMessage message)
            {
                return message.IsUser ? UserTemplate : AssistantTemplate;
            }

            return base.SelectTemplate(item, container);
        }
    }
}
