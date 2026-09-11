using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using AiChatClient.Services.Mcp;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AiChatClient.ViewModels;

public partial class MainViewModel
{
    private const string MeasurementResourceUri = "serial://measurement/latest";
    private readonly Queue<string> _measurementLines = new();
    private int _measurementReadRunning;
    private int _measurementRefreshRequested;
    private long _lastDisplayedMeasurementSequence;
    private bool _isSerialBusy;
    private string? _selectedSerialPort;
    private int _serialBaudRate = 115200;
    private int _serialDataBits = 8;
    private string _serialParity = "None";
    private string _serialStopBits = "One";
    private string _serialHandshake = "None";
    private int _measurementFrequency = 3;
    private int _measurementDurationSeconds = 60;
    private string _deviceStatus = "串口未连接";
    private string _serialReceiveStatus = "接收诊断：尚未收到串口字节";
    private string _serialLastFrame = "最近原始帧：—";
    private string _measurementText = "等待测量数据…";

    public ObservableCollection<string> SerialPorts { get; } = new();
    public IReadOnlyList<int> SerialBaudRates { get; } = [9600, 19200, 38400, 57600, 115200];
    public IReadOnlyList<int> SerialDataBitsOptions { get; } = [5, 6, 7, 8];
    public IReadOnlyList<string> SerialParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];
    public IReadOnlyList<string> SerialStopBitsOptions { get; } = ["One", "Two", "OnePointFive"];
    public IReadOnlyList<string> SerialHandshakeOptions { get; } = ["None", "XOnXOff", "RequestToSend", "RequestToSendXOnXOff"];

    public string? SelectedSerialPort
    {
        get => _selectedSerialPort;
        set
        {
            if (SetProperty(ref _selectedSerialPort, value))
            {
                OpenSerialCommand?.NotifyCanExecuteChanged();
                StartMeasurementCommand?.NotifyCanExecuteChanged();
            }
        }
    }
    public int SerialBaudRate { get => _serialBaudRate; set => SetProperty(ref _serialBaudRate, value); }
    public int SerialDataBits { get => _serialDataBits; set => SetProperty(ref _serialDataBits, value); }
    public string SerialParity { get => _serialParity; set => SetProperty(ref _serialParity, value); }
    public string SerialStopBits { get => _serialStopBits; set => SetProperty(ref _serialStopBits, value); }
    public string SerialHandshake { get => _serialHandshake; set => SetProperty(ref _serialHandshake, value); }
    public int MeasurementFrequency { get => _measurementFrequency; set => SetProperty(ref _measurementFrequency, value); }
    public int MeasurementDurationSeconds { get => _measurementDurationSeconds; set => SetProperty(ref _measurementDurationSeconds, value); }
    public string DeviceStatus { get => _deviceStatus; private set => SetProperty(ref _deviceStatus, value); }
    public string SerialReceiveStatus { get => _serialReceiveStatus; private set => SetProperty(ref _serialReceiveStatus, value); }
    public string SerialLastFrame { get => _serialLastFrame; private set => SetProperty(ref _serialLastFrame, value); }
    public string MeasurementText { get => _measurementText; private set => SetProperty(ref _measurementText, value); }

    public bool IsSerialBusy
    {
        get => _isSerialBusy;
        private set
        {
            if (SetProperty(ref _isSerialBusy, value)) RefreshSerialCommandStates();
        }
    }

    public IAsyncRelayCommand RefreshSerialPortsCommand { get; private set; } = null!;
    public IAsyncRelayCommand OpenSerialCommand { get; private set; } = null!;
    public IAsyncRelayCommand StartMeasurementCommand { get; private set; } = null!;
    public IAsyncRelayCommand StopMeasurementCommand { get; private set; } = null!;
    public IAsyncRelayCommand CloseSerialCommand { get; private set; } = null!;
    public IRelayCommand ClearMeasurementCommand { get; private set; } = null!;

    private void InitializeSerialControls()
    {
        var settings = SerialDeviceSettingsStore.Load();
        SelectedSerialPort = settings.PortName;
        SerialBaudRate = settings.BaudRate;
        SerialDataBits = settings.DataBits;
        SerialParity = settings.Parity;
        SerialStopBits = settings.StopBits;
        SerialHandshake = settings.Handshake;
        MeasurementFrequency = settings.Frequency;
        MeasurementDurationSeconds = settings.DurationSeconds;
        if (!string.IsNullOrWhiteSpace(settings.PortName)) SerialPorts.Add(settings.PortName);

        RefreshSerialPortsCommand = new AsyncRelayCommand(RefreshSerialPortsAsync, () => !IsSerialBusy);
        OpenSerialCommand = new AsyncRelayCommand(OpenSerialAsync,
            () => !IsSerialBusy && !string.IsNullOrWhiteSpace(SelectedSerialPort));
        StartMeasurementCommand = new AsyncRelayCommand(StartMeasurementAsync,
            () => !IsSerialBusy && !string.IsNullOrWhiteSpace(SelectedSerialPort));
        StopMeasurementCommand = new AsyncRelayCommand(StopMeasurementAsync, () => !IsSerialBusy && IsMcpConnected);
        CloseSerialCommand = new AsyncRelayCommand(CloseSerialAsync, () => !IsSerialBusy && IsMcpConnected);
        ClearMeasurementCommand = new RelayCommand(ClearMeasurement);
        _mcpClientService.ResourceUpdated += McpClientService_ResourceUpdated;
    }

    private async Task EnsureMcpConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (IsMcpConnected) return;
        await _mcpClientService.ConnectAsync(cancellationToken);
        OnPropertyChanged(nameof(IsMcpConnected));
        McpConnectionStatus = "已连接";
        var server = _mcpClientService.ServerInfo;
        McpServerDisplayName = server is null ? "未知 Server" : $"{server.Name}  v{server.Version}";
    }

    private async Task RefreshSerialPortsAsync()
    {
        await RunSerialOperationAsync(async cancellationToken =>
        {
            await EnsureMcpConnectedAsync(cancellationToken);
            var result = await _mcpClientService.CallToolAsync(
                "list_serial_ports", new Dictionary<string, object?>(), cancellationToken);
            var current = SelectedSerialPort;
            SerialPorts.Clear();
            if (result.StructuredContent is { } structured
                && structured.TryGetProperty("ports", out var ports))
            {
                foreach (var port in ports.EnumerateArray())
                    if (port.GetString() is { Length: > 0 } name) SerialPorts.Add(name);
            }
            SelectedSerialPort = current is not null && SerialPorts.Contains(current)
                ? current
                : SerialPorts.FirstOrDefault();
            DeviceStatus = SerialPorts.Count == 0 ? "未发现可用串口" : $"发现 {SerialPorts.Count} 个串口";
        });
    }

    private async Task OpenSerialAsync() => await RunSerialOperationAsync(OpenSerialCoreAsync);

    private async Task OpenSerialCoreAsync(CancellationToken cancellationToken)
    {
        var settings = CurrentSerialSettings();
        await EnsureMcpConnectedAsync(cancellationToken);
        var result = await _mcpClientService.CallToolAsync("open_serial", new Dictionary<string, object?>
        {
            ["portName"] = settings.PortName, ["baudRate"] = settings.BaudRate,
            ["dataBits"] = settings.DataBits, ["parity"] = settings.Parity,
            ["stopBits"] = settings.StopBits, ["handshake"] = settings.Handshake
        }, cancellationToken);
        SerialDeviceSettingsStore.Save(settings);
        DeviceStatus = result.Text;
    }

    private async Task StartMeasurementAsync() => await RunSerialOperationAsync(StartMeasurementCoreAsync);

    private async Task StartMeasurementCoreAsync(CancellationToken cancellationToken)
    {
        await OpenSerialCoreAsync(cancellationToken);
        await _mcpClientService.SubscribeResourceAsync(MeasurementResourceUri, cancellationToken);
        var result = await _mcpClientService.CallToolAsync("start_measurement",
            new Dictionary<string, object?>
            {
                ["frequency"] = MeasurementFrequency,
                ["durationSeconds"] = MeasurementDurationSeconds
            }, cancellationToken);
        DeviceStatus = result.Text;
    }

    private async Task StopMeasurementAsync() => await RunSerialOperationAsync(async cancellationToken =>
    {
        var result = await _mcpClientService.CallToolAsync("stop_measurement",
            new Dictionary<string, object?>(), cancellationToken);
        DeviceStatus = result.Text;
    });

    private async Task CloseSerialAsync() => await RunSerialOperationAsync(async cancellationToken =>
    {
        var result = await _mcpClientService.CallToolAsync("close_serial",
            new Dictionary<string, object?>(), cancellationToken);
        DeviceStatus = result.Text;
    });

    private async Task RunSerialOperationAsync(Func<CancellationToken, Task> operation)
    {
        if (IsSerialBusy) return;
        IsSerialBusy = true;
        try { await operation(CancellationToken.None); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "串口设备操作失败。");
            DeviceStatus = $"操作失败：{exception.Message}";
        }
        finally
        {
            IsSerialBusy = false;
            OnPropertyChanged(nameof(IsMcpConnected));
        }
    }

    private SerialDeviceSettings CurrentSerialSettings()
    {
        if (string.IsNullOrWhiteSpace(SelectedSerialPort))
            throw new InvalidOperationException("请先选择串口。");
        if (MeasurementFrequency is < 1 or > 50)
            throw new InvalidOperationException("频率必须在 1 到 50 之间。");
        if (MeasurementDurationSeconds is < 1 or > 9999)
            throw new InvalidOperationException("测定时间必须在 1 到 9999 秒之间。");
        return new SerialDeviceSettings(SelectedSerialPort, SerialBaudRate, SerialDataBits,
            SerialParity, SerialStopBits, SerialHandshake,
            MeasurementFrequency, MeasurementDurationSeconds);
    }

    private async Task<string> ExecuteDeviceIntentAsync(DeviceIntent intent, CancellationToken cancellationToken)
    {
        try
        {
            if (intent == DeviceIntent.Start) await StartMeasurementCoreAsync(cancellationToken);
            else
            {
                await EnsureMcpConnectedAsync(cancellationToken);
                var result = await _mcpClientService.CallToolAsync("stop_measurement",
                    new Dictionary<string, object?>(), cancellationToken);
                DeviceStatus = result.Text;
            }
            return DeviceStatus;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "对话设备指令执行失败：{Intent}", intent);
            DeviceStatus = $"设备操作失败：{exception.Message}";
            return DeviceStatus;
        }
    }

    internal static bool TryGetDeviceIntent(string input, out DeviceIntent intent)
    {
        var normalized = input.Trim().TrimEnd('。', '！', '!').Trim();
        if (normalized is "大激发开始测量" or "大激发开始测定")
        {
            intent = DeviceIntent.Start; return true;
        }
        if (normalized is "大激发停止测量" or "大激发停止测定")
        {
            intent = DeviceIntent.Stop; return true;
        }
        intent = default; return false;
    }

    private void McpClientService_ResourceUpdated(object? sender, McpResourceUpdatedEventArgs e)
    {
        if (e.Uri != MeasurementResourceUri) return;
        Interlocked.Exchange(ref _measurementRefreshRequested, 1);
        if (Interlocked.Exchange(ref _measurementReadRunning, 1) == 0)
            _ = ReadLatestMeasurementAsync();
    }

    private async Task ReadLatestMeasurementAsync()
    {
        try
        {
            do
            {
                Interlocked.Exchange(ref _measurementRefreshRequested, 0);
                var text = await _mcpClientService.ReadResourceAsync(MeasurementResourceUri);
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                var diagnostics = root.GetProperty("diagnostics");
                var bytesReceived = diagnostics.GetProperty("bytesReceived").GetInt64();
                var framesReceived = diagnostics.GetProperty("framesReceived").GetInt64();
                var measurementsParsed = diagnostics.GetProperty("measurementsParsed").GetInt64();
                var lastFrameLength = diagnostics.GetProperty("lastFrameLength").GetInt32();
                var lastFrameHexElement = diagnostics.GetProperty("lastFrameHex");
                var lastFrameHex = lastFrameHexElement.ValueKind == JsonValueKind.String
                    ? lastFrameHexElement.GetString()
                    : null;
                var lastReceiveHexElement = diagnostics.GetProperty("lastReceiveHex");
                var lastReceiveHex = lastReceiveHexElement.ValueKind == JsonValueKind.String
                    ? lastReceiveHexElement.GetString()
                    : null;
                string? line = null;
                if (root.TryGetProperty("sequence", out var sequenceElement)
                    && sequenceElement.ValueKind == JsonValueKind.Number
                    && sequenceElement.TryGetInt64(out var sequence)
                    && sequence != Interlocked.Read(ref _lastDisplayedMeasurementSequence)
                    && root.TryGetProperty("flowRaw", out var flowElement)
                    && flowElement.ValueKind == JsonValueKind.Number)
                {
                    var timestamp = root.GetProperty("timestamp").GetDateTimeOffset().ToLocalTime();
                    var pressureElement = root.GetProperty("mouthPressureRaw");
                    var pressure = pressureElement.ValueKind == JsonValueKind.Null
                        ? "--"
                        : pressureElement.GetInt32().ToString();
                    line = $"{timestamp:HH:mm:ss.fff}  流速(raw): {flowElement.GetInt32()}  |  口压(raw): {pressure}";
                    Interlocked.Exchange(ref _lastDisplayedMeasurementSequence, sequence);
                }
                var dispatcher = Application.Current?.Dispatcher;
                void UpdateUi()
                {
                    SerialReceiveStatus = $"接收诊断：{bytesReceived} 字节 / {framesReceived} 完整帧 / {measurementsParsed} 条有效数据"
                        + (lastFrameLength > 0 ? $" / 最近帧 {lastFrameLength} 字节" : string.Empty);
                    SerialLastFrame = !string.IsNullOrWhiteSpace(lastFrameHex)
                        ? $"最近完整帧：{FormatHex(lastFrameHex)}"
                        : !string.IsNullOrWhiteSpace(lastReceiveHex)
                            ? $"最近接收片段：{FormatHex(lastReceiveHex)}"
                            : "最近原始帧：—";
                    if (line is not null) AppendMeasurementLine(line);
                }
                if (dispatcher is null || dispatcher.CheckAccess()) UpdateUi();
                else await dispatcher.InvokeAsync(UpdateUi);
            }
            while (Interlocked.CompareExchange(ref _measurementRefreshRequested, 0, 0) != 0);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "读取最新测量资源失败。");
        }
        finally
        {
            Interlocked.Exchange(ref _measurementReadRunning, 0);
            if (Interlocked.CompareExchange(ref _measurementRefreshRequested, 0, 0) != 0
                && Interlocked.Exchange(ref _measurementReadRunning, 1) == 0)
                _ = ReadLatestMeasurementAsync();
        }
    }

    private void AppendMeasurementLine(string line)
    {
        if (_measurementLines.Count == 1000) _measurementLines.Dequeue();
        _measurementLines.Enqueue(line);
        MeasurementText = string.Join(Environment.NewLine, _measurementLines);
    }

    private void ClearMeasurement()
    {
        _measurementLines.Clear();
        MeasurementText = "等待测量数据…";
    }

    private static string FormatHex(string compactHex)
    {
        if (compactHex.Length < 2) return compactHex;
        return string.Join(' ', Enumerable.Range(0, compactHex.Length / 2)
            .Select(index => compactHex.Substring(index * 2, 2)));
    }

    private void RefreshSerialCommandStates()
    {
        RefreshSerialPortsCommand?.NotifyCanExecuteChanged();
        OpenSerialCommand?.NotifyCanExecuteChanged();
        StartMeasurementCommand?.NotifyCanExecuteChanged();
        StopMeasurementCommand?.NotifyCanExecuteChanged();
        CloseSerialCommand?.NotifyCanExecuteChanged();
    }
}

internal enum DeviceIntent { Start, Stop }
