namespace AiChatClient.McpServer.Serial;

internal sealed class MeasurementFrameParser
{
    internal const int LegacyFrameLength = 30;
    internal const int CurrentFrameLength = 22;
    private const ushort StartMeasurementAddress = 0x0FA0; // 十进制 4000
    private const int MaximumFrameLength = 4096;
    private readonly List<byte> _buffer = new();
    private long _sequence;

    public event Action<ReadOnlyMemory<byte>>? FrameReceived;

    public IReadOnlyList<MeasurementReading> Append(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            _buffer.Add(value);
        }

        var results = new List<MeasurementReading>();
        while (TryTakeFrame(out var frame))
        {
            FrameReceived?.Invoke(frame);
            var address = frame.Length >= 4
                ? (ushort)((frame[2] << 8) | frame[3])
                : (ushort)0;
            var parsed = (frame.Length, address) switch
            {
                (CurrentFrameLength, StartMeasurementAddress) =>
                    TryReadSignedBigEndian(frame[9], frame[10], frame[11], out var flow)
                    ? (Success: true, Flow: flow, Pressure: (int?)null)
                    : default,
                (>= LegacyFrameLength, StartMeasurementAddress) =>
                    TryReadSignedBigEndian(frame[14], frame[15], frame[16], out var flow)
                    && TryReadSignedBigEndian(frame[17], frame[18], frame[19], out var pressure)
                    ? (Success: true, Flow: flow, Pressure: (int?)pressure)
                    : default,
                _ => default
            };
            if (!parsed.Success)
            {
                continue;
            }

            results.Add(new MeasurementReading(
                Interlocked.Increment(ref _sequence),
                DateTimeOffset.Now,
                parsed.Flow,
                parsed.Pressure));
        }

        return results;
    }

    public void Reset() => _buffer.Clear();

    private bool TryTakeFrame(out byte[] frame)
    {
        frame = Array.Empty<byte>();
        while (_buffer.Count >= 2
               && (_buffer[0] != 0x5A || _buffer[1] != 0xA5))
        {
            _buffer.RemoveAt(0);
        }

        if (_buffer.Count < 4)
        {
            return false;
        }

        // 与已验证项目的 FrameProcessor 一致：收到帧头后持续收集，直到 A5 5A 帧尾。
        // 组帧不假设串口一次读取的长度；帧提取后再按地址和固定字段前缀解析。
        for (var index = 2; index < _buffer.Count - 1; index++)
        {
            if (_buffer[index] == 0x5A && _buffer[index + 1] == 0xA5)
            {
                // 前一帧缺少帧尾时，从新的帧头恢复，避免把两帧误拼成扩展帧。
                _buffer.RemoveRange(0, index);
                return TryTakeFrame(out frame);
            }
            if (_buffer[index] != 0xA5 || _buffer[index + 1] != 0x5A) continue;
            var frameLength = index + 2;
            frame = _buffer.GetRange(0, frameLength).ToArray();
            _buffer.RemoveRange(0, frameLength);
            return true;
        }

        if (_buffer.Count <= MaximumFrameLength) return false;
        _buffer.RemoveAt(0);
        return TryTakeFrame(out frame);
    }

    private static bool TryReadSignedBigEndian(
        byte sign,
        byte high,
        byte low,
        out int value)
    {
        var magnitude = (high << 8) | low;
        switch (sign)
        {
            case 0x00:
                value = magnitude;
                return true;
            case 0x01:
                value = -magnitude;
                return true;
            default:
                value = 0;
                return false;
        }
    }
}
