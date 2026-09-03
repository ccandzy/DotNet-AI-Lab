using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Dtos;

namespace AiChatClient.Services
{
    public interface IChatService
    {
        /// <summary>
        /// 发送一次统一格式的 AI 请求，并以流式方式返回聊天事件。
        /// </summary>
        IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
            ChatRequest request,
            CancellationToken cancellationToken = default);
    }
}
