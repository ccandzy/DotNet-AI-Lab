using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Helpers;
using AiChatClient.Models.Tools;
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
            Thinking = request.Tools.Count > 0 ? new DeepSeekThinking() : null
        };

        foreach (var message in request.Messages)
        {
            requestBody.Messages.Add(CreateMessage(message));
        }

        if (request.Tools.Count > 0)
        {
            requestBody.Tools = request.Tools
                .Select(CreateToolDefinition)
                .ToList();
        }

        return JsonContent.Create(requestBody);
    }

    private static ModelChatMessage CreateMessage(ChatRequestMessage message)
    {
        var providerMessage = new ModelChatMessage
        {
            Role = ConvertHelper.ConvertRole(message.Role),
            Content = message.Content,
            ToolCallId = message.ToolCallId
        };

        if (message.ToolCalls.Count > 0)
        {
            providerMessage.ToolCalls = message.ToolCalls
                .Select(toolCall => new DeepSeekToolCall
                {
                    Id = toolCall.Id,
                    Function = new DeepSeekFunctionCall
                    {
                        Name = toolCall.Name,
                        Arguments = toolCall.ArgumentsJson
                    }
                })
                .ToList();
        }

        return providerMessage;
    }

    /// <summary>
    /// 将应用内部的工具定义转换为 DeepSeek function calling 协议模型。
    /// </summary>
    private static DeepSeekTool CreateToolDefinition(ToolDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var function = new DeepSeekFunctionDefinition
        {
            Name = definition.Name,
            Description = definition.Description
        };

        if (definition.Parameters.Count > 0)
        {
            var parameters = new DeepSeekFunctionParameters();
            var requiredParameters = new List<string>();

            foreach (var parameter in definition.Parameters)
            {
                parameters.Properties.Add(parameter.Name, new DeepSeekFunctionParameter
                {
                    Type = parameter.Type,
                    Description = parameter.Description
                });

                if (parameter.IsRequired)
                {
                    requiredParameters.Add(parameter.Name);
                }
            }

            if (requiredParameters.Count > 0)
            {
                parameters.Required = requiredParameters;
            }

            function.Parameters = parameters;
        }

        return new DeepSeekTool
        {
            Function = function
        };
    }

    /// <summary>
    /// 解析 DeepSeek SSE 数据块中的增量文本和结束状态。
    /// </summary>
    public ChatChunk? Deserialize(string payload)
    {
        if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
        {
            return new ChatChunk
            {
                CompletionReason = ChatCompletionReason.Stop
            };
        }

        try
        {
            var response = JsonSerializer.Deserialize<DeepSeekChatResponse>(payload);
            var choice = response?.Choices.FirstOrDefault();

            if (choice is null)
            {
                return null;
            }

            var toolCallDeltas = choice.Delta.ToolCalls
                .Select(toolCall => new ToolCallDelta
                {
                    Index = toolCall.Index,
                    Id = toolCall.Id,
                    Name = toolCall.Function?.Name,
                    ArgumentsFragment = toolCall.Function?.Arguments
                })
                .ToArray();

            return new ChatChunk
            {
                Content = choice.Delta.Content ?? string.Empty,
                ToolCallDeltas = toolCallDeltas,
                CompletionReason = choice.FinishReason switch
                {
                    "tool_calls" => ChatCompletionReason.ToolCalls,
                    null or "" => ChatCompletionReason.None,
                    _ => ChatCompletionReason.Stop
                }
            };
        }
        catch (JsonException)
        {
            // 忽略无法解析的 SSE 数据块，由 ChatService 继续读取后续内容。
            return null;
        }
    }
}
