using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Models;

namespace AiChatClient.Services
{
    public interface IChatService
    {
        IAsyncEnumerable<string> SendStreamingAsync(IReadOnlyList<ChatMessage> messages,CancellationToken cancellationToken = default);
    }
}
