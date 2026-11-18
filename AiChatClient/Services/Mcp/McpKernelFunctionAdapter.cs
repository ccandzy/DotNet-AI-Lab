using System.ComponentModel;
using System.Linq;
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

    [KernelFunction("mcp_list_resources")]
    [Description("列出 MCP Server 当前提供的资源目录，每行包含资源 URI 和名称，不包含正文。")]
    public async Task<string> ListResources(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var resources = await _mcpClientService.ListResourcesAsync(cancellationToken);
        if (resources.Count == 0)
        {
            return "MCP Server 当前没有可用资源。";
        }

        return string.Join(
            Environment.NewLine,
            resources.Select(resource => $"{resource.Uri}：{resource.Name}"));
    }

    [KernelFunction("mcp_read_resource")]
    [Description("按 URI 读取 MCP Server 上的一份文本资源并返回正文。可用 URI 请先调用 mcp_list_resources 查询。")]
    public async Task<string> ReadResource(
        [Description("资源 URI，例如 project://readme")] string uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        cancellationToken.ThrowIfCancellationRequested();

        return await _mcpClientService.ReadResourceAsync(uri, cancellationToken);
    }
}