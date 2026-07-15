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

namespace AiChatClient.Services.Impl
{
    public class ChatService : IChatService
    {
        private readonly string _url;
        private readonly string _model;
        private readonly HttpClient _httpClient;
        public ChatService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _url = App.Config["Ollama:BaseUrl"]!;
            _model = App.Config["Ollama:Model"]!;
        }
        public async Task<string> SendAsync(string prompt)
        {
            var request = new OllamaGenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = false
            };
            var response = await _httpClient.PostAsJsonAsync(_url + "/api/generate", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            if (result is null)
            {
                throw new InvalidOperationException(
                    "Ollama returned an empty response.");
            }
            return result.Response;
        }

        public async IAsyncEnumerable<string> SendStreamingAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var requestBody = new OllamaGenerateRequest
            {
                Model = _model,
                Prompt = prompt,
                Stream = true
            }; 
            var request = new HttpRequestMessage(HttpMethod.Post, _url + "/api/generate")
            {
                Content = JsonContent.Create(requestBody)
            };
            var response = await  _httpClient.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadAsStreamAsync();
           var reader = new StreamReader(result);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var line = await reader.ReadLineAsync();
                if (line is not null)
                {
                    var chunk = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                    if (chunk is not null)
                    {
                        if(chunk.Done)
                        {
                            yield break;
                        }
                        //Debug.WriteLine($"Received line: {line}");
                        yield return chunk.Response;
                    }
                    else
                    {
                        yield break;
                    }
                }
                  
            }
        }
    }
}
