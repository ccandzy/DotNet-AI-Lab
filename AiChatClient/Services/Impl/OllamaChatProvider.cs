using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using AiChatClient.Models;
using Microsoft.Extensions.Options;

namespace AiChatClient.Services.Impl
{
    public class OllamaChatProvider : IChatProvider
    {
        private const string ProviderName = "Ollama";
        private readonly IOptionsMonitor<AiOptions> _options;

        public OllamaChatProvider(IOptionsMonitor<AiOptions> options)
        {
            _options = options;
        }

        private AIProviderOptions Provider => _options.CurrentValue.Providers
            .FirstOrDefault(provider =>
                string.Equals(provider.Name, ProviderName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"AI provider '{ProviderName}' is not configured.");

        private string Url => Provider.BaseUrl.TrimEnd('/');

        //public string Model => Provider.Models
        //    .FirstOrDefault(model => model.IsEnabled && !string.IsNullOrWhiteSpace(model.ModelId))
        //    ?.ModelId
        //    ?? throw new InvalidOperationException(
        //        $"No enabled model is configured for AI provider '{ProviderName}'.");

        public string ApiChatUrl => Url + "/api/chat";

        string IChatProvider.ProviderName => ProviderName;

        public void ConfigureRequest(HttpRequestMessage request)
        {
            ArgumentNullException.ThrowIfNull(request);
        }

        public HttpContent CreateHttpContent(ChatRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                throw new ArgumentException(
                    "Model cannot be empty.",
                    nameof(request));
            }
            var requestBody = new OllamaChatRequest
            {
                // 空模型时回退到 AI 配置中的第一个启用模型，保持旧行为兼容。
                Model =request.Model,
                Stream = true
            };
            foreach (var message in request.Messages)
            {
                requestBody.Messages.Add(new ModelChatMessage { Role = ConvertHelper.ConvertRole(message.Role), Content = message.Content });
            }
            return JsonContent.Create(requestBody);
        }

        public ChatChunk? Deserialize(string payload)
        {
            ChatChunk? chunk = null;
            try
            {
                var ollamaChatResponse = JsonSerializer.Deserialize<OllamaChatResponse>(payload);
                if (ollamaChatResponse != null)
                {
                    chunk = new ChatChunk() { Content = ollamaChatResponse.Message.Content, IsCompleted = ollamaChatResponse.Done };
                }
            }
            catch (JsonException)
            {
                // ignore non-json chunks
                chunk = null;
            }
            return chunk;
        }
    }
}
