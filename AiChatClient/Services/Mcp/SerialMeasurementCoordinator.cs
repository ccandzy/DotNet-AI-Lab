using Microsoft.Extensions.Logging;

namespace AiChatClient.Services.Mcp;

internal sealed class SerialMeasurementCoordinator : ISerialMeasurementCoordinator, IDisposable
{
    internal const string MeasurementResourceUri = "serial://measurement/latest";

    private readonly IMcpClientService _mcpClient;
    private readonly ILogger<SerialMeasurementCoordinator> _logger;
    private readonly ISerialDeviceSettingsPersistence _settingsPersistence;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _disposed;

    public SerialMeasurementCoordinator(
        IMcpClientService mcpClient,
        ILogger<SerialMeasurementCoordinator> logger,
        ISerialDeviceSettingsPersistence settingsPersistence)
    {
        _mcpClient = mcpClient;
        _logger = logger;
        _settingsPersistence = settingsPersistence;
    }

    public event EventHandler<SerialDeviceStatusChangedEventArgs>? StatusChanged;

    public Task<IReadOnlyList<string>> ListPortsAsync(CancellationToken cancellationToken = default) =>
        RunExclusiveAsync(async token =>
        {
            await EnsureConnectedCoreAsync(token);
            var result = await _mcpClient.CallToolAsync(
                "list_serial_ports", new Dictionary<string, object?>(), token);
            var ports = new List<string>();
            if (result.StructuredContent is { } structured
                && structured.TryGetProperty("ports", out var portArray))
            {
                foreach (var port in portArray.EnumerateArray())
                    if (port.GetString() is { Length: > 0 } name) ports.Add(name);
            }

            PublishStatus(ports.Count == 0 ? "未发现可用串口" : $"发现 {ports.Count} 个串口");
            return (IReadOnlyList<string>)ports;
        }, cancellationToken);

    public Task<string> OpenAsync(
        SerialDeviceSettings settings,
        CancellationToken cancellationToken = default) =>
        RunExclusiveAsync(token => OpenCoreAsync(settings, token), cancellationToken);

    public Task<string> OpenSavedAsync(CancellationToken cancellationToken = default) =>
        OpenAsync(LoadSavedSettings(), cancellationToken);

    public Task<string> StartAsync(
        SerialDeviceSettings settings,
        CancellationToken cancellationToken = default) =>
        RunExclusiveAsync(async token =>
        {
            await OpenCoreAsync(settings, token);
            await _mcpClient.SubscribeResourceAsync(MeasurementResourceUri, token);
            var result = await _mcpClient.CallToolAsync("start_measurement",
                new Dictionary<string, object?>
                {
                    ["frequency"] = settings.Frequency,
                    ["durationSeconds"] = settings.DurationSeconds
                }, token);
            PublishStatus(result.Text);
            return result.Text;
        }, cancellationToken);

    public Task<string> StartSavedAsync(CancellationToken cancellationToken = default) =>
        StartAsync(LoadSavedSettings(), cancellationToken);

    public Task<string> StopAsync(CancellationToken cancellationToken = default) =>
        CallSimpleToolAsync("stop_measurement", cancellationToken);

    public Task<string> CloseAsync(CancellationToken cancellationToken = default) =>
        CallSimpleToolAsync("close_serial", cancellationToken);

    public Task<string> GetStatusAsync(CancellationToken cancellationToken = default) =>
        CallSimpleToolAsync("get_device_status", cancellationToken);

    private Task<string> CallSimpleToolAsync(string name, CancellationToken cancellationToken) =>
        RunExclusiveAsync(async token =>
        {
            await EnsureConnectedCoreAsync(token);
            var result = await _mcpClient.CallToolAsync(
                name, new Dictionary<string, object?>(), token);
            PublishStatus(result.Text);
            return result.Text;
        }, cancellationToken);

    private async Task<string> OpenCoreAsync(SerialDeviceSettings settings, CancellationToken cancellationToken)
    {
        ValidateSettings(settings);
        await EnsureConnectedCoreAsync(cancellationToken);
        var result = await _mcpClient.CallToolAsync("open_serial", new Dictionary<string, object?>
        {
            ["portName"] = settings.PortName,
            ["baudRate"] = settings.BaudRate,
            ["dataBits"] = settings.DataBits,
            ["parity"] = settings.Parity,
            ["stopBits"] = settings.StopBits,
            ["handshake"] = settings.Handshake
        }, cancellationToken);
        _settingsPersistence.Save(settings);
        PublishStatus(result.Text);
        return result.Text;
    }

    private async Task EnsureConnectedCoreAsync(CancellationToken cancellationToken)
    {
        if (_mcpClient.State == McpConnectionState.Connected) return;
        await _mcpClient.ConnectAsync(cancellationToken);
    }

    private SerialDeviceSettings LoadSavedSettings()
    {
        var settings = _settingsPersistence.Load();
        ValidateSettings(settings);
        return settings;
    }

    private static void ValidateSettings(SerialDeviceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(settings.PortName))
            throw new InvalidOperationException("请先在右侧选择串口参数并打开一次串口。");
        if (settings.Frequency is < 1 or > 50)
            throw new InvalidOperationException("频率必须在 1 到 50 之间。");
        if (settings.DurationSeconds is < 1 or > 9999)
            throw new InvalidOperationException("测定时间必须在 1 到 9999 秒之间。");
    }

    private async Task<T> RunExclusiveAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            return await operation(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "串口协调操作失败。");
            PublishStatus($"操作失败：{exception.Message}");
            throw;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private void PublishStatus(string status) =>
        StatusChanged?.Invoke(this, new SerialDeviceStatusChangedEventArgs(status));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _operationLock.Dispose();
    }
}
