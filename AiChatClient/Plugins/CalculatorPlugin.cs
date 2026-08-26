using System.ComponentModel;
using System.Data;
using System.Globalization;
using Microsoft.SemanticKernel;

namespace AiChatClient.Plugins;

public sealed class CalculatorPlugin
{
    [KernelFunction("calculate")]
    [Description("计算一个基础算术表达式，例如：1 + 2 * 3。")]
    public string Calculate(
        [Description("需要计算的基础算术表达式。")] string expression,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return "缺少有效的 expression 参数。";
        }

        try
        {
            var value = new DataTable().Compute(expression, null);
            return Convert.ToString(value, CultureInfo.InvariantCulture)
                ?? string.Empty;
        }
        catch (EvaluateException)
        {
            return "无法计算该表达式。";
        }
        catch (SyntaxErrorException)
        {
            return "表达式语法无效。";
        }
    }
}
