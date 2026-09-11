using System.Text;
using System.Text.Json;

namespace AiChatClient.McpServer;

/// <summary>
/// 一个不依赖 MCP SDK 的最小 MCP Server。
/// </summary>
/// <remarks>
/// <para>
/// 这个程序通过标准输入输出（stdio）与 MCP Client 通信：
/// Client 把一行 JSON-RPC 写入本进程的 stdin，本程序处理后再把一行 JSON-RPC
/// 写入 stdout。stdout 只能承载协议消息，调试或错误信息必须写入 stderr，
/// 否则 Client 会把普通日志误认为协议响应。
/// </para>
/// <para>
/// 当前示例故意只实现学习 MCP 所需的最小集合：
/// initialize、notifications/initialized、tools/list 和 tools/call。
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Client 和 Server 约定使用的 MCP 协议版本。
    /// 初始化时双方必须确认支持同一个版本，后续才能交换 MCP 消息。
    /// </summary>
    private const string ProtocolVersion = "2025-11-25";

    /// <summary>
    /// 本 Server 对外暴露的唯一 Tool 名称。
    /// tools/list 返回这个名称，tools/call 也使用这个名称寻找 Tool。
    /// </summary>
    private const string TimeToolName = "get_current_time";

    /// <summary>
    /// stdio 要求一条协议消息占一行，因此不能使用缩进格式化后的多行 JSON。
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false
    };

    /// <summary>
    /// Server 进程入口，也是 stdio 消息读取循环。
    /// </summary>
    private static async Task Main()
    {
        // MCP 的 JSON-RPC 消息统一使用 UTF-8。
        // stdout 禁止输出 BOM，避免第一个 JSON 字符前出现额外字节。
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        string? line;
        // Client 关闭 stdin 时 ReadLineAsync 返回 null，循环结束，Server 随即正常退出。
        while ((line = await Console.In.ReadLineAsync()) is not null)
        {
            // 正常的 MCP stdio 消息不包含 BOM。
            // 这里仍做一次防御性清理，方便识别由错误编码设置产生的第一条消息。
            line = line.TrimStart('\uFEFF');
            var response = HandleMessage(line);
            if (response is null)
            {
                // Notification 不要求响应，因此这里直接继续读取下一条消息。
                continue;
            }

            // 一条响应写成一行，并立即 Flush，确保 Client 不会一直等待缓冲区。
            await Console.Out.WriteLineAsync(response);
            await Console.Out.FlushAsync();
        }
    }

    /// <summary>
    /// 解析并分发一条 JSON-RPC 消息。
    /// </summary>
    /// <param name="message">从 stdin 读取到的一整行 JSON。</param>
    /// <returns>
    /// 需要写给 Client 的 JSON 响应；如果收到的是 Notification，则返回 null。
    /// </returns>
    private static string? HandleMessage(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            // 最小的合法请求必须是 JSON 对象，包含 jsonrpc="2.0" 和字符串 method。
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("jsonrpc", out var jsonRpc)
                || jsonRpc.GetString() != "2.0"
                || !root.TryGetProperty("method", out var methodElement)
                || methodElement.ValueKind != JsonValueKind.String)
            {
                return CreateErrorResponse(null, -32600, "Invalid Request");
            }

            var method = methodElement.GetString()!;
            var hasId = root.TryGetProperty("id", out var idElement);
            var id = hasId ? idElement.Clone() : (JsonElement?)null;

            // JSON-RPC 中没有 id 的消息叫 Notification。
            // Notification 是“只通知、不等待结果”，所以 Server 不能返回 Response。
            if (!hasId)
            {
                return null;
            }

            // 根据 MCP method 把 Request 分发给对应处理逻辑。
            // Response 必须原样带回 Request 的 id，Client 才知道响应属于哪个请求。
            return method switch
            {
                "initialize" => HandleInitialize(root, id!.Value),
                "tools/list" => CreateSuccessResponse(id!.Value, new
                {
                    // tools/list 返回 Server 当前可用的所有 Tool 定义。
                    tools = new[]
                    {
                        new
                        {
                            name = TimeToolName,
                            description = "获取 MCP Server 所在计算机的当前本地时间。",
                            // inputSchema 使用 JSON Schema 描述参数。
                            // 当前 Tool 没有参数，所以 properties 为空，并禁止额外参数。
                            inputSchema = new
                            {
                                type = "object",
                                properties = new { },
                                additionalProperties = false
                            }
                        }
                    }
                }),
                "tools/call" => HandleToolCall(root, id!.Value),
                _ => CreateErrorResponse(id, -32601, $"Method not found: {method}")
            };
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"无法解析 JSON-RPC 消息：{exception.Message}");
            // -32700 是 JSON-RPC 规定的“JSON 解析失败”错误码。
            return CreateErrorResponse(null, -32700, "Parse error");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"处理 MCP 消息失败：{exception}");
            // -32603 表示协议处理过程中发生了未预期的内部错误。
            return CreateErrorResponse(null, -32603, "Internal error");
        }
    }

    /// <summary>
    /// 处理 MCP 建立连接后的第一个请求：initialize。
    /// </summary>
    /// <remarks>
    /// Client 会说明自己希望使用的协议版本；Server 确认版本后，
    /// 返回自身信息以及支持的能力。这里仅声明 tools 能力。
    /// </remarks>
    private static string HandleInitialize(JsonElement root, JsonElement id)
    {
        // 为了让示例保持简单，本 Server 只接受固定版本，不做版本降级协商。
        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("protocolVersion", out var protocolVersion)
            || protocolVersion.GetString() != ProtocolVersion)
        {
            return CreateErrorResponse(
                id,
                -32602,
                $"仅支持 MCP 协议版本 {ProtocolVersion}。");
        }

        return CreateSuccessResponse(id, new
        {
            protocolVersion = ProtocolVersion,
            // capabilities 告诉 Client：“这个 Server 能提供什么”。
            capabilities = new
            {
                tools = new { }
            },
            // serverInfo 会显示在 Host 的 MCP 能力区域。
            serverInfo = new
            {
                name = "AiChatClient.McpServer",
                version = "1.0.0"
            }
        });
    }

    /// <summary>
    /// 处理 tools/call 请求，找到并执行用户选择的 Tool。
    /// </summary>
    private static string HandleToolCall(JsonElement root, JsonElement id)
    {
        // params.name 决定要调用哪个 Tool。
        // 当前 Server 只有 get_current_time，因此其他名称都视为参数无效。
        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var nameElement)
            || nameElement.GetString() != TimeToolName)
        {
            return CreateErrorResponse(id, -32602, "Tool 名称无效。");
        }

        // get_current_time 没有参数。如果 arguments 不是空对象，就拒绝调用。
        if (parameters.TryGetProperty("arguments", out var arguments)
            && (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Any()))
        {
            return CreateErrorResponse(id, -32602, $"{TimeToolName} 不接受参数。");
        }

        // DateTimeOffset 同时保留本地时间和 UTC 偏移，例如 +08:00。
        var currentTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        return CreateSuccessResponse(id, new
        {
            // MCP Tool 的普通文本结果放在 result.content 数组中。
            content = new[]
            {
                new
                {
                    type = "text",
                    text = currentTime
                }
            },
            isError = false
        });
    }

    /// <summary>
    /// 创建成功的 JSON-RPC Response。
    /// </summary>
    /// <param name="id">必须与 Client Request 中的 id 完全一致。</param>
    /// <param name="result">具体 MCP 方法返回的数据。</param>
    private static string CreateSuccessResponse(JsonElement id, object result)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            result
        }, SerializerOptions);
    }

    /// <summary>
    /// 创建失败的 JSON-RPC Response。
    /// </summary>
    /// <param name="id">
    /// 能解析出请求 id 时原样返回；JSON 本身无法解析时只能返回 null。
    /// </param>
    /// <param name="code">JSON-RPC 错误码。</param>
    /// <param name="message">给 Client 阅读的错误说明。</param>
    private static string CreateErrorResponse(JsonElement? id, int code, string message)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id,
            error = new
            {
                code,
                message
            }
        }, SerializerOptions);
    }
}
