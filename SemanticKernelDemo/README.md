# Semantic Kernel 最小学习示例

这个独立 Console 项目不引用 WPF 主项目，用来观察 Semantic Kernel 如何组织和调用本地能力。

## 三个核心对象

- **Kernel**：Semantic Kernel 的运行时容器，保存服务、Plugin 和调用管线。
- **Plugin**：一组可供 AI 使用的能力；本例是 `TimePlugin`。
- **Function**：Plugin 中一个具体可调用的函数；本例是 `get_current_time`。

## 运行离线示例

```powershell
dotnet run --project .\SemanticKernelDemo
```

该命令不访问网络。它创建 Kernel、注册 `TimePlugin`，再用 `kernel.InvokeAsync` 直接执行本地 Function。

## 配置并运行 DeepSeek 自动工具调用

复制本地配置示例文件，再填入你自己的 API Key：

```powershell
Copy-Item .\SemanticKernelDemo\appsettings.Local.json.example .\SemanticKernelDemo\appsettings.Local.json
```

`appsettings.Local.json` 已被 Git 忽略，不应提交 API Key。

然后执行：

```powershell
dotnet run --project .\SemanticKernelDemo -- --deepseek
```

该模式会把同一个 `TimePlugin` 注册给 DeepSeek，并使用 `FunctionChoiceBehavior.Auto()`。当模型决定需要当前时间时，Semantic Kernel 会执行 `TimePlugin.GetCurrentTime`，再把结果交回模型生成最终回答。控制台的 `[SK]` 日志展示了这次本地 Function 调用。

联网调用中显式传递 `thinking: disabled`，以避免 DeepSeek thinking 模式要求后续请求回传 `reasoning_content`，让示例只聚焦于 Tool / Function Calling。

## 运行最小 Embedding 实验

确保 `appsettings.json` 的 `Embedding` 节点指向已经安装 `qwen3-embedding:0.6b` 的 Ollama 服务，然后执行：

```powershell
dotnet run --project .\SemanticKernelDemo\SemanticKernelDemo.csproj -- --embedding
```

该模式通过 Ollama 的 OpenAI-compatible `/v1/embeddings` 接口，一次把下面三句话转换为向量：

```text
A: Semantic Kernel supports function calling.
B: SK can automatically call tools.
C: I like eating apples.
```

程序会输出每个向量的维度和前 5 个值，再用纯 C# 计算：

```text
CosineSimilarity(A, B)
CosineSimilarity(A, C)
```

预期 A、B 的相似度高于 A、C，从而直观看到语义相近的文本在向量空间中更接近。这个实验不保存向量，也不包含 Chunking、Vector Store、TopK 或完整 RAG。

`ApiKey` 的值 `ollama` 只是为了满足 OpenAI 客户端的非空要求，Ollama 不会校验它。默认不带参数运行时仍然只执行离线示例，不会访问 DeepSeek 或 Ollama。

## 运行内存知识库 TopK 检索

最小 Embedding 实验通过后，可以继续执行：

```powershell
dotnet run --project .\SemanticKernelDemo\SemanticKernelDemo.csproj -- --vector-search
```

该模式准备 8 条项目知识和 5 个固定 Query，并按照下面的流程运行：

```text
8 条 Document
→ 批量生成 Document Embedding
→ 文本和向量保存到内存集合
→ 批量生成 Query Embedding
→ 用完整向量计算 Query 与每条 Document 的余弦相似度
→ 按相似度从高到低排序
→ 输出 Top 3
```

每个 Query 都会显示预期文档、实际 Top 3、相似度分数和 Top1 验证结果。例如 `How are conversations stored?` 预期首先检索到描述 EF Core 与 SQLite 持久化的 `Document 3`。

该模式仍然不是完整 RAG：它只完成“向量化 + 内存保存 + TopK 检索”，不会调用聊天模型生成回答，也不会使用 Vector Store、Qdrant 或其他数据库。程序退出后，内存中的文档向量随即释放。

## 验证 MarkdownChunker 流程

该模式不访问任何 AI 服务，只读取随项目提供的 Markdown 样本文档：

```powershell
dotnet run --project .\SemanticKernelDemo\SemanticKernelDemo.csproj -- --markdown-chunker
```

程序会用 300 字符的目标上限和 100 字符的段落重叠执行以下流程：

```text
读取 Markdown
→ 识别 YAML 与标题层级
→ 在章节内识别段落、代码围栏和表格
→ 拆分超长普通内容
→ 合并为 Chunk 并添加完整段落重叠
→ 输出标题路径、源文件行号、字符数和原文
```

最后会自动验证 Chunk 编号、来源路径、行号、标题层级、代码围栏、Markdown 表格和相邻块重叠。全部通过时输出验证总结；任何一项失败时，程序使用非零退出码结束。

## Visual Studio 调试配置

项目的 `Properties/launchSettings.json` 已提供以下启动配置，可以从 Visual Studio 顶部绿色启动按钮旁的下拉框直接选择：

- `SemanticKernelDemo`：仅运行默认离线 Function 示例。
- `DeepSeek Function Calling`：传入 `--deepseek`。
- `Ollama Embedding`：传入 `--embedding`。
- `In-Memory Vector Search`：传入 `--vector-search`。
- `Markdown Chunker`：传入 `--markdown-chunker`。
