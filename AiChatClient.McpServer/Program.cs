using System.IO.Ports;
using System.Text;
using System.Text.Json;
using AiChatClient.McpServer.Serial;

namespace AiChatClient.McpServer;

internal static class Program
{
    private const string ProtocolVersion = "2025-11-25";
    private const string MeasurementResourceUri = "serial://measurement/latest";
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };
    private static readonly SemaphoreSlim OutputLock = new(1, 1);
    private static readonly DeviceSerialService Device = new();
    private static readonly Timer NotificationTimer = new(
        _ => _ = PublishMeasurementUpdateAsync(), null, Timeout.Infinite, Timeout.Infinite);
    private static long _latestSequence;
    private static long _notifiedSequence;
    private static int _notificationScheduled;
    private static bool _measurementSubscribed;

    private static async Task Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(false);
        Device.SerialActivity += OnSerialActivity;
        try
        {
            string? line;
            while ((line = await Console.In.ReadLineAsync()) is not null)
            {
                var response = await HandleMessageAsync(line.TrimStart('\uFEFF'));
                if (response is not null) await WriteProtocolMessageAsync(response);
            }
        }
        finally
        {
            Device.SerialActivity -= OnSerialActivity;
            await Device.DisposeAsync();
            await NotificationTimer.DisposeAsync();
            OutputLock.Dispose();
        }
    }

    private static async Task<string?> HandleMessageAsync(string message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("jsonrpc", out var jsonRpc)
                || jsonRpc.GetString() != "2.0"
                || !root.TryGetProperty("method", out var methodElement)
                || methodElement.ValueKind != JsonValueKind.String)
            {
                return Error(null, -32600, "Invalid Request");
            }

            var method = methodElement.GetString()!;
            if (!root.TryGetProperty("id", out var idElement)) return null;
            var id = idElement.Clone();
            return method switch
            {
                "initialize" => Initialize(root, id),
                "tools/list" => Success(id, new { tools = ToolDefinitions() }),
                "tools/call" => await CallToolAsync(root, id),
                "resources/list" => ListResources(id),
                "resources/read" => ReadResource(root, id),
                "resources/subscribe" => Subscribe(root, id, true),
                "resources/unsubscribe" => Subscribe(root, id, false),
                _ => Error(id, -32601, $"Method not found: {method}")
            };
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"无法解析 JSON-RPC 消息：{exception.Message}");
            return Error(null, -32700, "Parse error");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"处理 MCP 消息失败：{exception}");
            return Error(null, -32603, "Internal error");
        }
    }

    private static string Initialize(JsonElement root, JsonElement id)
    {
        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("protocolVersion", out var version)
            || version.GetString() != ProtocolVersion)
        {
            return Error(id, -32602, $"仅支持 MCP 协议版本 {ProtocolVersion}。");
        }

        return Success(id, new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new { tools = new { }, resources = new { subscribe = true } },
            serverInfo = new { name = "AiChatClient.McpServer", version = "2.0.0" }
        });
    }

    private static object[] ToolDefinitions() =>
    [
        Tool("get_current_time", "获取 MCP Server 所在计算机的当前本地时间。", EmptySchema()),
        Tool("list_serial_ports", "列出本机当前可用的串口。", EmptySchema()),
        Tool("open_serial", "使用指定参数打开设备串口。", new
        {
            type = "object",
            properties = new
            {
                portName = new { type = "string", description = "串口名称，例如 COM3。" },
                baudRate = new { type = "integer", minimum = 1 },
                dataBits = new { type = "integer", minimum = 5, maximum = 8 },
                parity = new { type = "string", @enum = Enum.GetNames<Parity>() },
                stopBits = new { type = "string", @enum = Enum.GetNames<StopBits>() },
                handshake = new { type = "string", @enum = Enum.GetNames<Handshake>() }
            },
            required = new[] { "portName", "baudRate", "dataBits", "parity", "stopBits", "handshake" },
            additionalProperties = false
        }),
        Tool("start_measurement", "发送大激发开始测量协议；设备没有独立确认回包。", new
        {
            type = "object",
            properties = new
            {
                frequency = new { type = "integer", minimum = 1, maximum = 50 },
                durationSeconds = new { type = "integer", minimum = 1, maximum = 9999 }
            },
            required = new[] { "frequency", "durationSeconds" },
            additionalProperties = false
        }),
        Tool("stop_measurement", "发送大激发停止测量协议，串口保持打开。", EmptySchema()),
        Tool("close_serial", "停止测量并关闭设备串口。", EmptySchema()),
        Tool("get_device_status", "读取当前串口和设备测量状态。", EmptySchema())
    ];

    private static object Tool(string name, string description, object inputSchema) =>
        new { name, title = description.TrimEnd('。'), description, inputSchema };

    private static object EmptySchema() =>
        new { type = "object", properties = new { }, additionalProperties = false };

    private static async Task<string> CallToolAsync(JsonElement root, JsonElement id)
    {
        if (!TryGetToolCall(root, out var name, out var arguments, out var error))
            return Error(id, -32602, error!);

        try
        {
            return name switch
            {
                "get_current_time" => ToolSuccess(id,
                    new { localTime = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") },
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz")),
                "list_serial_ports" => ListSerialPorts(id),
                "open_serial" => await OpenSerialAsync(id, arguments),
                "start_measurement" => await StartMeasurementAsync(id, arguments),
                "stop_measurement" => await StopMeasurementAsync(id),
                "close_serial" => await CloseSerialAsync(id),
                "get_device_status" => ToolSuccess(id, Status(), DescribeStatus()),
                _ => Error(id, -32602, $"Tool 名称无效：{name}")
            };
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or IOException
            or UnauthorizedAccessException)
        {
            return ToolError(id, exception.Message);
        }
    }

    private static string ListSerialPorts(JsonElement id)
    {
        var ports = DeviceSerialService.GetPortNames();
        return ToolSuccess(id, new { ports }, ports.Count == 0
            ? "未发现可用串口。"
            : string.Join(Environment.NewLine, ports));
    }

    private static async Task<string> OpenSerialAsync(JsonElement id, JsonElement arguments)
    {
        var options = new SerialConnectionOptions(
            RequiredString(arguments, "portName"), RequiredInt32(arguments, "baudRate"),
            RequiredInt32(arguments, "dataBits"), RequiredEnum<Parity>(arguments, "parity"),
            RequiredEnum<StopBits>(arguments, "stopBits"), RequiredEnum<Handshake>(arguments, "handshake"));
        await Device.OpenAsync(options, CancellationToken.None);
        return ToolSuccess(id, Status(), $"串口 {options.PortName} 已打开。");
    }

    private static async Task<string> StartMeasurementAsync(JsonElement id, JsonElement arguments)
    {
        var frequency = RequiredInt32(arguments, "frequency");
        var durationSeconds = RequiredInt32(arguments, "durationSeconds");
        await Device.StartMeasurementAsync(frequency, durationSeconds, CancellationToken.None);
        return ToolSuccess(id, Status(), "开始指令已发送，正在等待实时测量数据。");
    }

    private static async Task<string> StopMeasurementAsync(JsonElement id)
    {
        await Device.StopMeasurementAsync(CancellationToken.None);
        return ToolSuccess(id, Status(), "停止指令已发送，串口保持打开。");
    }

    private static async Task<string> CloseSerialAsync(JsonElement id)
    {
        await Device.CloseAsync(CancellationToken.None);
        return ToolSuccess(id, Status(), "串口已关闭。");
    }

    private static object Status() => new
    {
        state = Device.State.ToString(),
        portName = Device.ConnectionOptions?.PortName,
        latestMeasurement = Device.LatestMeasurement is null ? null : Measurement(Device.LatestMeasurement),
        diagnostics = Diagnostics(Device.Diagnostics)
    };

    private static string DescribeStatus() => Device.ConnectionOptions is null
        ? "串口未连接。"
        : $"串口 {Device.ConnectionOptions.PortName}，设备状态：{Device.State}。";

    private static object Measurement(MeasurementReading reading) => new
    {
        sequence = reading.Sequence,
        timestamp = reading.Timestamp.ToString("O"),
        flowRaw = reading.FlowRaw,
        mouthPressureRaw = reading.MouthPressureRaw
    };

    private static object Diagnostics(SerialReceiveDiagnostics diagnostics) => new
    {
        activitySequence = diagnostics.ActivitySequence,
        bytesReceived = diagnostics.BytesReceived,
        framesReceived = diagnostics.FramesReceived,
        measurementsParsed = diagnostics.MeasurementsParsed,
        lastFrameLength = diagnostics.LastFrameLength,
        lastFrameHex = diagnostics.LastFrameHex,
        lastReceiveHex = diagnostics.LastReceiveHex,
        lastActivityAt = diagnostics.LastActivityAt?.ToString("O")
    };

    private static object MeasurementResource()
    {
        var reading = Device.LatestMeasurement;
        var diagnostics = Device.Diagnostics;
        return new
        {
            sequence = reading?.Sequence,
            timestamp = reading?.Timestamp.ToString("O"),
            flowRaw = reading?.FlowRaw,
            mouthPressureRaw = reading?.MouthPressureRaw,
            diagnostics = Diagnostics(diagnostics)
        };
    }

    private static string ListResources(JsonElement id) => Success(id, new
    {
        resources = new[]
        {
            new { uri = MeasurementResourceUri, name = "大激发最新测量值",
                description = "最新流速和口压原始值。", mimeType = "application/json" }
        }
    });

    private static string ReadResource(JsonElement root, JsonElement id)
    {
        if (!TryGetResourceUri(root, out var uri) || uri != MeasurementResourceUri)
            return Error(id, -32602, "资源 URI 无效。");
        var text = JsonSerializer.Serialize(MeasurementResource(), SerializerOptions);
        return Success(id, new
        {
            contents = new[] { new { uri = MeasurementResourceUri, mimeType = "application/json", text } }
        });
    }

    private static string Subscribe(JsonElement root, JsonElement id, bool subscribe)
    {
        if (!TryGetResourceUri(root, out var uri) || uri != MeasurementResourceUri)
            return Error(id, -32602, "资源 URI 无效。");
        _measurementSubscribed = subscribe;
        return Success(id, new { });
    }

    private static void OnSerialActivity(long activitySequence)
    {
        Interlocked.Exchange(ref _latestSequence, activitySequence);
        if (_measurementSubscribed && Interlocked.Exchange(ref _notificationScheduled, 1) == 0)
            NotificationTimer.Change(TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);
    }

    private static async Task PublishMeasurementUpdateAsync()
    {
        if (!_measurementSubscribed)
        {
            Interlocked.Exchange(ref _notificationScheduled, 0);
            return;
        }
        var sequence = Interlocked.Read(ref _latestSequence);
        try
        {
            if (sequence != Interlocked.Read(ref _notifiedSequence))
            {
                await WriteProtocolMessageAsync(JsonSerializer.Serialize(new
                {
                    jsonrpc = "2.0", method = "notifications/resources/updated",
                    @params = new { uri = MeasurementResourceUri }
                }, SerializerOptions));
                Interlocked.Exchange(ref _notifiedSequence, sequence);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"发送测量通知失败：{exception.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _notificationScheduled, 0);
            if (_measurementSubscribed
                && Interlocked.Read(ref _latestSequence) != Interlocked.Read(ref _notifiedSequence)
                && Interlocked.Exchange(ref _notificationScheduled, 1) == 0)
            {
                NotificationTimer.Change(TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);
            }
        }
    }

    private static bool TryGetToolCall(JsonElement root, out string name,
        out JsonElement arguments, out string? error)
    {
        name = string.Empty; arguments = default; error = null;
        if (!root.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var nameElement)
            || string.IsNullOrWhiteSpace(nameElement.GetString()))
        {
            error = "Tool 名称无效。"; return false;
        }
        name = nameElement.GetString()!;
        if (parameters.TryGetProperty("arguments", out arguments))
        {
            if (arguments.ValueKind == JsonValueKind.Object) return true;
            error = "Tool arguments 必须是对象。"; return false;
        }
        using var empty = JsonDocument.Parse("{}");
        arguments = empty.RootElement.Clone();
        return true;
    }

    private static bool TryGetResourceUri(JsonElement root, out string? uri)
    {
        uri = null;
        return root.TryGetProperty("params", out var parameters)
            && parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("uri", out var element)
            && (uri = element.GetString()) is not null;
    }

    private static string RequiredString(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw new ArgumentException($"缺少有效参数 {name}。");
        return value.GetString()!;
    }

    private static int RequiredInt32(JsonElement arguments, string name)
    {
        if (!arguments.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result))
            throw new ArgumentException($"缺少有效参数 {name}。");
        return result;
    }

    private static T RequiredEnum<T>(JsonElement arguments, string name) where T : struct, Enum
    {
        var text = RequiredString(arguments, name);
        if (!Enum.TryParse<T>(text, true, out var result))
            throw new ArgumentException($"参数 {name} 的值 {text} 无效。");
        return result;
    }

    private static string ToolSuccess(JsonElement id, object structuredContent, string text) =>
        Success(id, new { content = new[] { new { type = "text", text } }, structuredContent, isError = false });

    private static string ToolError(JsonElement id, string message) =>
        Success(id, new { content = new[] { new { type = "text", text = message } }, isError = true });

    private static string Success(JsonElement id, object result) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, result }, SerializerOptions);

    private static string Error(JsonElement? id, int code, string message) =>
        JsonSerializer.Serialize(new { jsonrpc = "2.0", id, error = new { code, message } }, SerializerOptions);

    private static async Task WriteProtocolMessageAsync(string message)
    {
        await OutputLock.WaitAsync();
        try
        {
            await Console.Out.WriteLineAsync(message);
            await Console.Out.FlushAsync();
        }
        finally { OutputLock.Release(); }
    }
}
