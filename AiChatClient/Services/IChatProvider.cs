using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Dtos;
using AiChatClient.Models;

namespace AiChatClient.Services
{
    public interface IChatProvider
    {
      
        public string Model { get; }

        public string ApiChatUrl { get; }

        HttpContent CreateHttpContent(IReadOnlyList<ChatMessage> messages);

        ChatChunk Deserialize(string payload);
    }
}
