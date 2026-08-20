using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using Microsoft.Extensions.Options;

namespace AiChatClient.Services.Impl;

/// <summary>
/// DeepSeek Chat Completions Provider。
/// DeepSeek 使用 OpenAI 兼容的 JSON 请求和 SSE 流式响应，并通过 Bearer Token 鉴权。
/// </summary>
public sealed class DeepSeekChatProvider : IChatProvider
{
    private const string ProviderNameValue = "DeepSeek";
    private readonly IOptionsMonitor<AiOptions> _options;

    public DeepSeekChatProvider(IOptionsMonitor<AiOptions> options)
    {
        _options = options;
    }

    public string ProviderName => ProviderNameValue;

    private AIProviderOptions Provider => _options.CurrentValue.Providers
        .FirstOrDefault(provider =>
            string.Equals(provider.Name, ProviderNameValue, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"AI provider '{ProviderNameValue}' is not configured.");

    private string Url => Provider.BaseUrl.TrimEnd('/');

    public string ApiChatUrl => Url + "/chat/completions";

    /// <summary>
    /// 为 DeepSeek 请求添加 Bearer Token 和 SSE 响应头。
    /// </summary>
    public void ConfigureRequest(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var apiKey = Provider.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "你的Key")
        {
            throw new InvalidOperationException(
                "DeepSeek ApiKey 未配置，请在 appsettings.json 的 AI:Providers:DeepSeek:ApiKey 中填写有效密钥。");
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    /// <summary>
    /// 将统一 ChatRequest 转换为 DeepSeek Chat Completions 请求。
    /// </summary>
    public HttpContent CreateHttpContent(ChatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            throw new ArgumentException("Model cannot be empty.", nameof(request));
        }

        var requestBody = new DeepSeekChatRequest
        {
            Model = request.Model,
            Stream = true,
            Temperature = request.Settings.Temperature,
            TopP = request.Settings.TopP,
            MaxTokens = request.Settings.MaxTokens,
        };

        foreach (var message in request.Messages)
        {
            requestBody.Messages.Add(new ModelChatMessage
            {
                Role = ConvertHelper.ConvertRole(message.Role),
                Content = message.Content
            });
        }

        return JsonContent.Create(requestBody);
    }

    /// <summary>
    /// 解析 DeepSeek SSE 数据块中的增量文本和结束状态。
    /// </summary>
    public ChatChunk? Deserialize(string payload)
    {
        if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatChunk { Content = string.Empty, IsCompleted = true };
        }

        try
        {
            var response = JsonSerializer.Deserialize<DeepSeekChatResponse>(payload);
            var choice = response?.Choices.FirstOrDefault();

            if (choice is null)
            {
                return null;
            }

            return new ChatChunk
            {
                Content = choice.Delta.Content ?? string.Empty,
                IsCompleted = !string.IsNullOrWhiteSpace(choice.FinishReason)
            };
        }
        catch (JsonException)
        {
            // 忽略无法解析的 SSE 数据块，由 ChatService 继续读取后续内容。
            return null;
        }
    }
}
