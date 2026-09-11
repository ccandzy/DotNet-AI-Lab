namespace AiChatClient.McpServer.Serial;

internal sealed record MeasurementReading(
    long Sequence,
    DateTimeOffset Timestamp,
    int FlowRaw,
    int MouthPressureRaw);
