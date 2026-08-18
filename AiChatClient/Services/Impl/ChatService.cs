using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using AiChatClient.Models;
using Services;

namespace AiChatClient.Services.Impl
{
    public class ChatService : IChatService
    {
        private readonly IChatProviderResolver _chatProviderResolver;
        private readonly HttpClient _httpClient;
        public ChatService(IChatProviderResolver chatProviderResolver, HttpClient httpClient)
        {
            _chatProviderResolver = chatProviderResolver;   
            _httpClient = httpClient;
        }


        public async IAsyncEnumerable<string> SendStreamingAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
           var  _chatProvider = _chatProviderResolver.Resolve(request.Provider);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, _chatProvider.ApiChatUrl)
            {
                Content = _chatProvider.CreateHttpContent(request)
            };
            _chatProvider.ConfigureRequest(httpRequest);
            Debug.WriteLine($"request.Content:{httpRequest.Content}");
            var response = await  _httpClient.SendAsync(httpRequest,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            response.EnsureSuccessStatusCode();
            Debug.WriteLine($"response.Content:{response.Content}");
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
