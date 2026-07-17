using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiChatClient.Dtos
{
    public class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<ModelChatMessage> Messages { get;  } = new List<ModelChatMessage>();

        public bool Stream { get; set; }
    }
}
