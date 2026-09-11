using System.IO.Ports;

namespace AiChatClient.McpServer.Serial;

internal sealed class DeviceSerialService : IAsyncDisposable
{
    private static readonly byte[] StopCommand =
        [0x34, 0x31, 0x30, 0x30, 0x0D, 0x0A];
    private readonly MeasurementFrameParser _parser = new();
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly object _receiveLock = new();
    private SerialPort? _port;
    private long _activitySequence;
    private long _bytesReceived;
    private long _framesReceived;
    private long _measurementsParsed;
    private int _lastFrameLength;
    private string? _lastFrameHex;
    private string? _lastReceiveHex;
    private DateTimeOffset? _lastActivityAt;

    public event Action<MeasurementReading>? MeasurementReceived;
    public event Action<long>? SerialActivity;

    public DeviceSerialService() => _parser.FrameReceived += OnFrameReceived;

    internal static ReadOnlyMemory<byte> StopProtocolCommand => StopCommand;

    internal static byte[] BuildStartProtocolCommand(int frequency, int durationSeconds)
    {
        if (frequency is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(frequency), "频率必须在 1 到 50 之间。");
        if (durationSeconds is < 1 or > 9999)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds), "测定时间必须在 1 到 9999 秒之间。");
        return System.Text.Encoding.ASCII.GetBytes($"4000{frequency:D2}{durationSeconds:D4}\r\n");
    }

    public DeviceMeasurementState State { get; private set; } =
        DeviceMeasurementState.Disconnected;

    public SerialConnectionOptions? ConnectionOptions { get; private set; }

    public MeasurementReading? LatestMeasurement { get; private set; }

    public SerialReceiveDiagnostics Diagnostics
    {
        get
        {
            lock (_receiveLock)
            {
                return new SerialReceiveDiagnostics(
                    _activitySequence, _bytesReceived, _framesReceived, _measurementsParsed,
                    _lastFrameLength, _lastFrameHex, _lastReceiveHex, _lastActivityAt);
            }
        }
    }

    public static IReadOnlyList<string> GetPortNames() =>
        SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public async Task OpenAsync(
        SerialConnectionOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(options.PortName);
        if (options.BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(options.BaudRate));
        if (options.DataBits is < 5 or > 8) throw new ArgumentOutOfRangeException(nameof(options.DataBits));

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            if (_port is { IsOpen: true })
            {
                if (ConnectionOptions == options) return;
                throw new InvalidOperationException("串口已经打开，请先关闭后再修改参数。");
            }

            var port = new SerialPort(
                options.PortName,
                options.BaudRate,
                options.Parity,
                options.DataBits,
                options.StopBits)
            {
                Handshake = options.Handshake,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            try
            {
                port.Open();
                _port = port;
                ConnectionOptions = options;
                State = DeviceMeasurementState.PortOpen;
                ResetReceiveState();
                port.DataReceived += Port_DataReceived;
            }
            catch
            {
                port.Dispose();
                throw;
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task StartMeasurementAsync(
        int frequency,
        int durationSeconds,
        CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var port = EnsureOpen();
            if (State == DeviceMeasurementState.Measuring) return;

            State = DeviceMeasurementState.Starting;
            ResetReceiveState();
            var command = BuildStartProtocolCommand(frequency, durationSeconds);
            port.Write(command, 0, command.Length);
            // 设备没有独立的“开始成功”回复；串口写入成功即表示命令已下发。
            // 测量数据通过 DataReceived 事件异步到达，不能在这里等待数据并超时停机。
            State = DeviceMeasurementState.Measuring;
            SerialActivity?.Invoke(Diagnostics.ActivitySequence);
        }
        catch { State = DeviceMeasurementState.PortOpen; throw; }
        finally { _operationLock.Release(); }
    }

    public async Task StopMeasurementAsync(CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var port = EnsureOpen();
            port.Write(StopCommand, 0, StopCommand.Length);
            State = DeviceMeasurementState.PortOpen;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var port = _port;
            if (port is null)
            {
                State = DeviceMeasurementState.Disconnected;
                return;
            }

            if (port.IsOpen && State is DeviceMeasurementState.Starting or DeviceMeasurementState.Measuring)
            {
                try
                {
                    port.Write(StopCommand, 0, StopCommand.Length);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"关闭前发送停止命令失败：{exception.Message}");
                }
            }

            port.DataReceived -= Port_DataReceived;
            port.Close();
            port.Dispose();
            _port = null;
            ConnectionOptions = null;
            State = DeviceMeasurementState.Disconnected;
            LatestMeasurement = null;
        }
        finally
        {
            _operationLock.Release();
        }

    }

    private void Port_DataReceived(object sender, SerialDataReceivedEventArgs eventArgs)
    {
        try
        {
            if (sender is not SerialPort port || !port.IsOpen) return;
            var available = port.BytesToRead;
            if (available <= 0) return;
            var buffer = new byte[available];
            var count = port.Read(buffer, 0, buffer.Length);
            if (count <= 0) return;

            IReadOnlyList<MeasurementReading> readings;
            long activitySequence;
            lock (_receiveLock)
            {
                _bytesReceived += count;
                _lastActivityAt = DateTimeOffset.Now;
                _lastReceiveHex = Convert.ToHexString(buffer.AsSpan(0, count));
                readings = _parser.Append(buffer.AsSpan(0, count));
                _measurementsParsed += readings.Count;
                activitySequence = ++_activitySequence;
            }

            foreach (var reading in readings)
            {
                LatestMeasurement = reading;
                MeasurementReceived?.Invoke(reading);
            }
            SerialActivity?.Invoke(activitySequence);
        }
        catch (Exception exception)
        {
            State = DeviceMeasurementState.Faulted;
            Console.Error.WriteLine($"串口读取失败：{exception}");
            var sequence = Interlocked.Increment(ref _activitySequence);
            SerialActivity?.Invoke(sequence);
        }
    }

    private void OnFrameReceived(ReadOnlyMemory<byte> frame)
    {
        // MeasurementFrameParser is invoked while _receiveLock is held.
        _framesReceived++;
        _lastFrameLength = frame.Length;
        _lastFrameHex = Convert.ToHexString(frame.Span);
    }

    private void ResetReceiveState()
    {
        lock (_receiveLock)
        {
            _parser.Reset();
            LatestMeasurement = null;
            _bytesReceived = 0;
            _framesReceived = 0;
            _measurementsParsed = 0;
            _lastFrameLength = 0;
            _lastFrameHex = null;
            _lastReceiveHex = null;
            _lastActivityAt = null;
            _activitySequence++;
        }
    }

    private SerialPort EnsureOpen()
    {
        if (_port is not { IsOpen: true })
        {
            throw new InvalidOperationException("串口尚未打开。");
        }

        return _port;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);
        _parser.FrameReceived -= OnFrameReceived;
        _operationLock.Dispose();
    }
}

internal sealed record SerialReceiveDiagnostics(
    long ActivitySequence,
    long BytesReceived,
    long FramesReceived,
    long MeasurementsParsed,
    int LastFrameLength,
    string? LastFrameHex,
    string? LastReceiveHex,
    DateTimeOffset? LastActivityAt);
