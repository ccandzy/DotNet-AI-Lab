using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AiChatClient.Services.SemanticKernel;
using Microsoft.Extensions.Logging;

namespace AiChatClient.Tests;

public sealed class DeepSeekThinkingHandlerTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("required")]
    public async Task LogsActualToolPolicyWithoutLoggingSensitivePayload(string choice)
    {
        var logger = new RecordingLogger();
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DeepSeekThinkingHandler(logger) { InnerHandler = capture });
        var body = new JsonObject
        {
            ["model"] = "test-model",
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = "private-message" }),
            ["tool_choice"] = choice,
            ["tools"] = new JsonArray(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = "MCP-mcp_current_time",
                    ["description"] = "private-description"
                }
            })
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer private-key");
        using var response = await client.SendAsync(request);

        var sent = JsonNode.Parse(capture.Body!)!;
        Assert.Equal("disabled", sent["thinking"]!["type"]!.GetValue<string>());
        Assert.Equal(choice, sent["tool_choice"]!.GetValue<string>());
        Assert.Equal("private-message", sent["messages"]![0]!["content"]!.GetValue<string>());
        var log = Assert.Single(logger.Messages);
        Assert.Contains($"ToolChoice={choice}", log);
        Assert.Contains("ToolCount=1", log);
        Assert.Contains("MCP-mcp_current_time", log);
        Assert.Contains("Thinking=disabled", log);
        Assert.DoesNotContain("private-message", log);
        Assert.DoesNotContain("private-description", log);
        Assert.DoesNotContain("private-key", log);
    }

    [Fact]
    public async Task LogsMissingToolsAndChoiceExplicitly()
    {
        var logger = new RecordingLogger();
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DeepSeekThinkingHandler(logger) { InnerHandler = capture });
        using var content = new StringContent("{\"model\":\"test-model\",\"messages\":[]}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("https://api.deepseek.com/chat/completions", content);

        var log = Assert.Single(logger.Messages);
        Assert.Contains("ToolChoice=<omitted>", log);
        Assert.Contains("ToolCount=0", log);
    }

    [Fact]
    public async Task LeavesOllamaRequestUnchanged()
    {
        var logger = new RecordingLogger();
        var capture = new CaptureHandler();
        using var client = new HttpClient(new DeepSeekThinkingHandler(logger) { InnerHandler = capture });
        const string json = "{\"model\":\"local-model\",\"messages\":[]}";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("http://localhost:11434/v1/chat/completions", content);

        Assert.Equal(json, capture.Body);
        Assert.Empty(logger.Messages);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class RecordingLogger : ILogger<DeepSeekThinkingHandler>
    {
        public List<string> Messages { get; } = new();
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception));
    }
}
