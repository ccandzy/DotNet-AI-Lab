using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Services;

namespace Services
{
    public interface IChatProviderResolver
    {
        IChatProvider Resolve(string providerName);
    }
}
