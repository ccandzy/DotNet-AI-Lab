using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Services;

namespace Services.Impl
{
    public class ChatProviderResolver : IChatProviderResolver
    {
        private readonly IEnumerable<IChatProvider> _providers;

        public ChatProviderResolver(
            IEnumerable<IChatProvider> providers)
        {
            _providers = providers;
        }

        public IChatProvider Resolve(string providerName)
        {
            var provider = _providers.FirstOrDefault(
                x => x.ProviderName.Equals(
                    providerName,
                    StringComparison.OrdinalIgnoreCase));

            return provider
                ?? throw new InvalidOperationException(
                    $"未找到 AI Provider: {providerName}");
        }
    }
}
