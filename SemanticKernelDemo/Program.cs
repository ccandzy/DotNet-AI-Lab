using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticKernelDemo.Filters;
using SemanticKernelDemo.Plugins;

// 默认只运行离线部分；必须显式传入 --deepseek 才会产生一次联网请求。
var runDeepSeekDemo = args.Contains("--deepseek", StringComparer.OrdinalIgnoreCase);

Console.WriteLine("=== Semantic Kernel 最小学习示例 ===");
Console.WriteLine();

await RunDirectInvocationAsync();

if (runDeepSeekDemo)
{
    await RunDeepSeekFunctionCallingAsync();
}
else
{
    Console.WriteLine();
    Console.WriteLine("联网示例未执行。运行下列命令以执行 DeepSeek 自动工具调用：");
    Console.WriteLine("dotnet run --project .\\SemanticKernelDemo -- --deepseek");
}

static async Task RunDirectInvocationAsync()
{
    Console.WriteLine("--- 第一层：离线直接调用本地 Function ---");

    // Kernel 是 SK 的运行时容器。这里没有注册任何 AI 服务，仍可以调用本地 Function。
    var kernel = Kernel.CreateBuilder().Build();

    // Filter 类似 ASP.NET Core 的中间件：在每一次 Function 调用前后执行。
    // 本示例用它把原本在 SK 内部发生的调用过程打印到控制台。
    kernel.FunctionInvocationFilters.Add(new ConsoleFunctionInvocationFilter());

    // Plugin 是一组能力的容器；AddFromType 会扫描 TimePlugin 中的 [KernelFunction] 方法。
    var timePlugin = kernel.Plugins.AddFromType<TimePlugin>("Time");

    // Function 是 Plugin 中一个具体、可执行的能力。
    var getCurrentTime = timePlugin["get_current_time"];

    Console.WriteLine($"Kernel 已注册 Plugin：{timePlugin.Name}");
    Console.WriteLine($"Function：{getCurrentTime.Name}");
    Console.WriteLine($"说明：{getCurrentTime.Description}");
    Console.WriteLine("直接调用：kernel.InvokeAsync(function)");

    // 这里是“直接调用”：由代码明确指定要运行哪个 Function，完全不需要 LLM。
    var result = await kernel.InvokeAsync(getCurrentTime);
    Console.WriteLine($"直接调用结果：{result.GetValue<string>()}");
}

static async Task RunDeepSeekFunctionCallingAsync()
{
    Console.WriteLine();
    Console.WriteLine("--- 第二层：DeepSeek 自动 Function Calling ---");

    // 公共配置给出 endpoint 和模型；本地配置仅放 API Key，且被 Git 忽略。
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Local.json", optional: true)
        .Build();

    var deepSeekSection = configuration.GetRequiredSection("DeepSeek");
    var options = new DeepSeekOptions
    {
        Endpoint = deepSeekSection["Endpoint"]
            ?? throw new InvalidOperationException("未配置 DeepSeek:Endpoint。"),
        ModelId = deepSeekSection["ModelId"]
            ?? throw new InvalidOperationException("未配置 DeepSeek:ModelId。"),
        ApiKey = deepSeekSection["ApiKey"]
    };

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        Console.WriteLine("未配置 DeepSeek API Key，因此跳过联网示例。");
        Console.WriteLine("请复制 appsettings.Local.json.example 为 appsettings.Local.json，并填写 ApiKey。");
        return;
    }

    var builder = Kernel.CreateBuilder();

    // DeepSeek 提供 OpenAI-compatible API，因此可以直接使用 SK 的 OpenAI connector。
    builder.AddOpenAIChatCompletion(
        modelId: options.ModelId,
        endpoint: new Uri(options.Endpoint),
        apiKey: options.ApiKey);

    var kernel = builder.Build();
    kernel.FunctionInvocationFilters.Add(new ConsoleFunctionInvocationFilter());

    // 仍然注册同一个本地 Plugin。区别仅在于：本次由模型决定是否调用它。
    kernel.Plugins.AddFromType<TimePlugin>("Time");

    // IChatCompletionService 是 SK 对聊天模型服务的统一抽象。
    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

    // ChatHistory 是本次请求的对话上下文；此最小示例只放一条用户消息。
    var history = new ChatHistory("你是一个简洁的助手。需要当前时间时，调用提供的工具。");
    history.AddUserMessage("请告诉我现在的本地时间，并说明它使用的格式。");

    var settings = new OpenAIPromptExecutionSettings
    {
        // Auto 的意思是：SK 将 Plugin Function 的定义给模型；模型要求调用时，SK 会执行它，
        // 再把结果回传给模型，直到模型返回最终普通文本。调用循环由 SK 维护。
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        ExtensionData = new Dictionary<string, object>
        {
            // 此自定义 DeepSeek 请求字段关闭 thinking，避免示例需要处理 reasoning_content 回传。
            ["thinking"] = new { type = "disabled" }
        }
    };

    Console.WriteLine("请求 DeepSeek：请告诉我现在的本地时间，并说明它使用的格式。");
    Console.WriteLine("Semantic Kernel 将自动处理模型发起的 Function Calling 循环。");

    // 这一行触发整个自动调用流程。调用日志由 ConsoleFunctionInvocationFilter 输出。
    var response = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: settings,
        kernel: kernel);


    Console.WriteLine();
    Console.WriteLine($"DeepSeek 最终回答：{response.Content}");
}

internal sealed class DeepSeekOptions
{
    // 只承载本示例需要的三项配置，不与 WPF 主项目的配置类型共享。
    public required string Endpoint { get; init; }

    public required string ModelId { get; init; }

    public string? ApiKey { get; init; }
}
