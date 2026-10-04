using System.ComponentModel;
using AiChatClient.Services.Mcp;
using Microsoft.SemanticKernel;

namespace AiChatClient.Services.Mcp;

internal sealed class McpKernelFunctionAdapter
{
    private readonly IMcpClientService _mcpClientService;

    public McpKernelFunctionAdapter(
        IMcpClientService mcpClientService)
    {
        _mcpClientService = mcpClientService;
    }

    [KernelFunction("mcp_current_time")]
    [Description("通过 MCP Server 获取当前本地时间。")]
    public async Task<string> GetCurrentTime(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await _mcpClientService.CallToolAsync(
            "get_current_time",
            cancellationToken);
    }
}