using System.Globalization;
using AiChatClient.Models.Tools;

namespace AiChatClient.Services.Tools.Impl;

/// <summary>
/// 提供当前本地时间。
/// </summary>
public sealed class CurrentTimeTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Name = "current_time",
        Description = "获取当前本地时间，结果使用 ISO 8601 格式并包含时区偏移。"
    };

    public Task<ToolResult> ExecuteAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(toolCall.Name, Definition.Name, StringComparison.Ordinal))
        {
            return Task.FromResult(new ToolResult
            {
                ToolCallId = toolCall.Id,
                Content = $"不支持的工具调用：{toolCall.Name}。",
                IsError = true
            });
        }

        return Task.FromResult(new ToolResult
        {
            ToolCallId = toolCall.Id,
            Content = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture)
        });
    }
}
