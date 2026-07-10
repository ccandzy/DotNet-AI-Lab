using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Models;

namespace AiChatClient.Services.Impl
{
    public class OllamaService : IOllamaService
    {
        string url = App.Config["Ollama:BaseUrl"];
        string model = App.Config["Ollama:Model"];
        private readonly HttpClient _httpClient;
        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        public async Task<string> GenerateAsync(string prompt)
        {
            var request = new OllamaGenerateRequest
            {
                Model =model,
                Prompt = prompt,
                Stream = false
            };
            var response = await _httpClient.PostAsJsonAsync(url + "/api/generate", request);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>();
            return result?.Response ?? string.Empty;
        }
    }
}
