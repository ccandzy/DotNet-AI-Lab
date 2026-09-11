namespace AiChatClient.Services.Mcp;

/// <summary>
/// AiChatClient 内部使用的最小 MCP Client 抽象。
/// </summary>
/// <remarks>
/// 该接口只负责 MCP 通信，不负责决定大模型是否调用 Tool。
/// 当前页面通过这些方法手动连接 Server、发现 Tool 并发起调用。
/// </remarks>
public interface IMcpClientService : IDisposable
{
    /// <summary>
    /// 当前 MCP 连接所处的状态，供 ViewModel 控制界面和命令启用条件使用。
    /// </summary>
    McpConnectionState State { get; }

    /// <summary>
    /// initialize 成功后由 Server 返回的名称和版本；未连接时为 null。
    /// </summary>
    McpServerInfo? ServerInfo { get; }

    /// <summary>
    /// 启动本地 Server 进程并完成 MCP initialize 握手。
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 调用 MCP 的 tools/list，获取 Server 当前提供的 Tool。
    /// </summary>
    Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 调用一个无参数 MCP Tool，并返回它产生的第一段文本内容。
    /// </summary>
    Task<string> CallToolAsync(
        string toolName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 关闭 Server 的 stdin，等待子进程退出并释放进程资源。
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// MCP Client 的简化连接状态。
/// </summary>
public enum McpConnectionState
{
    /// <summary>尚未启动 Server，或已经主动断开。</summary>
    Disconnected,

    /// <summary>正在启动子进程并执行 initialize。</summary>
    Connecting,

    /// <summary>initialize 已完成，可以查询和调用 Tool。</summary>
    Connected,

    /// <summary>启动、通信或协议校验失败。</summary>
    Error
}

/// <summary>
/// Server 在 initialize 响应中提供的基本身份信息。
/// </summary>
public sealed record McpServerInfo(string Name, string Version);

/// <summary>
/// tools/list 返回并展示在页面中的最小 Tool 信息。
/// </summary>
public sealed record McpToolInfo(string Name, string Description);
