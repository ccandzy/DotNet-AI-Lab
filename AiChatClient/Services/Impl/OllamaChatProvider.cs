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

        public string Model => Provider.Models
            .FirstOrDefault(model => model.IsEnabled && !string.IsNullOrWhiteSpace(model.ModelId))
            ?.ModelId
            ?? throw new InvalidOperationException(
                $"No enabled model is configured for AI provider '{ProviderName}'.");

        public string ApiChatUrl => Url + "/api/chat";

        public HttpContent CreateHttpContent(IReadOnlyList<ChatMessage> messages)
        {
            var requestBody = new OllamaChatRequest
            {
                Model = this.Model,
                Stream = true
            };
            foreach (var message in messages)
            {
                requestBody.Messages.Add(new ModelChatMessage { Role = ConvertHelper.ConvertRole(message.Role), Content = message.Content });
            }
            return JsonContent.Create(requestBody);
        }

        public ChatChunk Deserialize(string payload)
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
