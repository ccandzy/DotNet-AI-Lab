using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using AiChatClient.Models;

namespace AiChatClient.Services.Impl
{
    public class OllamaChatProvider : IChatProvider
    {
        private string Url => App.Config["Ollama:BaseUrl"]!;

        public string Model => App.Config["Ollama:Model"]!;

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
