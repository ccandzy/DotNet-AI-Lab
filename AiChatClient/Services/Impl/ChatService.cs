using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using AiChatClient.Models;

namespace AiChatClient.Services.Impl
{
    public class ChatService : IChatService
    {
       private readonly IChatProvider _chatProvider;
        private readonly HttpClient _httpClient;
        public ChatService(IChatProvider chatProvider, HttpClient httpClient)
        {
            _chatProvider = chatProvider;   
            _httpClient = httpClient;
        }


        public async IAsyncEnumerable<string> SendStreamingAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, _chatProvider.ApiChatUrl)
            {
                Content = _chatProvider.CreateHttpContent(messages)
            };
            var response = await  _httpClient.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStreamAsync();
           var reader = new StreamReader(result);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    // skip empty lines
                    continue;
                }

                // Some servers send SSE with a "data: " prefix
                var payload = line.Trim();
                if (payload.StartsWith("data: "))
                {
                    payload = payload.Substring("data: ".Length);
                }

                if (payload == "[DONE]")
                {
                    yield break;
                }

                var chunk = _chatProvider.Deserialize(payload);

                if (chunk is null)
                {
                    continue;
                }

                if (chunk.IsCompleted)
                {
                    yield break;
                }

                yield return chunk.Content;
                  
            }
        }
    }
}
