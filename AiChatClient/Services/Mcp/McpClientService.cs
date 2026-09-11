using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AiChatClient.Services.Mcp;

internal sealed class McpClientService : IMcpClientService
{
    private const string ProtocolVersion = "2025-11-25";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(2);
    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

    private readonly ILogger<McpClientService> _logger;
    private readonly Func<ProcessStartInfo>? _startInfoFactory;
    private readonly TimeSpan _requestTimeout;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private Process? _serverProcess;
    private CancellationTokenSource? _readerCancellation;
    private Task? _readerTask;
    private int _nextRequestId;
    private bool _disposed;

    private sealed class RemoteToolException(string message) : InvalidOperationException(message);

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

    public event EventHandler<McpResourceUpdatedEventArgs>? ResourceUpdated;

    public McpConnectionState State { get; private set; } = McpConnectionState.Disconnected;
    public McpServerInfo? ServerInfo { get; private set; }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (State == McpConnectionState.Connected && IsServerRunning()) return;
            State = McpConnectionState.Connecting;
            CleanupProcess();

            var serverPath = Path.Combine(AppContext.BaseDirectory,
                "mcp-server", "AiChatClient.McpServer.exe");
            if (_startInfoFactory is null && !File.Exists(serverPath))
                throw new FileNotFoundException("找不到 MCP Server，请先重新构建解决方案。", serverPath);

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
            _serverProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            _serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
            if (!_serverProcess.Start()) throw new InvalidOperationException("MCP Server 启动失败。");
            _serverProcess.BeginErrorReadLine();
            _readerCancellation = new CancellationTokenSource();
            _readerTask = ReadLoopAsync(_serverProcess, _readerCancellation.Token);

            var result = await SendRequestCoreAsync("initialize", new
            {
                protocolVersion = ProtocolVersion,
                capabilities = new { },
                clientInfo = new { name = "AiChatClient", version = "2.0.0" }
            }, cancellationToken);
            var negotiated = result.GetProperty("protocolVersion").GetString();
            if (negotiated != ProtocolVersion)
                throw new InvalidOperationException($"MCP 协议版本不兼容：Server 返回 {negotiated ?? "空版本"}。");
            var info = result.GetProperty("serverInfo");
            ServerInfo = new McpServerInfo(info.GetProperty("name").GetString() ?? "未知 Server",
                info.GetProperty("version").GetString() ?? "未知版本");
            await SendNotificationCoreAsync("notifications/initialized", null, cancellationToken);
            State = McpConnectionState.Connected;
        }
        catch
        {
            State = McpConnectionState.Error;
            ServerInfo = null;
            CleanupProcess();
            throw;
        }
        finally { _lifecycleLock.Release(); }
    }

    public async Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecuteConnectedRequestAsync("tools/list", new { }, cancellationToken);
        return result.GetProperty("tools").EnumerateArray().Select(tool => new McpToolInfo(
            tool.GetProperty("name").GetString() ?? string.Empty,
            tool.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
            tool.TryGetProperty("inputSchema", out var schema) ? schema.GetRawText() : "{}"))
            .ToArray();
    }

    public async Task<string> CallToolAsync(string toolName, CancellationToken cancellationToken = default) =>
        (await CallToolAsync(toolName, new Dictionary<string, object?>(), cancellationToken)).Text;

    public async Task<McpToolCallResult> CallToolAsync(string toolName,
        IReadOnlyDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);
        var result = await ExecuteConnectedRequestAsync("tools/call", new { name = toolName, arguments }, cancellationToken,
            preserveBusinessErrors: true);
        var textItem = result.GetProperty("content").EnumerateArray().FirstOrDefault(item =>
            item.TryGetProperty("type", out var type) && type.GetString() == "text");
        var text = textItem.ValueKind == JsonValueKind.Undefined
            ? "Tool 未返回文本内容。"
            : textItem.GetProperty("text").GetString() ?? string.Empty;
        if (result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
            throw new RemoteToolException(text);
        JsonElement? structured = result.TryGetProperty("structuredContent", out var content)
            ? content.Clone() : null;
        return new McpToolCallResult(text, structured);
    }

    public async Task SubscribeResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        await ExecuteConnectedRequestAsync("resources/subscribe", new { uri }, cancellationToken);
    }

    public async Task<string> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        var result = await ExecuteConnectedRequestAsync("resources/read", new { uri }, cancellationToken);
        var content = result.GetProperty("contents").EnumerateArray().FirstOrDefault();
        return content.ValueKind == JsonValueKind.Undefined
            ? "null"
            : content.GetProperty("text").GetString() ?? "null";
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            await StopProcessAsync(cancellationToken);
            State = McpConnectionState.Disconnected;
            ServerInfo = null;
        }
        finally { _lifecycleLock.Release(); }
    }

    private async Task<JsonElement> ExecuteConnectedRequestAsync(string method, object parameters,
        CancellationToken cancellationToken, bool preserveBusinessErrors = false)
    {
        ThrowIfDisposed();
        EnsureConnected();
        try { return await SendRequestCoreAsync(method, parameters, cancellationToken); }
        catch (RemoteToolException) when (preserveBusinessErrors) { throw; }
        catch
        {
            AbortConnection();
            throw;
        }
    }

    private async Task<JsonElement> SendRequestCoreAsync(string method, object? parameters,
        CancellationToken cancellationToken)
    {
        var process = GetRunningProcess();
        var id = Interlocked.Increment(ref _nextRequestId);
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, completion)) throw new InvalidOperationException("MCP 请求编号冲突。");
        try
        {
            await WriteMessageAsync(process, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0", ["id"] = id, ["method"] = method, ["params"] = parameters
            }), cancellationToken);
            try
            {
                return await completion.Task.WaitAsync(_requestTimeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                throw new TimeoutException("MCP 请求超时，连接已关闭，请重新连接。");
            }
        }
        finally { _pending.TryRemove(id, out _); }
    }

    private async Task ReadLoopAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(cancellationToken);
                if (line is null) throw new InvalidOperationException("MCP Server 已断开连接。");
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("jsonrpc", out var jsonRpc) || jsonRpc.GetString() != "2.0")
                    throw new InvalidOperationException("MCP Server 返回了无效的 JSON-RPC 消息。");

                if (root.TryGetProperty("id", out var idElement))
                {
                    if (!idElement.TryGetInt32(out var id) || !_pending.TryRemove(id, out var completion))
                        throw new InvalidOperationException("MCP Server 返回了无法关联的响应 id。");
                    if (root.TryGetProperty("error", out var error))
                    {
                        var message = error.TryGetProperty("message", out var errorMessage)
                            ? errorMessage.GetString() : null;
                        completion.TrySetException(new RemoteToolException(message ?? "MCP Server 返回了未知错误。"));
                    }
                    else if (root.TryGetProperty("result", out var result))
                        completion.TrySetResult(result.Clone());
                    else
                        completion.TrySetException(new InvalidOperationException("MCP Server 响应中缺少 result。"));
                    continue;
                }

                if (root.TryGetProperty("method", out var notificationMethod)
                    && notificationMethod.GetString() == "notifications/resources/updated"
                    && root.TryGetProperty("params", out var notificationParameters)
                    && notificationParameters.TryGetProperty("uri", out var uri))
                {
                    ResourceUpdated?.Invoke(this, new McpResourceUpdatedEventArgs(uri.GetString() ?? string.Empty));
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "MCP stdout 读取循环失败。");
            FailPending(exception);
            if (State is McpConnectionState.Connected or McpConnectionState.Connecting)
            {
                State = McpConnectionState.Error;
                ServerInfo = null;
            }
        }
    }

    private Task SendNotificationCoreAsync(string method, object? parameters,
        CancellationToken cancellationToken) =>
        WriteMessageAsync(GetRunningProcess(), JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0", ["method"] = method,
            ["params"] = parameters
        }), cancellationToken);

    private async Task WriteMessageAsync(Process process, string message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await process.StandardInput.WriteLineAsync(message.AsMemory(), cancellationToken);
            await process.StandardInput.FlushAsync(cancellationToken);
        }
        finally { _writeLock.Release(); }
    }

    private async Task StopProcessAsync(CancellationToken cancellationToken)
    {
        var process = _serverProcess;
        if (process is null) return;
        try
        {
            process.StandardInput.Close();
            if (!process.HasExited)
            {
                try { await process.WaitForExitAsync(cancellationToken).WaitAsync(ShutdownTimeout, cancellationToken); }
                catch (TimeoutException)
                {
                    process.Kill();
                    await process.WaitForExitAsync(cancellationToken);
                }
            }
        }
        finally { CleanupProcess(); }
    }

    private Process GetRunningProcess() => IsServerRunning()
        ? _serverProcess!
        : throw new InvalidOperationException("MCP Server 已断开连接。");

    private bool IsServerRunning() => _serverProcess is { HasExited: false };

    private void EnsureConnected()
    {
        if (State != McpConnectionState.Connected || !IsServerRunning())
            throw new InvalidOperationException("MCP Server 尚未连接。");
    }

    private void AbortConnection()
    {
        State = McpConnectionState.Error;
        ServerInfo = null;
        CleanupProcess();
    }

    private void CleanupProcess()
    {
        _readerCancellation?.Cancel();
        _readerCancellation?.Dispose();
        _readerCancellation = null;
        _readerTask = null;
        FailPending(new InvalidOperationException("MCP 连接已关闭。"));
        var process = _serverProcess;
        _serverProcess = null;
        if (process is null) return;
        try
        {
            if (!process.HasExited) process.Kill();
        }
        catch (Exception exception) { _logger.LogWarning(exception, "终止 MCP Server 失败。"); }
        finally
        {
            process.ErrorDataReceived -= ServerProcess_ErrorDataReceived;
            process.Dispose();
        }
    }

    private void FailPending(Exception exception)
    {
        foreach (var pair in _pending.ToArray())
            if (_pending.TryRemove(pair.Key, out var completion)) completion.TrySetException(exception);
    }

    private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data)) _logger.LogInformation("MCP Server: {Message}", e.Data);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CleanupProcess();
        State = McpConnectionState.Disconnected;
        ServerInfo = null;
        _lifecycleLock.Dispose();
        _writeLock.Dispose();
    }
}
