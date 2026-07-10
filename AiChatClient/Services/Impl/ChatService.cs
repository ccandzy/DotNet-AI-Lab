using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiChatClient.Services.Impl
{
    public class ChatService : IChatService
    {
        private readonly IOllamaService _ollamaService;
        public ChatService(IOllamaService ollamaService)
        {
            _ollamaService = ollamaService;
        }
        public async Task<string> SendAsync(string message)
        {
            return await _ollamaService.GenerateAsync(message);
        }
    }
}
