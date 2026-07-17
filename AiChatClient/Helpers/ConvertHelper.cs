using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Models;

namespace AiChatClient.Helpers
{
    public class ConvertHelper
    {
        public static string ConvertRole(ChatRole role)
        {
            return role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                ChatRole.System => "system",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
