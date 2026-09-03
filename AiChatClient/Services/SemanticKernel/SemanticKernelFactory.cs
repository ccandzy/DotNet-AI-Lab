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

        if (!AIProviderNames.IsSupported(request.Provider))
        {
            throw new InvalidOperationException(
                $"不支持 AI Provider '{request.Provider}'。当前仅支持 {AIProviderNames.DeepSeek} 和 {AIProviderNames.Ollama}。");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new InvalidOperationException(
                $"未选择 {request.Provider} 模型。");
        }

        var provider = _options.CurrentValue.Providers.FirstOrDefault(candidate =>
            string.Equals(
                candidate.Name,
                request.Provider,
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"AI Provider '{request.Provider}' 未配置。");

        if (string.IsNullOrWhiteSpace(provider.BaseUrl)
            || !Uri.TryCreate(provider.BaseUrl, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(
                $"{provider.Name} BaseUrl 未配置或格式无效。");
        }

        if (string.Equals(
                provider.Name,
                AIProviderNames.Ollama,
                StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                endpoint.AbsolutePath.TrimEnd('/'),
                "/v1",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Ollama BaseUrl 必须以 /v1 结尾，例如 http://192.168.137.2:11434/v1。");
        }

        var apiKey = provider.ApiKey?.Trim();
        if (string.Equals(
                provider.Name,
                AIProviderNames.DeepSeek,
                StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrWhiteSpace(apiKey) || apiKey == "你的Key"))
        {
            throw new InvalidOperationException(
                "DeepSeek ApiKey 未配置，请在 appsettings.Local.json 中填写有效密钥。");
        }

        // OpenAI 客户端要求非空密钥；Ollama 不校验该值。
        if (string.Equals(
                provider.Name,
                AIProviderNames.Ollama,
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = "ollama";
        }

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton(_loggerFactory);
        builder.AddOpenAIChatCompletion(
            modelId: request.Model,
            endpoint: endpoint,
            apiKey: apiKey!,
            httpClient: _httpClientFactory.CreateClient(HttpClientName));

        var kernel = builder.Build();
        kernel.Plugins.AddFromObject(_calculatorPlugin, "Utilities");
        kernel.Plugins.AddFromObject(_timePlugin, "Time");
        kernel.AutoFunctionInvocationFilters.Add(
            new ToolInvocationEventFilter(eventWriter));

        return kernel;
    }
}
