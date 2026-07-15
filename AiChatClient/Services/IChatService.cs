using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiChatClient.Services
{
    public interface IChatService
    {
        Task<string> SendAsync(string message);

        IAsyncEnumerable<string> SendStreamingAsync(string message,CancellationToken cancellationToken = default);
    }
}
