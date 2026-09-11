using System.Text.Json;
using AiChatClient.Services.Mcp;
using AiChatClient.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiChatClient.Tests;

public sealed class SerialMeasurementCoordinatorTests
{
    [Fact]
    public async Task StartAsync_ConnectsOpensSubscribesAndStartsInOrder()
    {
        var mcp = new RecordingMcpClient();
        using var coordinator = new SerialMeasurementCoordinator(
            mcp, NullLogger<SerialMeasurementCoordinator>.Instance, new SettingsPersistence());
        var settings = new SerialDeviceSettings("COM5", 115200, 8, "None", "One", "None", 3, 60);

        var result = await coordinator.StartAsync(settings);

        Assert.Equal("开始指令已发送", result);
        Assert.Equal(
            ["connect", "tool:open_serial", "subscribe:serial://measurement/latest", "tool:start_measurement"],
            mcp.Calls);
        Assert.Equal("COM5", mcp.Arguments["open_serial"]["portName"]);
        Assert.Equal(3, mcp.Arguments["start_measurement"]["frequency"]);
        Assert.Equal(60, mcp.Arguments["start_measurement"]["durationSeconds"]);
    }

    [Fact]
    public async Task ListPortsAsync_ReturnsStructuredPortNames()
    {
        var mcp = new RecordingMcpClient();
        using var coordinator = new SerialMeasurementCoordinator(
            mcp, NullLogger<SerialMeasurementCoordinator>.Instance, new SettingsPersistence());

        var ports = await coordinator.ListPortsAsync();

        Assert.Equal(["COM3", "COM5"], ports);
    }

    [Fact]
    public async Task StartSavedAsync_WithoutPort_DoesNotConnectOrGuessADevice()
    {
        var mcp = new RecordingMcpClient();
        using var coordinator = new SerialMeasurementCoordinator(
            mcp, NullLogger<SerialMeasurementCoordinator>.Instance, new SettingsPersistence());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => coordinator.StartSavedAsync());

        Assert.Contains("请先在右侧选择串口参数", exception.Message);
        Assert.Empty(mcp.Calls);
    }

    [Fact]
    public async Task SerialDevicePlugin_ReturnsConfigurationErrorToTheModel()
    {
        var mcp = new RecordingMcpClient();
        using var coordinator = new SerialMeasurementCoordinator(
            mcp, NullLogger<SerialMeasurementCoordinator>.Instance, new SettingsPersistence());
        var plugin = new SerialDevicePlugin(coordinator);

        var result = await plugin.StartMeasurementAsync();

        Assert.Contains("请先在右侧选择串口参数", result);
        Assert.Empty(mcp.Calls);
    }

    private sealed class RecordingMcpClient : IMcpClientService
    {
        public event EventHandler<McpResourceUpdatedEventArgs>? ResourceUpdated;
        public List<string> Calls { get; } = [];
        public Dictionary<string, IReadOnlyDictionary<string, object?>> Arguments { get; } = [];
        public McpConnectionState State { get; private set; }
        public McpServerInfo? ServerInfo => State == McpConnectionState.Connected ? new("test", "1") : null;

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("connect");
            State = McpConnectionState.Connected;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<McpToolInfo>>([]);

        public Task<string> CallToolAsync(string toolName, CancellationToken cancellationToken = default) =>
            Task.FromResult("");

        public Task<McpToolCallResult> CallToolAsync(
            string toolName,
            IReadOnlyDictionary<string, object?> arguments,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"tool:{toolName}");
            Arguments[toolName] = arguments;
            if (toolName == "list_serial_ports")
            {
                using var document = JsonDocument.Parse("{\"ports\":[\"COM3\",\"COM5\"]}");
                return Task.FromResult(new McpToolCallResult("", document.RootElement.Clone()));
            }
            return Task.FromResult(new McpToolCallResult(
                toolName == "start_measurement" ? "开始指令已发送" : "串口已打开", null));
        }

        public Task SubscribeResourceAsync(string uri, CancellationToken cancellationToken = default)
        {
            Calls.Add($"subscribe:{uri}");
            return Task.CompletedTask;
        }

        public Task<string> ReadResourceAsync(string uri, CancellationToken cancellationToken = default) => Task.FromResult("null");
        public Task DisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
    }

    private sealed class SettingsPersistence : ISerialDeviceSettingsPersistence
    {
        public SerialDeviceSettings Settings { get; private set; } = new(string.Empty);
        public SerialDeviceSettings Load() => Settings;
        public void Save(SerialDeviceSettings settings) => Settings = settings;
    }
}
