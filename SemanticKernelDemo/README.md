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
