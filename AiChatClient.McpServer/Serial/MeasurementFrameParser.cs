namespace AiChatClient.McpServer.Serial;

internal sealed class MeasurementFrameParser
{
    internal const int LegacyFrameLength = 30;
    internal const int CurrentFrameLength = 22;
    private readonly List<byte> _buffer = new();
    private long _sequence;

    public IReadOnlyList<MeasurementReading> Append(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            _buffer.Add(value);
        }

        var results = new List<MeasurementReading>();
        while (TryTakeFrame(out var frame))
        {
            var parsed = frame.Length switch
            {
                LegacyFrameLength => TryReadSignedBigEndian(frame[14], frame[15], frame[16], out var flow)
                    && TryReadSignedBigEndian(frame[17], frame[18], frame[19], out var pressure)
                    ? (Success: true, Flow: flow, Pressure: (int?)pressure)
                    : default,
                CurrentFrameLength => TryReadSignedBigEndian(frame[9], frame[10], frame[11], out var flow)
                    ? (Success: true, Flow: flow, Pressure: (int?)null)
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

        if (_buffer.Count < CurrentFrameLength)
        {
            return false;
        }

        var frameLength = _buffer[20] == 0xA5 && _buffer[21] == 0x5A
            ? CurrentFrameLength
            : _buffer.Count >= LegacyFrameLength && _buffer[28] == 0xA5 && _buffer[29] == 0x5A
                ? LegacyFrameLength
                : 0;
        if (frameLength == 0)
        {
            if (_buffer.Count < LegacyFrameLength) return false;
            _buffer.RemoveAt(0);
            return TryTakeFrame(out frame);
        }

        frame = _buffer.GetRange(0, frameLength).ToArray();
        _buffer.RemoveRange(0, frameLength);
        return true;
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
