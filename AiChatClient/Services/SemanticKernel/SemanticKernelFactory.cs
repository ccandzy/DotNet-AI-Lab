using System.Threading.Channels;
using System.Net.Http;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace AiChatClient.Services.SemanticKernel;

internal sealed class SemanticKernelFactory : IKernelFactory
{
    internal const string HttpClientName = "SemanticKernel";
    private const string SupportedProviderName = "DeepSeek";

    private readonly IOptionsMonitor<AiOptions> _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly CalculatorPlugin _calculatorPlugin;
    private readonly TimePlugin _timePlugin;

    public SemanticKernelFactory(
        IOptionsMonitor<AiOptions> options,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CalculatorPlugin calculatorPlugin,
        TimePlugin timePlugin)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _loggerFactory = loggerFactory;
        _calculatorPlugin = calculatorPlugin;
        _timePlugin = timePlugin;
    }

    public Kernel CreateKernel(
        ChatRequest request,
        ChannelWriter<ChatStreamEvent> eventWriter)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(eventWriter);

        if (!string.Equals(
                request.Provider,
                SupportedProviderName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Semantic Kernel 版本当前仅支持 {SupportedProviderName}，收到的 Provider 为：{request.Provider}。");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException("未选择 DeepSeek 模型。");
        }

        var provider = _options.CurrentValue.Providers.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Name,
                SupportedProviderName,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"AI Provider '{SupportedProviderName}' 未配置。");

        if (string.IsNullOrWhiteSpace(provider.BaseUrl)
            || !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException("DeepSeek BaseUrl 未配置或格式无效。");
        }

        var apiKey = provider.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "你的Key")
        {
            throw new InvalidOperationException(
                "DeepSeek ApiKey 未配置，请在 appsettings.Local.json 中填写有效密钥。");
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_loggerFactory);
        builder.AddOpenAIChatCompletion(
            modelId: request.Model,
            endpoint: endpoint,
            apiKey: apiKey,
            httpClient: _httpClientFactory.CreateClient(HttpClientName));

        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(_calculatorPlugin, "Utilities");
        kernel.Plugins.AddFromObject(_timePlugin, "Time");
        kernel.AutoFunctionInvocationFilters.Add(
            new ToolInvocationEventFilter(eventWriter));

        return kernel;
    }
}
