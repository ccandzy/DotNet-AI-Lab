using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiChatClient.Services.Mcp;

/// <summary>
/// 通过 stdio 与本地 MCP Server 通信的最小 Client 实现。
/// </summary>
/// <remarks>
/// Client 启动 Server 子进程，向子进程的 stdin 写入 JSON-RPC，再从 stdout
/// 读取响应。为了便于学习，当前所有请求严格串行：收到上一条响应后才发送下一条。
/// 这里不接入 Semantic Kernel，只负责 MCP 协议通信并把结果交给 ViewModel。
/// </remarks>
internal sealed class McpClientService : IMcpClientService
{
    /// <summary>本 Client 当前唯一支持的 MCP 协议版本。</summary>
    private const string ProtocolVersion = "2025-11-25";

    /// <summary>避免 Server 不响应时 UI 永久等待。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    /// <summary>断开连接时等待 Server 自己退出的最长时间。</summary>
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// MCP stdio 使用 UTF-8，但消息前不能写入 BOM，否则第一个字节不是 JSON 的“{”。
    /// </summary>
    private static readonly Encoding Utf8WithoutBom =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly ILogger<McpClientService> _logger;
    private readonly Func<ProcessStartInfo>? _startInfoFactory;
    private readonly TimeSpan _requestTimeout;

    private sealed class RemoteToolException(string message) : InvalidOperationException(message);

    // 串行锁保证连接、查询、调用和断开不会同时读写同一组标准流。
    private readonly SemaphoreSlim _operationLock = new(1, 1);

    // 当前由 Client 启动并持有的 Server 子进程。
    private Process? _serverProcess;

    // JSON-RPC Request 的 id。每次请求递增，Response 必须返回相同 id。
    private int _nextRequestId;
    private bool _disposed;

    public McpClientService(ILogger<McpClientService> logger)
    {
        _logger = logger;
        _requestTimeout = RequestTimeout;
    }

    internal McpClientService(ILogger<McpClientService> logger,
        Func<ProcessStartInfo> startInfoFactory, TimeSpan requestTimeout) : this(logger)
    {
        _startInfoFactory = startInfoFactory;
        _requestTimeout = requestTimeout;
    }

    /// <inheritdoc />
    public McpConnectionState State { get; private set; } = McpConnectionState.Disconnected;

    /// <inheritdoc />
    public McpServerInfo? ServerInfo { get; private set; }

    /// <summary>
    /// 启动 Server 子进程，并完成 MCP initialize 握手。
    /// </summary>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (State == McpConnectionState.Connected && IsServerRunning())
            {
                return;
            }

            State = McpConnectionState.Connecting;
            CleanupProcess();

            // Server 的完整输出由项目构建复制到主程序旁边，运行时不依赖源码目录。
            var serverPath = Path.Combine(
                AppContext.BaseDirectory,
                "mcp-server",
                "AiChatClient.McpServer.exe");
            if (_startInfoFactory is null && !File.Exists(serverPath))
            {
                throw new FileNotFoundException(
                    "找不到 MCP Server，请先重新构建解决方案。",
                    serverPath);
            }

            // RedirectStandardInput/Output 建立 MCP stdio 通道；
            // RedirectStandardError 单独接收日志，避免污染协议 stdout。
            var startInfo = _startInfoFactory?.Invoke() ?? new ProcessStartInfo
            {
                FileName = serverPath,
                WorkingDirectory = Path.GetDirectoryName(serverPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardInputEncoding = Utf8WithoutBom,
                StandardOutputEncoding = Utf8WithoutBom,
                StandardErrorEncoding = Utf8WithoutBom
            };

            _serverProcess = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            _serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

            if (!_serverProcess.Start())
            {
                throw new InvalidOperationException("MCP Server 启动失败。");
            }

            _serverProcess.BeginErrorReadLine();

            // initialize 必须是连接建立后的第一条 MCP Request。
            // Client 在其中声明协议版本、能力和自己的基本信息。
            var initializeResult = await SendRequestCoreAsync(
                "initialize",
                new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { },
                    clientInfo = new
                    {
                        name = "AiChatClient",
                        version = "1.0.0"
                    }
                },
                cancellationToken);

            // Server 可能返回它支持的版本；最小 Client 只接受完全相同的版本。
            var negotiatedVersion = initializeResult
                .GetProperty("protocolVersion")
                .GetString();
            if (negotiatedVersion != ProtocolVersion)
            {
                throw new InvalidOperationException(
                    $"MCP 协议版本不兼容：Server 返回 {negotiatedVersion ?? "空版本"}。");
            }

            var serverInfo = initializeResult.GetProperty("serverInfo");
            ServerInfo = new McpServerInfo(
                serverInfo.GetProperty("name").GetString() ?? "未知 Server",
                serverInfo.GetProperty("version").GetString() ?? "未知版本");

            // initialize 成功后发送 initialized Notification。
            // 该消息没有 id，因此 Server 不会返回 Response。
            await SendNotificationCoreAsync(
                "notifications/initialized",
                cancellationToken);
            State = McpConnectionState.Connected;
        }
        catch
        {
            State = McpConnectionState.Error;
            ServerInfo = null;
            CleanupProcess();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// 发送 tools/list，并把协议中的 Tool JSON 转成页面容易绑定的模型。
    /// </summary>
    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();
            var result = await SendRequestCoreAsync(
                "tools/list",
                new { },
                cancellationToken);

            var tools = new List<McpToolInfo>();
            foreach (var tool in result.GetProperty("tools").EnumerateArray())
            {
                tools.Add(new McpToolInfo(
                    tool.GetProperty("name").GetString() ?? string.Empty,
                    tool.TryGetProperty("description", out var description)
                        ? description.GetString() ?? string.Empty
                        : string.Empty));
            }

            return tools;
        }
        catch (RemoteToolException) { throw; }
        catch
        {
            AbortConnection();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// 发送 tools/call 调用一个无参数 Tool，并提取第一段文本结果。
    /// </summary>
    public async Task<string> CallToolAsync(
        string toolName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            EnsureConnected();
            var result = await SendRequestCoreAsync(
                "tools/call",
                new
                {
                    name = toolName,
                    arguments = new { }
                },
                cancellationToken);

            var text = result.GetProperty("content")
                .EnumerateArray()
                .FirstOrDefault(item =>
                    item.TryGetProperty("type", out var type)
                    && type.GetString() == "text");
            var content = text.ValueKind == JsonValueKind.Undefined
                ? "Tool 未返回文本内容。"
                : text.GetProperty("text").GetString() ?? string.Empty;

            if (result.TryGetProperty("isError", out var isError)
                && isError.ValueKind == JsonValueKind.True)
            {
                throw new RemoteToolException(content);
            }

            return content;
        }
        catch (RemoteToolException) { throw; }
        catch
        {
            AbortConnection();
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// 主动关闭 stdin，让 Server 读到 EOF 后正常退出。
    /// </summary>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            await StopProcessAsync(cancellationToken);
            ServerInfo = null;
            State = McpConnectionState.Disconnected;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    /// <summary>
    /// 发送一条带 id 的 JSON-RPC Request，并同步读取对应 Response。
    /// </summary>
    /// <remarks>
    /// 第一版只有串行请求，所以可以在写入后直接 ReadLineAsync。
    /// 如果以后允许并发请求，则需要后台读取循环，再根据 id 分发响应。
    /// </remarks>
    private async Task<JsonElement> SendRequestCoreAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var process = GetRunningProcess();
        var requestId = ++_nextRequestId;
        // 使用字典构造 JSON-RPC 的四个基础字段。
        var message = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = method,
            ["params"] = parameters
        };

        // 同时尊重调用者取消和内部 10 秒超时。
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_requestTimeout);
        try
        {

        var serialized = JsonSerializer.Serialize(message);
        await process.StandardInput.WriteLineAsync(
            serialized.AsMemory(),
            timeout.Token);
        await process.StandardInput.FlushAsync(timeout.Token);

        var responseLine = await process.StandardOutput.ReadLineAsync(timeout.Token);
        if (responseLine is null)
        {
            throw new InvalidOperationException("MCP Server 已断开连接。");
        }

        using var response = JsonDocument.Parse(responseLine);
        var root = response.RootElement;
        // 先验证 JSON-RPC 固定版本。
        if (!root.TryGetProperty("jsonrpc", out var jsonRpc)
            || jsonRpc.GetString() != "2.0")
        {
            throw new InvalidOperationException("MCP Server 返回了无效的 JSON-RPC 响应。");
        }

        // 正常响应的 id 必须是本次请求的数字 id。
        // Parse error 等无法关联到请求的协议错误会返回 id:null，此时直接显示 Server 的错误，
        // 不能对 null 调用 TryGetInt32，否则会掩盖最初的协议错误。
        if (!root.TryGetProperty("id", out var responseId)
            || responseId.ValueKind != JsonValueKind.Number
            || !responseId.TryGetInt32(out var parsedId)
            || parsedId != requestId)
        {
            if (root.TryGetProperty("error", out var uncorrelatedError)
                && uncorrelatedError.TryGetProperty("message", out var errorMessage))
            {
                throw new InvalidOperationException(
                    errorMessage.GetString() ?? "MCP Server 返回了未知错误。");
            }

            throw new InvalidOperationException("MCP Server 返回了无效的 JSON-RPC 响应 id。");
        }

        // JSON-RPC Response 只会有 result 或 error；error 要转换为 C# 异常交给 UI。
        if (root.TryGetProperty("error", out var error))
        {
            var messageText = error.TryGetProperty("message", out var errorMessage)
                ? errorMessage.GetString()
                : null;
            throw new RemoteToolException(
                messageText ?? "MCP Server 返回了未知错误。");
        }

        if (!root.TryGetProperty("result", out var result))
        {
            throw new InvalidOperationException("MCP Server 响应中缺少 result。");
        }

        return result.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("MCP 请求超时，连接已关闭，请重新连接。");
        }
    }

    /// <summary>
    /// 发送一条没有 id 的 JSON-RPC Notification，因此不读取响应。
    /// </summary>
    private async Task SendNotificationCoreAsync(
        string method,
        CancellationToken cancellationToken)
    {
        var process = GetRunningProcess();
        var serialized = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            method
        });

        await process.StandardInput.WriteLineAsync(
            serialized.AsMemory(),
            cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private Process GetRunningProcess()
    {
        if (!IsServerRunning())
        {
            throw new InvalidOperationException("MCP Server 未连接。");
        }

        return _serverProcess!;
    }

    private bool IsServerRunning()
    {
        return _serverProcess is { HasExited: false };
    }

    private void EnsureConnected()
    {
        if (State != McpConnectionState.Connected || !IsServerRunning())
        {
            throw new InvalidOperationException("MCP Server 未连接。");
        }
    }

    private void AbortConnection()
    {
        State = McpConnectionState.Error;
        ServerInfo = null;
        CleanupProcess();
    }

    /// <summary>
    /// 先通过关闭 stdin 请求 Server 自然退出；超时后才强制结束进程。
    /// </summary>
    private async Task StopProcessAsync(CancellationToken cancellationToken)
    {
        var process = _serverProcess;
        if (process is null)
        {
            return;
        }

        try
        {
            process.StandardInput.Close();
            if (!process.HasExited)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ShutdownTimeout);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
        }
        finally
        {
            CleanupProcess();
        }
    }

    private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            _logger.LogInformation("MCP Server: {Message}", e.Data);
        }
    }

    private void CleanupProcess()
    {
        var process = _serverProcess;
        _serverProcess = null;
        if (process is null)
        {
            return;
        }

        try
        {
            // Dispose alone does not terminate a child process.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)ShutdownTimeout.TotalMilliseconds);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "清理 MCP 子进程失败。"); }
        finally
        {
            process.ErrorDataReceived -= ServerProcess_ErrorDataReceived;
            process.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    /// <summary>
    /// 应用退出时的同步兜底清理，确保不会残留 MCP Server 子进程。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var process = _serverProcess;
        _serverProcess = null;
        if (process is not null)
        {
            try
            {
                process.StandardInput.Close();
                if (!process.HasExited && !process.WaitForExit((int)ShutdownTimeout.TotalMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "关闭 MCP Server 时发生错误。");
            }
            finally
            {
                process.ErrorDataReceived -= ServerProcess_ErrorDataReceived;
                process.Dispose();
            }
        }

        State = McpConnectionState.Disconnected;
        ServerInfo = null;
    }
}
