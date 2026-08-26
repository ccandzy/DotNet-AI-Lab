using System.Data;
using System.Globalization;
using System.Text.Json;
using AiChatClient.Models.Tools;

namespace AiChatClient.Services.Tools.Impl;

/// <summary>
/// 提供基础算术表达式计算能力。
/// </summary>
public sealed class CalculatorTool : ITool
{
    public ToolDefinition Definition { get; } = new()
    {
        Name = "calculate",
        Description = "计算一个基础算术表达式，例如：1 + 2 * 3。",
        Parameters =
        [
            new ToolParameterDefinition
            {
                Name = "expression",
                Description = "需要计算的算术表达式。",
                Type = "string",
                IsRequired = true
            }
        ]
    };

    public Task<ToolResult> ExecuteAsync(
        ToolCall toolCall,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(toolCall.Name, Definition.Name, StringComparison.Ordinal))
        {
            return Task.FromResult(CreateErrorResult(toolCall, $"不支持的工具调用：{toolCall.Name}。"));
        }

        try
        {
            using var arguments = JsonDocument.Parse(toolCall.ArgumentsJson);
            if (arguments.RootElement.ValueKind != JsonValueKind.Object
                || !arguments.RootElement.TryGetProperty("expression", out var expressionElement)
                || expressionElement.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(expressionElement.GetString()))
            {
                return Task.FromResult(CreateErrorResult(toolCall, "缺少有效的 expression 参数。"));
            }

            var expression = expressionElement.GetString()!;
            var value = new DataTable().Compute(expression, null);
            var content = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            return Task.FromResult(new ToolResult
            {
                ToolCallId = toolCall.Id,
                Content = content
            });
        }
        catch (JsonException)
        {
            return Task.FromResult(CreateErrorResult(toolCall, "工具参数不是有效的 JSON。"));
        }
        catch (EvaluateException)
        {
            return Task.FromResult(CreateErrorResult(toolCall, "无法计算该表达式。"));
        }
        catch (SyntaxErrorException)
        {
            return Task.FromResult(CreateErrorResult(toolCall, "表达式语法无效。"));
        }
    }

    private static ToolResult CreateErrorResult(ToolCall toolCall, string message)
    {
        return new ToolResult
        {
            ToolCallId = toolCall.Id,
            Content = message,
            IsError = true
        };
    }
}
