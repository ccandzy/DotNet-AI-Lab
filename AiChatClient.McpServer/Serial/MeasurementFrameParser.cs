namespace AiChatClient.McpServer.Serial;

internal sealed class MeasurementFrameParser
{
    internal const int FrameLength = 30;
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
            if (!TryReadSignedBigEndian(frame[14], frame[15], frame[16], out var flow)
                || !TryReadSignedBigEndian(frame[17], frame[18], frame[19], out var pressure))
            {
                continue;
            }

            results.Add(new MeasurementReading(
                Interlocked.Increment(ref _sequence),
                DateTimeOffset.Now,
                flow,
                pressure));
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

        if (_buffer.Count < FrameLength)
        {
            return false;
        }

        if (_buffer[28] != 0xA5 || _buffer[29] != 0x5A)
        {
            _buffer.RemoveAt(0);
            return TryTakeFrame(out frame);
        }

        frame = _buffer.GetRange(0, FrameLength).ToArray();
        _buffer.RemoveRange(0, FrameLength);
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
