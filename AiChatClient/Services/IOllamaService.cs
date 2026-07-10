using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiChatClient.Services
{
    public interface IOllamaService
    {
        Task<string> GenerateAsync(string prompt);
    }
}
