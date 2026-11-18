using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace AiChatClient.Services.SemanticKernel;

public sealed class DeepSeekThinkingHandler : DelegatingHandler
{
    private readonly ILogger<DeepSeekThinkingHandler> _logger;

    public DeepSeekThinkingHandler(ILogger<DeepSeekThinkingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var uri = request.RequestUri;

        // 仅处理发往 DeepSeek 官方接口的聊天请求。
        // 同一个 HttpClient 还用于 Ollama，因此需要这个判断。
        if (request.Method == HttpMethod.Post
            && uri is not null
            && string.Equals(
                uri.Host,
                "api.deepseek.com",
                StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.TrimEnd('/').EndsWith(
                "/chat/completions",
                StringComparison.OrdinalIgnoreCase)
            && request.Content is not null)
        {
            var originalContent = request.Content;
            var json = await originalContent.ReadAsStringAsync(
                cancellationToken);

            var body = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException(
                    "DeepSeek 请求正文必须是 JSON 对象。");

            // 在最终请求 JSON 的顶层明确关闭思考模式。
            body["thinking"] = new JsonObject
            {
                ["type"] = "disabled"
            };

            var replacement = new StringContent(
                body.ToJsonString(),
                Encoding.UTF8,
                "application/json");

            // 保留其他内容头；类型和长度由新正文重新生成。
            foreach (var header in originalContent.Headers)
            {
                if (string.Equals(
                        header.Key, "Content-Length",
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        header.Key, "Content-Type",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                replacement.Headers.TryAddWithoutValidation(
                    header.Key, header.Value);
            }

            request.Content = replacement;
            originalContent.Dispose();

            // Log only routing metadata from the final JSON, never messages,
            // tool arguments/results, schemas, descriptions, or authorization headers.
            var tools = body["tools"] as JsonArray;
            var toolNames = tools?
                .OfType<JsonObject>()
                .Select(tool => (tool["function"] as JsonObject)?["name"]?.GetValue<string>() ?? "<unnamed>")
                .ToArray() ?? Array.Empty<string>();
            var toolChoice = body["tool_choice"] is JsonObject namedChoice
                ? $"function:{(namedChoice["function"] as JsonObject)?["name"]?.GetValue<string>() ?? "<unnamed>"}"
                : body["tool_choice"]?.GetValue<string>() ?? "<omitted>";

            _logger.LogInformation(
                "DeepSeek outgoing request: Model={Model}, Thinking={Thinking}, ToolChoice={ToolChoice}, ToolCount={ToolCount}, Tools=[{Tools}], MessageCount={MessageCount}.",
                body["model"]?.GetValue<string>(), "disabled", toolChoice,
                tools?.Count ?? 0, string.Join(", ", toolNames),
                (body["messages"] as JsonArray)?.Count ?? 0);
        }

        // 将处理后的请求交给后续处理器，继续发送。
        return await base.SendAsync(request, cancellationToken);
    }
}
