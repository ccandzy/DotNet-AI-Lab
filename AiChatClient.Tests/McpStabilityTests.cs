using System.Diagnostics;
using System.Text;
using AiChatClient.Services.Mcp;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiChatClient.Tests;

public sealed class McpStabilityTests
{
    [Theory]
    [InlineData("timeout")]
    [InlineData("wrong-id")]
    [InlineData("bad-json")]
    public async Task CommunicationFailure_ClosesProcessAndReconnects(string fault)
    {
        var attempts = 0;
        using var client = new McpClientService(NullLogger<McpClientService>.Instance,
            () => StartInfo(++attempts == 1 ? fault : "ok"), TimeSpan.FromSeconds(3));
        await client.ConnectAsync();
        var info = client.ServerInfo!;
        var pid = int.Parse(info.Version);
        var error = await Record.ExceptionAsync(() => client.CallToolAsync("get_current_time"));
        Assert.NotNull(error);
        if (fault == "timeout") Assert.IsType<TimeoutException>(error);
        Assert.Equal(McpConnectionState.Error, client.State);
        Assert.Null(client.ServerInfo);
        Assert.False(IsRunning(pid));
        await client.ConnectAsync();
        Assert.Equal("ok", await client.CallToolAsync("get_current_time"));
        var newPid = int.Parse(client.ServerInfo!.Version);
        await client.DisconnectAsync();
        Assert.False(IsRunning(newPid));
    }

    [Fact]
    public async Task ToolBusinessError_KeepsConnectionUsable()
    {
        using var client = new McpClientService(NullLogger<McpClientService>.Instance,
            () => StartInfo("business-error"), TimeSpan.FromSeconds(3));
        await client.ConnectAsync();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => client.CallToolAsync("get_current_time"));
        Assert.Equal(McpConnectionState.Connected, client.State);
        Assert.Equal("ok", await client.CallToolAsync("get_current_time"));
        await client.DisconnectAsync();
    }

    private static bool IsRunning(int pid)
    {
        try { using var process = Process.GetProcessById(pid); return !process.HasExited; }
        catch (ArgumentException) { return false; }
    }

    private static ProcessStartInfo StartInfo(string fault)
    {
        // A bounded stdio test peer, never a network server. The client owns and cleans up this child.
        var script = "[Console]::InputEncoding=[Text.UTF8Encoding]::new($false);[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);" +
            "$fault='" + fault + "'; $calls=0;" + """
            while ($null -ne ($line=[Console]::ReadLine())) {
                $request=$line | ConvertFrom-Json
                if ($request.method -eq 'notifications/initialized') { continue }
                $id=$request.id
                if ($request.method -eq 'initialize') {
                    $result=@{protocolVersion='2025-11-25';serverInfo=@{name='test';version=[string]$PID}}
                } else {
                    $calls++
                    if ($calls -eq 1) {
                        if ($fault -eq 'timeout') { Start-Sleep -Seconds 20 }
                        if ($fault -eq 'wrong-id') { $id=9999 }
                        if ($fault -eq 'bad-json') { [Console]::WriteLine('broken'); continue }
                    }
                    $result=@{content=@(@{type='text';text='ok'});isError=($fault -eq 'business-error' -and $calls -eq 1)}
                }
                $response=@{jsonrpc='2.0';id=$id;result=$result} | ConvertTo-Json -Depth 8 -Compress
                [Console]::WriteLine($response)
                [Console]::Out.Flush()
            }
            """;
        var info = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };
        info.ArgumentList.Add("-NoProfile");
        info.ArgumentList.Add("-NonInteractive");
        info.ArgumentList.Add("-EncodedCommand");
        info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        return info;
    }
}
