using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace AiChatClient.Dtos
{
    public class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public ModelChatMessage Message { get; set; } = new ModelChatMessage();

        [JsonPropertyName("done")]
        public bool Done { get; set; }
        [JsonPropertyName("done_reason")]
        public string? DoneReason { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}
