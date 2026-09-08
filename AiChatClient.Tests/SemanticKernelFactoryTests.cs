using System.Threading.Channels;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Plugins;
using AiChatClient.Services.SemanticKernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AiChatClient.Tests;

public sealed class SemanticKernelFactoryTests
{
    [Fact]
    public void CreateKernel_RejectsUnsupportedProvider()
    {
        var factory = CreateFactory(CreateDeepSeekOptions("key"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(
                CreateRequest(provider: "Unknown"),
                CreateWriter()));

        Assert.Contains("不支持 AI Provider", exception.Message);
    }

    [Fact]
    public void CreateKernel_RejectsMissingModel()
    {
        var factory = CreateFactory(CreateDeepSeekOptions("key"));

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(
                CreateRequest(model: string.Empty),
                CreateWriter()));

        Assert.Contains("未选择 DeepSeek 模型", exception.Message);
    }

    [Fact]
    public void CreateKernel_RejectsMissingProviderConfiguration()
    {
        var factory = CreateFactory(new AiOptions());

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(CreateRequest(), CreateWriter()));

        Assert.Contains("未配置", exception.Message);
    }

    [Fact]
    public void CreateKernel_RejectsMissingApiKey()
    {
        var factory = CreateFactory(CreateDeepSeekOptions(string.Empty));

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(CreateRequest(), CreateWriter()));

        Assert.Contains("ApiKey 未配置", exception.Message);
    }

    [Fact]
    public void CreateKernel_AcceptsOllamaWithoutARealApiKey()
    {
        var factory = CreateFactory(CreateOllamaOptions(
            baseUrl: "http://192.168.137.2:11434/v1",
            apiKey: string.Empty));

        var kernel = factory.CreateKernel(
            CreateRequest(provider: "Ollama", model: "qwen3:4b", enableTools: true),
            CreateWriter());

        Assert.Equal(
            "calculate",
            kernel.Plugins["Utilities"]["calculate"].Name);
    }

    [Fact]
    public void CreateKernel_RejectsInvalidBaseUrl()
    {
        var factory = CreateFactory(CreateOllamaOptions(
            baseUrl: "not-a-url",
            apiKey: string.Empty));

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(
                CreateRequest(provider: "Ollama", model: "qwen3:4b"),
                CreateWriter()));

        Assert.Contains("BaseUrl 未配置或格式无效", exception.Message);
    }

    [Fact]
    public void CreateKernel_RequiresOllamaV1Endpoint()
    {
        var factory = CreateFactory(CreateOllamaOptions(
            baseUrl: "http://192.168.137.2:11434",
            apiKey: string.Empty));

        var exception = Assert.Throws<InvalidOperationException>(
            () => factory.CreateKernel(
                CreateRequest(provider: "Ollama", model: "qwen3:4b"),
                CreateWriter()));

        Assert.Contains("必须以 /v1 结尾", exception.Message);
    }

    [Fact]
    public void CreateKernel_RegistersNativePluginsWithoutSendingARequest()
    {
        var factory = CreateFactory(CreateDeepSeekOptions("test-key"));

        var kernel = factory.CreateKernel(
            CreateRequest(enableTools: true),
            CreateWriter());

        Assert.Equal(
            "calculate",
            kernel.Plugins["Utilities"]["calculate"].Name);
        Assert.Equal(
            "current_time",
            kernel.Plugins["Time"]["current_time"].Name);
    }

    [Fact]
    public void CreateKernel_DoesNotRegisterNativePluginsWhenToolsAreDisabled()
    {
        var factory = CreateFactory(CreateDeepSeekOptions("test-key"));

        var kernel = factory.CreateKernel(
            CreateRequest(enableTools: false),
            CreateWriter());

        Assert.Empty(kernel.Plugins);
        Assert.Empty(kernel.AutoFunctionInvocationFilters);
    }

    [Fact]
    public void ToolRoundGuard_AllowsEightRoundsAndRejectsNinth()
    {
        for (var index = 0;
             index < ToolInvocationEventFilter.MaximumToolRounds;
             index++)
        {
            ToolInvocationEventFilter.EnsureWithinToolRoundLimit(index);
        }

        Assert.Throws<InvalidOperationException>(
            () => ToolInvocationEventFilter.EnsureWithinToolRoundLimit(
                ToolInvocationEventFilter.MaximumToolRounds));
    }

    private static SemanticKernelFactory CreateFactory(AiOptions options)
    {
        return new SemanticKernelFactory(
            new StaticOptionsMonitor<AiOptions>(options),
            new TestHttpClientFactory(),
            NullLoggerFactory.Instance,
            new CalculatorPlugin(),
            new TimePlugin());
    }

    private static AiOptions CreateDeepSeekOptions(string apiKey)
    {
        return new AiOptions
        {
            Providers =
            [
                new AIProviderOptions
                {
                    Name = "DeepSeek",
                    BaseUrl = "https://api.deepseek.com",
                    ApiKey = apiKey
                }
            ]
        };
    }

    private static AiOptions CreateOllamaOptions(
        string baseUrl,
        string apiKey)
    {
        return new AiOptions
        {
            Providers =
            [
                new AIProviderOptions
                {
                    Name = "Ollama",
                    BaseUrl = baseUrl,
                    ApiKey = apiKey
                }
            ]
        };
    }

    private static ChatRequest CreateRequest(
        string provider = "DeepSeek",
        string model = "test-model",
        bool enableTools = false)
    {
        return new ChatRequest
        {
            Provider = provider,
            Model = model,
            EnableTools = enableTools
        };
    }

    private static ChannelWriter<ChatStreamEvent> CreateWriter()
    {
        return Channel.CreateUnbounded<ChatStreamEvent>().Writer;
    }

    private sealed class TestHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient();
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name)
        {
            return _value;
        }

        public IDisposable? OnChange(
            Action<T, string?> listener)
        {
            return null;
        }
    }
}
