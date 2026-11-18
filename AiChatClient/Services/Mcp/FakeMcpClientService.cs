using System;
using System.Collections.Generic;
using System.Text;
using AiChatClient.Services.Mcp;

namespace Services.Mcp
{
    internal sealed class FakeMcpClientService : IMcpClientService
    {
        public McpConnectionState State => McpConnectionState.Connected;

        public McpServerInfo? ServerInfo =>
            new("FakeMcpServer", "1.0");

        public string? LastCalledToolName { get; private set; }

        public Task ConnectAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<McpToolInfo> tools =
            [
                new McpToolInfo(
                "get_current_time",
                "获取当前本地时间")
            ];

            return Task.FromResult(tools);
        }

        public Task<string> CallToolAsync(
            string toolName,
            CancellationToken cancellationToken = default)
        {
            LastCalledToolName = toolName;

            return Task.FromResult(
                "2026-10-04T21:30:00+08:00");
        }

        public Task<IReadOnlyList<McpResourceInfo>> ListResourcesAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<McpResourceInfo>>(
                new[] { new McpResourceInfo("project://readme", "项目说明", "text/markdown") });
        }

        public Task<string> ReadResourceAsync(
            string uri,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return uri == "project://readme"
                ? Task.FromResult("模拟资源正文")
                : Task.FromException<string>(new InvalidOperationException("Resource 不存在。"));
        }

        public Task DisconnectAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
