using System;
using System.Collections.Generic;
namespace AiChatClient.Config
{
    public class AiOptions
    {
        public List<AIProviderOptions> Providers { get; set; } = new();
    }
}
