using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace AiChatClient.Services.SemanticKernel;

public sealed class DeepSeekThinkingHandler : DelegatingHandler
{
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
        }

        // 将处理后的请求交给后续处理器，继续发送。
        return await base.SendAsync(request, cancellationToken);
    }
}