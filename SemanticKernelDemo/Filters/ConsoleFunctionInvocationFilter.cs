using Microsoft.SemanticKernel;

namespace SemanticKernelDemo.Filters;

/// <summary>
/// 记录 Function 调用前后的信息，让 SK 自动 Function Calling 不再是“黑盒”。
/// </summary>
public sealed class ConsoleFunctionInvocationFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // context 包含本次调用的 Function、参数和（执行后）结果。
        var arguments = context.Arguments.Count == 0
            ? "（无参数）"
            : string.Join(", ", context.Arguments.Select(pair => $"{pair.Key}={pair.Value}"));

        Console.WriteLine();
        Console.WriteLine($"[SK] 即将调用：{context.Function.PluginName}.{context.Function.Name}");
        Console.WriteLine($"[SK] 参数：{arguments}");

        // next(context) 才是真正执行 Plugin 方法的位置。
        await next(context);

        Console.WriteLine($"[SK] 返回值：{context.Result?.GetValue<object?>() ?? "(null)"}");
    }
}
