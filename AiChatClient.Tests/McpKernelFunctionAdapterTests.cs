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
        }

}
