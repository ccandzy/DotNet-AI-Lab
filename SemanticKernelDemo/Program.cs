using System.ClientModel;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;
using SemanticKernelDemo.Documents;
using SemanticKernelDemo.Filters;
using SemanticKernelDemo.Plugins;

// args 是用户在命令行中传给程序的参数。
// 例如执行 “dotnet run ... -- --embedding” 时，args 中就会包含 "--embedding"。
// 默认只运行离线示例；只有显式传入下面某个参数，程序才会访问 DeepSeek 或 Ollama。
var runDeepSeekDemo = args.Contains("--deepseek", StringComparer.OrdinalIgnoreCase);
var runEmbeddingDemo = args.Contains("--embedding", StringComparer.OrdinalIgnoreCase);
var runVectorSearchDemo = args.Contains("--vector-search", StringComparer.OrdinalIgnoreCase);
var runMarkdownChunkerDemo = args.Contains("--markdown-chunker", StringComparer.OrdinalIgnoreCase);

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
    Console.WriteLine("DeepSeek 示例未执行。运行下列命令以执行自动工具调用：");
    Console.WriteLine("dotnet run --project .\\SemanticKernelDemo -- --deepseek");
}

if (runEmbeddingDemo)
{
    try
    {
        await RunEmbeddingDemoAsync();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Embedding 实验失败：{exception.Message}");

        // 非 0 退出码表示程序没有成功完成，方便脚本或 CI 判断运行结果。
        Environment.ExitCode = 1;
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("Embedding 示例未执行。运行下列命令以调用 Ollama Embedding 模型：");
    Console.WriteLine("dotnet run --project .\\SemanticKernelDemo -- --embedding");
}

if (runVectorSearchDemo)
{
    try
    {
        await RunVectorSearchDemoAsync();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"向量检索实验失败：{exception.Message}");
        Environment.ExitCode = 1;
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("向量检索示例未执行。运行下列命令以测试内存知识库 TopK 检索：");
    Console.WriteLine("dotnet run --project .\\SemanticKernelDemo -- --vector-search");
}

if (runMarkdownChunkerDemo)
{
    try
    {
        await RunMarkdownChunkerDemoAsync();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine();
        Console.Error.WriteLine($"Markdown Chunker 实验失败：{exception.Message}");
        Environment.ExitCode = 1;
    }
}
else
{
    Console.WriteLine();
    Console.WriteLine("Markdown Chunker 示例未执行。运行下列命令以观察 Markdown 切块过程：");
    Console.WriteLine("dotnet run --project .\\SemanticKernelDemo -- --markdown-chunker");
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

static async Task RunEmbeddingDemoAsync()
{
    Console.WriteLine();
    Console.WriteLine("--- 第三层：Ollama 最小 Embedding 实验 ---");

    // CreateEmbeddingSetup 负责读取配置、创建 OpenAI-compatible 客户端并注册 SK 服务。
    // 这里不关心连接细节，只从 Kernel 中取出已经准备好的 Embedding 生成器。
    var setup = CreateEmbeddingSetup();
    var options = setup.Options;

    // 泛型可以这样读：
    //   string           = 输入是一段文本；
    //   Embedding<float> = 输出是由 float 数字组成的向量。
    var embeddingGenerator =
        setup.Kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    // A、B 表达的语义相近，C 与它们无关。
    // 这个刻意设计的数据集用来验证：Embedding 是否真的能表达“语义距离”。
    string[] inputs =
    [
        "Semantic Kernel supports function calling.",
        "SK can automatically call tools.",
        "I like eating apples."
    ];

    Console.WriteLine($"Endpoint：{options.Endpoint}");
    Console.WriteLine($"Model：{options.ModelId}");
    Console.WriteLine("一次请求生成 A、B、C 三个向量，不保存任何数据。");

    // 一次批量请求三段文本。返回集合与 inputs 的顺序一一对应：
    // embeddings[0] 属于 A，embeddings[1] 属于 B，embeddings[2] 属于 C。
    var embeddings = await GenerateEmbeddingsAsync(
        embeddingGenerator,
        inputs,
        options,
        "A、B、C 文本");

    for (var index = 0; index < embeddings.Count; index++)
    {
        var label = (char)('A' + index);

        // Vector 才是真正的浮点数向量。当前 Qwen 模型会返回 1024 个 float。
        var vector = embeddings[index].Vector;

        // 完整输出 1024 个数字太长，所以只展示前 5 个用于观察数据格式。
        // 这里只截断“显示内容”，后面的相似度计算仍使用完整向量。
        var previewLength = Math.Min(5, vector.Length);
        var preview = string.Join(
            ", ",
            vector.Span[..previewLength]
                .ToArray()
                .Select(value => value.ToString("F6", CultureInfo.InvariantCulture)));

        Console.WriteLine();
        Console.WriteLine($"{label}：{inputs[index]}");
        Console.WriteLine($"维度：{vector.Length}");
        Console.WriteLine($"前 {previewLength} 个值：[{preview}]");
    }

    // .Span 提供一个不复制数据的只读窗口，让 CosineSimilarity 可以遍历完整向量。
    // 这里分别验证“语义相近”和“语义无关”两种组合。
    var similarityAB = CosineSimilarity(
        embeddings[0].Vector.Span,
        embeddings[1].Vector.Span);
    var similarityAC = CosineSimilarity(
        embeddings[0].Vector.Span,
        embeddings[2].Vector.Span);

    Console.WriteLine();
    Console.WriteLine($"CosineSimilarity(A, B)：{similarityAB:F4}");
    Console.WriteLine($"CosineSimilarity(A, C)：{similarityAC:F4}");
    Console.WriteLine(
        similarityAB > similarityAC
            ? "实验结论：通过。语义相近的 A、B 比 A、C 更相似。"
            : "实验结论：未达到预期。A、B 的相似度没有高于 A、C。");
}

static async Task RunVectorSearchDemoAsync()
{
    // TopK 表示只保留相似度最高的前 K 条文档。
    // 当前知识库只有 8 条，显示前 3 条就足以观察排序效果。
    const int topK = 3;

    Console.WriteLine();
    Console.WriteLine("--- 第四层：内存知识库 TopK 向量检索 ---");

    var setup = CreateEmbeddingSetup();
    var embeddingGenerator =
        setup.Kernel.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    // -------------------- 准备知识库文档 --------------------
    // 目前没有数据库或文件系统，知识库就是这个内存数组。
    // Id 用来标识文档，Content 是之后要转换成向量的原始文本。
    KnowledgeDocument[] documents =
    [
        new("Document 1", "Semantic Kernel supports automatic function calling."),
        new("Document 2", "GenerationSettings contains Temperature, TopP and MaxTokens."),
        new("Document 3", "EF Core and SQLite are used to persist conversations and chat messages."),
        new("Document 4", "Ollama can run local AI models and expose them through an HTTP API."),
        new("Document 5", "Qwen3 Embedding converts text into semantic vectors."),
        new("Document 6", "Cosine similarity measures how similar two embedding vectors are."),
        new("Document 7", "RAG retrieves relevant documents before asking the chat model to generate an answer."),
        new("Document 8", "An apple is a kind of fruit.")
    ];

    // -------------------- 准备测试问题 --------------------
    // ExpectedDocumentId 仅用于验证实验结果，不参与相似度计算和排序。
    // 真正检索时，程序只看 Query 文本与 Document 向量之间的数学相似度。
    KnowledgeQuery[] queries =
    [
        new("How are conversations stored?", "Document 3"),
        new("Which settings control model generation?", "Document 2"),
        new("How can I run an AI model locally?", "Document 4"),
        new("How do we compare two text vectors?", "Document 6"),
        new("What fruit can people eat?", "Document 8")
    ];

    Console.WriteLine($"Endpoint：{setup.Options.Endpoint}");
    Console.WriteLine($"Model：{setup.Options.ModelId}");
    Console.WriteLine($"TopK：{topK}");
    Console.WriteLine();
    Console.WriteLine($"[文档入库] 批量生成 {documents.Length} 条知识库内容的向量……");

    // ==================== 阶段一：文档入库 ====================
    // 真实 RAG 会在资料新增或更新时执行这一阶段：
    // Document 文本 → Embedding 模型 → Document 向量 → 保存。
    // Select 只取每条文档的 Content，再用 ToArray 组成字符串数组发给模型。
    var documentEmbeddings = await GenerateEmbeddingsAsync(
        embeddingGenerator,
        documents.Select(document => document.Content).ToArray(),
        setup.Options,
        "知识库文档");

    // documents 和 documentEmbeddings 的顺序相同，所以可以用同一个 index 配对：
    // documents[0] + documentEmbeddings[0]，documents[1] + documentEmbeddings[1]……
    // 配对后得到的 EmbeddedKnowledgeDocument 同时拥有 Id、原文和向量。
    var knowledgeBase = documents
        .Select((document, index) => new EmbeddedKnowledgeDocument(
            document.Id,
            document.Content,
            documentEmbeddings[index].Vector))
        .ToArray();

    Console.WriteLine(
        $"[文档入库] 完成：{knowledgeBase.Length} 条文档已存入内存，向量维度为 {knowledgeBase[0].Vector.Length}。");
    Console.WriteLine($"[查询检索] 批量生成 {queries.Length} 个 Query 的向量……");

    // ==================== 阶段二：查询向量化 ====================
    // Query 必须使用与文档完全相同的 Embedding 模型，否则两组向量不能可靠比较。
    // 为了减少 HTTP 请求次数，这里一次批量生成 5 个 Query 的向量。
    var queryEmbeddings = await GenerateEmbeddingsAsync(
        embeddingGenerator,
        queries.Select(query => query.Text).ToArray(),
        setup.Options,
        "查询文本");

    // 记录有多少个 Query 的第一名符合我们的人工预期。
    var expectedTop1Count = 0;

    // 每次循环处理一个 Query：取向量、与全部文档比较、排序并输出 TopK。
    for (var queryIndex = 0; queryIndex < queries.Length; queryIndex++)
    {
        var query = queries[queryIndex];
        var queryVector = queryEmbeddings[queryIndex].Vector;

        // 下面这段 LINQ 就是当前“向量搜索引擎”的核心：
        // 1. Select：Query 与知识库中的每条文档都计算一次余弦相似度；
        // 2. OrderByDescending：分数从高到低排序；
        // 3. Take(topK)：只取前 K 条；
        // 4. ToArray：立即执行上述操作，并把结果保存成数组。
        // 当前只有 8 条文档，全量比较很简单；大型知识库才需要专门的向量数据库。
        var results = knowledgeBase
            .Select(document => new VectorSearchResult(
                document,
                CosineSimilarity(queryVector.Span, document.Vector.Span)))
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToArray();

        // results[0] 是排序后的第一名，也就是 Top1。
        // 这一步只是自动检查学习实验是否符合预期，不会改变搜索结果。
        var matchesExpectation = string.Equals(
            results[0].Document.Id,
            query.ExpectedDocumentId,
            StringComparison.Ordinal);

        if (matchesExpectation)
        {
            expectedTop1Count++;
        }

        Console.WriteLine();
        Console.WriteLine($"Query {queryIndex + 1}：{query.Text}");
        Console.WriteLine($"预期 Top1：{query.ExpectedDocumentId}");

        for (var rank = 0; rank < results.Length; rank++)
        {
            var result = results[rank];
            Console.WriteLine(
                $"Top {rank + 1} | {result.Document.Id} | Score={result.Score:F4} | {result.Document.Content}");
        }

        Console.WriteLine(
            matchesExpectation
                ? "Top1 验证：通过"
                : $"Top1 验证：未达到预期，实际为 {results[0].Document.Id}");
    }

    Console.WriteLine();
    Console.WriteLine($"检索总结：{expectedTop1Count}/{queries.Length} 个 Query 的 Top1 符合预期。");
    Console.WriteLine("所有文档及向量仅保存在本次进程的内存中，程序退出后即释放。");
}

static async Task RunMarkdownChunkerDemoAsync()
{
    Console.WriteLine();
    Console.WriteLine("--- 第五层：MarkdownChunker 流程验证 ---");

    // 这个验证完全是本地文件处理，不会访问 DeepSeek、Ollama 或向量数据库。
    // csproj 会把样本文档复制到输出目录，所以使用 AppContext.BaseDirectory 可以稳定找到它。
    var samplePath = Path.Combine(
        AppContext.BaseDirectory,
        "Documents",
        "MarkdownChunkerSample1.md");

    // 为了让一个很短的样本文档也能明显产生多个 Chunk，这里使用较小的上限。
    // MarkdownChunker 要求 maxChunkCharacters 不能小于 200。
    const int maxChunkCharacters = 300;
    const int overlapCharacters = 100;

    Console.WriteLine($"样本文档：{samplePath}");
    Console.WriteLine($"目标 Chunk 上限：{maxChunkCharacters} 字符");
    Console.WriteLine($"目标重叠：{overlapCharacters} 字符（只复制完整段落）");
    Console.WriteLine();

    // 整个 Chunking 数据流只有这一行入口：
    // Markdown 文件 → 识别章节/段落/受保护块 → 合并并重叠 → MarkdownChunk 集合。
    var chunks = await MarkdownChunker.ReadAndChunkMarkdownAsync(
        samplePath,
        maxChunkCharacters,
        overlapCharacters);

    Console.WriteLine($"切分完成，共得到 {chunks.Count} 个 Chunk。");

    // 逐个打印 Chunk，便于用肉眼观察标题路径、行号和内容边界。
    foreach (var chunk in chunks)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"Chunk {chunk.Index}");
        Console.WriteLine($"HeadingPath：{chunk.HeadingPath}");
        Console.WriteLine($"Source Lines：{chunk.StartLine}-{chunk.EndLine}");
        Console.WriteLine($"Characters：{chunk.Content.Length}");
        Console.WriteLine("Content：");
        Console.WriteLine(chunk.Content);
    }

    // 除了肉眼看结果，还做一组自动断言式检查。
    // 每项检查只验证一种职责，失败时容易判断是哪条 Chunking 规则出了问题。
    var fullPath = Path.GetFullPath(samplePath);
    var sourceLineCount = (await File.ReadAllLinesAsync(samplePath)).Length;
    const string overlapMarker = "OVERLAP_MARKER：这段完整的小段落应同时出现在相邻的两个 Chunk 中。";

    var hasAdjacentOverlap = Enumerable.Range(0, Math.Max(0, chunks.Count - 1))
        .Any(index =>
            chunks[index].Content.Contains(overlapMarker, StringComparison.Ordinal)
            && chunks[index + 1].Content.Contains(overlapMarker, StringComparison.Ordinal));

    // 结束围栏后允许继续出现普通正文，所以不能简单检查 Chunk 是否以 ``` 结尾。
    // 正确检查方式是：先找到开始围栏，再确认后面还能找到一个结束围栏。
    var hasCompleteCodeFence = chunks.Any(chunk =>
    {
        var openingFenceIndex = chunk.Content.IndexOf("```csharp", StringComparison.Ordinal);
        if (openingFenceIndex < 0)
        {
            return false;
        }

        var closingFenceIndex = chunk.Content.IndexOf(
            "```",
            openingFenceIndex + "```csharp".Length,
            StringComparison.Ordinal);

        return closingFenceIndex > openingFenceIndex
            && chunk.Content.Contains("# 这不是 Markdown 标题", StringComparison.Ordinal);
    });

    ChunkerValidationResult[] validations =
    [
        new(
            "生成了多个 Chunk",
            chunks.Count > 1,
            $"实际数量：{chunks.Count}"),
        new(
            "Chunk 编号从 1 开始且连续",
            chunks.Select((chunk, index) => chunk.Index == index + 1).All(result => result),
            "Index 应依次为 1、2、3……"),
        new(
            "每个 Chunk 都保留绝对来源路径",
            chunks.All(chunk => string.Equals(chunk.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)),
            fullPath),
        new(
            "行号范围有效",
            chunks.All(chunk =>
                chunk.StartLine >= 1
                && chunk.EndLine >= chunk.StartLine
                && chunk.EndLine <= sourceLineCount),
            $"源文件共 {sourceLineCount} 行"),
        new(
            "内容均不为空",
            chunks.All(chunk => !string.IsNullOrWhiteSpace(chunk.Content)),
            "每个 Content 都应包含可检索文本"),
        new(
            "YAML Front Matter 被识别为独立章节",
            chunks.Any(chunk => chunk.HeadingPath.EndsWith("YAML Front Matter", StringComparison.Ordinal)),
            "HeadingPath 应包含 YAML Front Matter"),
        new(
            "嵌套标题路径被完整保留",
            chunks.Any(chunk => string.Equals(
                chunk.HeadingPath,
                "MarkdownChunker 验证文档 > 嵌套标题 > 检索元数据",
                StringComparison.Ordinal)),
            "应保留 # > ## > ### 的层级"),
        new(
            "代码围栏保持完整",
            hasCompleteCodeFence,
            "代码中的 # 不能被识别成标题，开始和结束围栏必须在同一 Chunk"),
        new(
            "Markdown 表格保持完整",
            chunks.Any(chunk =>
                chunk.Content.Contains("| 配置项 | 作用 |", StringComparison.Ordinal)
                && chunk.Content.Contains("| --- | --- |", StringComparison.Ordinal)
                && chunk.Content.Contains("| MaxTokens | 限制最大输出长度 |", StringComparison.Ordinal)),
            "表头、分隔行和数据行必须在同一 Chunk"),
        new(
            "相邻 Chunk 保留完整段落重叠",
            hasAdjacentOverlap,
            overlapMarker)
    ];

    Console.WriteLine();
    Console.WriteLine(new string('=', 72));
    Console.WriteLine("自动验证结果：");

    foreach (var validation in validations)
    {
        Console.WriteLine(
            $"[{(validation.Passed ? "通过" : "失败")}] {validation.Name} —— {validation.Detail}");
    }

    var passedCount = validations.Count(validation => validation.Passed);
    Console.WriteLine();
    Console.WriteLine($"验证总结：{passedCount}/{validations.Length} 项通过。");

    if (passedCount != validations.Length)
    {
        throw new InvalidOperationException("存在未通过的 Chunking 规则，请检查上面的失败项。");
    }
}

static EmbeddingSetup CreateEmbeddingSetup()
{
    // AppContext.BaseDirectory 是程序编译后的运行目录。
    // csproj 会把 appsettings*.json 复制到这里，因此无论从哪里启动都能找到配置。
    var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)

        // 公共配置必须存在；本地配置可选，并且后加载，所以同名配置会覆盖公共配置。
        .AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile("appsettings.Local.json", optional: true)
        .Build();

    // 读取 appsettings.json 中的 Embedding 节点，并转换成强类型配置对象。
    var embeddingSection = configuration.GetRequiredSection("Embedding");
    var options = new EmbeddingOptions
    {
        Endpoint = embeddingSection["Endpoint"]
            ?? throw new InvalidOperationException("未配置 Embedding:Endpoint。"),
        ModelId = embeddingSection["ModelId"]
            ?? throw new InvalidOperationException("未配置 Embedding:ModelId。"),
        ApiKey = embeddingSection["ApiKey"]
            ?? throw new InvalidOperationException("未配置 Embedding:ApiKey。")
    };

    // 在真正发起网络请求前先检查配置，错误信息会比 SDK 的底层异常更容易理解。
    if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
    {
        throw new InvalidOperationException(
            $"Embedding:Endpoint 不是有效的绝对地址：{options.Endpoint}");
    }

    if (string.IsNullOrWhiteSpace(options.ModelId))
    {
        throw new InvalidOperationException("Embedding:ModelId 不能为空。");
    }

    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        throw new InvalidOperationException("Embedding:ApiKey 不能为空；Ollama 可使用占位值 ollama。");
    }

    // Ollama 提供 OpenAI-compatible /v1/embeddings 接口，所以可以复用 OpenAI 客户端。
    // Endpoint 指向 Ollama 而不是 OpenAI 云端。
    // ApiKeyCredential 是 OpenAI 客户端的必填项，但本地 Ollama 不校验 "ollama" 这个占位值。
    var openAIClient = new OpenAIClient(
        new ApiKeyCredential(options.ApiKey),
        new OpenAIClientOptions { Endpoint = endpoint });

    // Kernel Builder 用来注册 AI 服务；Build 后得到真正的 Kernel 容器。
    var builder = Kernel.CreateBuilder();
#pragma warning disable SKEXP0010 // SK 1.80 的 Embedding 注册 API 仍标记为实验性。

    // 把“使用哪个模型、通过哪个客户端调用”注册成统一的 IEmbeddingGenerator 服务。
    builder.AddOpenAIEmbeddingGenerator(
        modelId: options.ModelId,
        openAIClient: openAIClient);
#pragma warning restore SKEXP0010

    // 同时返回 Kernel 和配置：调用方从 Kernel 获取服务，并用配置打印 Endpoint/Model。
    var kernel = builder.Build();
    return new EmbeddingSetup(kernel, options);
}

static async Task<GeneratedEmbeddings<Embedding<float>>> GenerateEmbeddingsAsync(
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    IReadOnlyList<string> inputs,
    EmbeddingOptions options,
    string inputDescription)
{
    // 这是最小实验和 TopK 实验共用的“批量生成向量”方法。
    // 一段输入文本应对应一个输出向量，因此返回数量必须与 inputs.Count 相同。
    GeneratedEmbeddings<Embedding<float>> embeddings;
    try
    {
        // await 表示等待远端 Ollama 计算完成，但不会阻塞当前线程。
        embeddings = await embeddingGenerator.GenerateAsync(inputs);
    }
    catch (Exception exception)
    {
        // 在原始异常外再补充 Endpoint 和 ModelId，出现连接问题时更容易定位。
        throw new InvalidOperationException(
            $"无法调用 Embedding 服务。Endpoint={options.Endpoint}，ModelId={options.ModelId}。{exception.Message}",
            exception);
    }

    if (embeddings.Count != inputs.Count)
    {
        throw new InvalidOperationException(
            $"{inputDescription}应返回 {inputs.Count} 个向量，实际返回 {embeddings.Count} 个。");
    }

    return embeddings;
}

static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
{
    // 余弦相似度公式：
    //                    A · B
    // cosine(A, B) = ----------------
    //                  |A| × |B|
    //
    // A · B 是点积；|A|、|B| 是两个向量的长度（模）。
    // 分数越大，通常表示两段文本的语义越相近。
    // 这里接收 ReadOnlySpan，表示方法只读取向量，不会修改向量，也不需要复制数组。

    // 空向量没有可比较的维度。
    if (left.IsEmpty || right.IsEmpty)
    {
        throw new ArgumentException("参与余弦相似度计算的向量不能为空。");
    }

    // 两个向量必须来自兼容的 Embedding 模型，并且维度完全一致，才能逐项计算。
    if (left.Length != right.Length)
    {
        throw new ArgumentException(
            $"向量维度不一致：左侧 {left.Length}，右侧 {right.Length}。");
    }

    // 使用 double 累加可以减少 1024 次 float 运算产生的累计精度误差。
    double dotProduct = 0;
    double leftSquaredMagnitude = 0;
    double rightSquaredMagnitude = 0;

    // 一次遍历同时计算：
    // dotProduct            = A · B
    // leftSquaredMagnitude  = a1² + a2² + ...
    // rightSquaredMagnitude = b1² + b2² + ...
    for (var index = 0; index < left.Length; index++)
    {
        dotProduct += left[index] * right[index];
        leftSquaredMagnitude += left[index] * left[index];
        rightSquaredMagnitude += right[index] * right[index];
    }

    // 零向量的长度为 0，会导致后面的除法出现除以 0，因此必须提前拒绝。
    if (leftSquaredMagnitude == 0 || rightSquaredMagnitude == 0)
    {
        throw new ArgumentException("零向量不能用于余弦相似度计算。");
    }

    // Math.Sqrt 把“平方和”还原成向量长度，然后代入余弦相似度公式。
    return dotProduct
        / (Math.Sqrt(leftSquaredMagnitude) * Math.Sqrt(rightSquaredMagnitude));
}

internal sealed class DeepSeekOptions
{
    // 只承载本示例需要的三项配置，不与 WPF 主项目的配置类型共享。
    public required string Endpoint { get; init; }

    public required string ModelId { get; init; }

    public string? ApiKey { get; init; }
}

internal sealed class EmbeddingOptions
{
    // Ollama 的 OpenAI-compatible 地址，例如 http://192.168.137.2:11434/v1。
    public required string Endpoint { get; init; }

    // 用于把文本转换为向量的模型名称。
    public required string ModelId { get; init; }

    // Ollama 不校验这个值，但 OpenAI 客户端要求它不能为空。
    public required string ApiKey { get; init; }
}

// record 很适合表示“只承载数据”的小对象，创建后主要用于读取。
// EmbeddingSetup 把已经构建好的 Kernel 与它使用的配置一起返回。
internal sealed record EmbeddingSetup(Kernel Kernel, EmbeddingOptions Options);

// 尚未向量化的原始知识库文档。
internal sealed record KnowledgeDocument(string Id, string Content);

// 测试问题及其人工设定的预期 Top1；ExpectedDocumentId 不参与搜索计算。
internal sealed record KnowledgeQuery(string Text, string ExpectedDocumentId);

// 已入库的文档：在原始 Id、Content 基础上增加对应的 Embedding 向量。
// ReadOnlyMemory<float> 表示可长期保存在对象中、但不能通过这里修改的浮点数内存。
internal sealed record EmbeddedKnowledgeDocument(
    string Id,
    string Content,
    ReadOnlyMemory<float> Vector);

// 一条检索结果 = 命中的文档 + Query 与该文档的余弦相似度分数。
internal sealed record VectorSearchResult(
    EmbeddedKnowledgeDocument Document,
    double Score);

// MarkdownChunker 流程验证中的单项检查结果。
internal sealed record ChunkerValidationResult(
    string Name,
    bool Passed,
    string Detail);
