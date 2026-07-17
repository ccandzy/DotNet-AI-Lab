using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace AiChatClient.Dtos
{
    public class ModelChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "system";

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
