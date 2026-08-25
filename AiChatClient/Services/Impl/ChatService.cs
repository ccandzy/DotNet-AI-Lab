using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using AiChatClient.Dtos;
using AiChatClient.Models.Tools;
using AiChatClient.Services.Tools;
using Models;
using Services;

namespace AiChatClient.Services.Impl;

/// <summary>
/// 负责发送聊天请求，并编排 Provider 返回的工具调用。
/// </summary>
public class ChatService : IChatService
{
    private const int MaximumToolRounds = 8;
    private readonly IChatProviderResolver _chatProviderResolver;
    private readonly IToolResolver _toolResolver;
    private readonly HttpClient _httpClient;

    public ChatService(
        IChatProviderResolver chatProviderResolver,
        IToolResolver toolResolver,
        HttpClient httpClient)
    {
        _chatProviderResolver = chatProviderResolver;
        _toolResolver = toolResolver;
        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<ChatStreamEvent> SendStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestMessages = request.Messages.ToList();

        for (var toolRound = 0; toolRound < MaximumToolRounds; toolRound++)
        {
            var roundRequest = new ChatRequest
            {
                Messages = requestMessages,
                Provider = request.Provider,
                Model = request.Model,
                Settings = request.Settings,
                Tools = request.Tools
            };

            var toolCallAccumulators = new Dictionary<int, ToolCallAccumulator>();
            var assistantContent = new StringBuilder();
            var completionReason = ChatCompletionReason.None;

            await foreach (var chunk in SendRequestStreamingAsync(roundRequest, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    assistantContent.Append(chunk.Content);
                    yield return new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.TextDelta,
                        Content = chunk.Content
                    };
                }

                foreach (var toolCallDelta in chunk.ToolCallDeltas)
                {
                    if (!toolCallAccumulators.TryGetValue(toolCallDelta.Index, out var accumulator))
                    {
                        accumulator = new ToolCallAccumulator();
                        toolCallAccumulators.Add(toolCallDelta.Index, accumulator);
                    }

                    accumulator.Append(toolCallDelta);
                }

                if (chunk.CompletionReason != ChatCompletionReason.None)
                {
                    completionReason = chunk.CompletionReason;
                }
            }

            if (completionReason != ChatCompletionReason.ToolCalls)
            {
                yield break;
            }

            var toolCalls = toolCallAccumulators
                .OrderBy(pair => pair.Key)
                .Select(pair => pair.Value.ToToolCall(pair.Key))
                .ToArray();

            if (toolCalls.Length == 0)
            {
                throw new InvalidOperationException("DeepSeek 返回了 tool_calls 结束状态，但未包含有效工具调用。");
            }

            requestMessages.Add(new ChatRequestMessage
            {
                Role = ChatRole.Assistant,
                Content = assistantContent.ToString(),
                ToolCalls = toolCalls
            });

            foreach (var toolCall in toolCalls)
            {
                yield return new ChatStreamEvent
                {
                    Type = ChatStreamEventType.ToolCalling,
                    ToolName = toolCall.Name
                };

                var toolResult = await ExecuteToolAsync(toolCall, cancellationToken);
                requestMessages.Add(new ChatRequestMessage
                {
                    Role = ChatRole.Tool,
                    Content = toolResult.Content,
                    ToolCallId = toolResult.ToolCallId
                });
            }
        }

        throw new InvalidOperationException($"工具调用超过最大子轮次数 {MaximumToolRounds}。");
    }

    private async Task<ToolResult> ExecuteToolAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken)
    {
        var tool = _toolResolver.Resolve(toolCall.Name);
        if (tool is null)
        {
            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                Content = $"未找到工具：{toolCall.Name}。",
                IsError = true
            };
        }

        try
        {
            return await tool.ExecuteAsync(toolCall, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                ToolCallId = toolCall.Id,
                Content = $"工具 {toolCall.Name} 执行失败：{ex.Message}",
                IsError = true
            };
        }
    }

    private async IAsyncEnumerable<ChatChunk> SendRequestStreamingAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var chatProvider = _chatProviderResolver.Resolve(request.Provider);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, chatProvider.ApiChatUrl)
        {
            Content = chatProvider.CreateHttpContent(request)
        };

        chatProvider.ConfigureRequest(httpRequest);

        var requestContent = httpRequest.Content is null
            ? string.Empty
            : await httpRequest.Content.ReadAsStringAsync(cancellationToken);
        Debug.WriteLine($"request.Content:{requestContent}");

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            Debug.WriteLine($"response.StatusCode:{(int)response.StatusCode} {response.ReasonPhrase}");
            Debug.WriteLine($"response.ErrorContent:{errorContent}");
        }

        response.EnsureSuccessStatusCode();

        await using var result = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(result);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Debug.WriteLine($"response.Chunk:{line}");

            var payload = line.Trim();
            if (payload.StartsWith("data: ", StringComparison.Ordinal))
            {
                payload = payload["data: ".Length..];
            }

            if (string.Equals(payload, "[DONE]", StringComparison.OrdinalIgnoreCase))
            {
                yield break;
            }

            var chunk = chatProvider.Deserialize(payload);
            if (chunk is not null)
            {
                yield return chunk;
            }
        }
    }

    private sealed class ToolCallAccumulator
    {
        private readonly StringBuilder _arguments = new();
        private string? _id;
        private string? _name;

        public void Append(ToolCallDelta delta)
        {
            _id ??= delta.Id;
            _name ??= delta.Name;

            if (!string.IsNullOrEmpty(delta.ArgumentsFragment))
            {
                _arguments.Append(delta.ArgumentsFragment);
            }
        }

        public ToolCall ToToolCall(int index)
        {
            if (string.IsNullOrWhiteSpace(_id) || string.IsNullOrWhiteSpace(_name))
            {
                throw new InvalidOperationException($"工具调用片段 {index} 缺少 Id 或名称。");
            }

            return new ToolCall
            {
                Id = _id,
                Name = _name,
                ArgumentsJson = _arguments.Length == 0 ? "{}" : _arguments.ToString()
            };
        }
    }
}
