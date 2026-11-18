using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Services.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Services.Mcp;

namespace AiChatClient.Tests
{
    public class McpKernelFunctionAdapterTests
    {

            [Fact]
            public async Task McpKernelFunctionAdapterCallTest()
            {
                var mcpClientService =
                    new FakeMcpClientService();

                var kernel = Kernel.CreateBuilder().Build();

                kernel.Plugins.AddFromObject(
                    new McpKernelFunctionAdapter(mcpClientService),
                    "Mcp");

                var function = kernel.Plugins.GetFunction(
                    "Mcp",
                    "mcp_current_time");

                var result = await kernel.InvokeAsync(function);

                Assert.Equal(
                    "2026-10-04T21:30:00+08:00",
                    result.ToString());

                Assert.Equal(
                    "get_current_time",
                    mcpClientService.LastCalledToolName);
            }

            [Fact]
            public async Task McpKernelFunctionAdapterListResourcesTest()
            {
                var mcpClientService =
                    new FakeMcpClientService();

                var kernel = Kernel.CreateBuilder().Build();

                kernel.Plugins.AddFromObject(
                    new McpKernelFunctionAdapter(mcpClientService),
                    "MCP");

                var function = kernel.Plugins.GetFunction(
                    "MCP",
                    "mcp_list_resources");

                var result = await kernel.InvokeAsync(function);

                Assert.Equal(
                    "project://readme：项目说明",
                    result.ToString());
            }

            [Fact]
            public async Task McpKernelFunctionAdapterReadResourceTest()
            {
                var mcpClientService =
                    new FakeMcpClientService();

                var kernel = Kernel.CreateBuilder().Build();

                kernel.Plugins.AddFromObject(
                    new McpKernelFunctionAdapter(mcpClientService),
                    "MCP");

                var function = kernel.Plugins.GetFunction(
                    "MCP",
                    "mcp_read_resource");

                var result = await kernel.InvokeAsync(function, new()
                {
                    ["uri"] = "project://readme"
                });

                Assert.Equal(
                    "模拟资源正文",
                    result.ToString());
            }

            [Fact]
            public async Task McpKernelFunctionAdapterReadResourceInvalidUriTest()
            {
                var mcpClientService =
                    new FakeMcpClientService();

                var kernel = Kernel.CreateBuilder().Build();

                kernel.Plugins.AddFromObject(
                    new McpKernelFunctionAdapter(mcpClientService),
                    "MCP");

                var function = kernel.Plugins.GetFunction(
                    "MCP",
                    "mcp_read_resource");

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => kernel.InvokeAsync(function, new()
                    {
                        ["uri"] = "project://unknown"
                    }));
            }
        }

}
