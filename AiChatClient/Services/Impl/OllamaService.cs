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
        private readonly string _url;
        private readonly string _model;
        private readonly HttpClient _httpClient;
        public OllamaService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _url = App.Config["Ollama:BaseUrl"]!;
            _model = App.Config["Ollama:Model"]!;
        }
        public async Task<string> GenerateAsync(string prompt)
        {
            var request = new OllamaGenerateRequest
            {
                Model =_model,
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
    }
}
