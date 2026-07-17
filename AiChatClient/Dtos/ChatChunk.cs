using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiChatClient.Dtos
{
    public class ChatChunk
    {
        public string Content { get; init; }

        public bool IsCompleted { get; init; }
    }
}
