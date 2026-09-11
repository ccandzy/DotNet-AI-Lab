using System.IO.Ports;

namespace AiChatClient.McpServer.Serial;

internal sealed class DeviceSerialService : IAsyncDisposable
{
    private static readonly byte[] StartCommand =
        [0x34, 0x30, 0x30, 0x30, 0x30, 0x33, 0x0D, 0x0A];
    private static readonly byte[] StopCommand =
        [0x34, 0x31, 0x30, 0x30, 0x0D, 0x0A];
    private static readonly TimeSpan StartResponseTimeout = TimeSpan.FromSeconds(3);

    private readonly MeasurementFrameParser _parser = new();
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private SerialPort? _port;
    private CancellationTokenSource? _readCancellation;
    private Task? _readTask;
    private TaskCompletionSource<MeasurementReading>? _firstMeasurement;

    public event Action<MeasurementReading>? MeasurementReceived;

    internal static ReadOnlyMemory<byte> StartProtocolCommand => StartCommand;
    internal static ReadOnlyMemory<byte> StopProtocolCommand => StopCommand;

    public DeviceMeasurementState State { get; private set; } =
        DeviceMeasurementState.Disconnected;

    public SerialConnectionOptions? ConnectionOptions { get; private set; }

    public MeasurementReading? LatestMeasurement { get; private set; }

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
                _parser.Reset();
                _readCancellation = new CancellationTokenSource();
                _readTask = ReadLoopAsync(port, _readCancellation.Token);
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

    public async Task<MeasurementReading> StartMeasurementAsync(
        CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        Task<MeasurementReading> firstMeasurementTask;
        try
        {
            var port = EnsureOpen();
            if (State == DeviceMeasurementState.Measuring && LatestMeasurement is not null)
            {
                return LatestMeasurement;
            }

            State = DeviceMeasurementState.Starting;
            _parser.Reset();
            _firstMeasurement = new TaskCompletionSource<MeasurementReading>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            firstMeasurementTask = _firstMeasurement.Task;
            await port.BaseStream.WriteAsync(StartCommand, cancellationToken);
            await port.BaseStream.FlushAsync(cancellationToken);
        }
        finally
        {
            _operationLock.Release();
        }

        try
        {
            var reading = await firstMeasurementTask.WaitAsync(
                StartResponseTimeout,
                cancellationToken);
            State = DeviceMeasurementState.Measuring;
            return reading;
        }
        catch
        {
            await TrySendStopAfterFailedStartAsync();
            State = DeviceMeasurementState.PortOpen;
            throw;
        }
        finally
        {
            _firstMeasurement = null;
        }
    }

    public async Task StopMeasurementAsync(CancellationToken cancellationToken)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var port = EnsureOpen();
            await port.BaseStream.WriteAsync(StopCommand, cancellationToken);
            await port.BaseStream.FlushAsync(cancellationToken);
            State = DeviceMeasurementState.PortOpen;
            _firstMeasurement?.TrySetCanceled(cancellationToken);
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
                    await port.BaseStream.WriteAsync(StopCommand, cancellationToken);
                    await port.BaseStream.FlushAsync(cancellationToken);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"关闭前发送停止命令失败：{exception.Message}");
                }
            }

            _readCancellation?.Cancel();
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

        if (_readTask is not null)
        {
            try { await _readTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken); }
            catch (Exception) { }
        }

        _readCancellation?.Dispose();
        _readCancellation = null;
        _readTask = null;
    }

    private async Task ReadLoopAsync(SerialPort port, CancellationToken cancellationToken)
    {
        var buffer = new byte[256];
        try
        {
            while (!cancellationToken.IsCancellationRequested && port.IsOpen)
            {
                var count = await port.BaseStream.ReadAsync(buffer, cancellationToken);
                if (count == 0) continue;
                foreach (var reading in _parser.Append(buffer.AsSpan(0, count)))
                {
                    LatestMeasurement = reading;
                    _firstMeasurement?.TrySetResult(reading);
                    MeasurementReceived?.Invoke(reading);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            State = DeviceMeasurementState.Faulted;
            _firstMeasurement?.TrySetException(exception);
            Console.Error.WriteLine($"串口读取失败：{exception}");
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

    private async Task TrySendStopAfterFailedStartAsync()
    {
        try
        {
            if (_port is { IsOpen: true } port)
            {
                await port.BaseStream.WriteAsync(StopCommand);
                await port.BaseStream.FlushAsync();
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"启动失败后发送停止命令失败：{exception.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync(CancellationToken.None);
        _operationLock.Dispose();
    }
}
