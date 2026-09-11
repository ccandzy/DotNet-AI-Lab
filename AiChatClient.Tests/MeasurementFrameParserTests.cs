using AiChatClient.McpServer.Serial;

namespace AiChatClient.Tests;

public sealed class MeasurementFrameParserTests
{
    private static readonly byte[] SampleFrame =
    [
        0x5A, 0xA5, 0x0F, 0xA0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x01, 0x1A, 0x0E, 0x01, 0x2F, 0xE6, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xA5, 0x5A
    ];
    private static readonly byte[] CurrentSampleFrame =
    [
        0x5A, 0xA5, 0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01,
        0x2F, 0xE6, 0x01, 0x00, 0x00, 0x10, 0x62, 0x3C, 0x00, 0x11,
        0xA5, 0x5A
    ];

    [Fact]
    public void SampleFrame_ReadsOnlyFlowAndMouthPressure()
    {
        var reading = Assert.Single(new MeasurementFrameParser().Append(SampleFrame));
        Assert.Equal(-12262, reading.FlowRaw);
        Assert.Equal(0, reading.MouthPressureRaw);
    }

    [Fact]
    public void SplitFrame_IsBufferedUntilComplete()
    {
        var parser = new MeasurementFrameParser();
        Assert.Empty(parser.Append(SampleFrame.AsSpan(0, 9)));
        var reading = Assert.Single(parser.Append(SampleFrame.AsSpan(9)));
        Assert.Equal(-12262, reading.FlowRaw);
    }

    [Fact]
    public void Current22ByteFrame_ParsesFlowWithoutInventingPressure()
    {
        var reading = Assert.Single(new MeasurementFrameParser().Append(CurrentSampleFrame));
        Assert.Equal(-12262, reading.FlowRaw);
        Assert.Null(reading.MouthPressureRaw);
    }

    [Fact]
    public void UnsupportedCompleteFrame_IsStillExposedForDiagnostics()
    {
        var parser = new MeasurementFrameParser();
        ReadOnlyMemory<byte> receivedFrame = default;
        parser.FrameReceived += frame => receivedFrame = frame;
        var unsupported = new byte[] { 0x5A, 0xA5, 0x40, 0x00, 0x11, 0x22, 0xA5, 0x5A };

        Assert.Empty(parser.Append(unsupported));
        Assert.Equal(unsupported, receivedFrame.ToArray());
    }

    [Fact]
    public void NoiseAndJoinedFrames_AreResynchronized()
    {
        var parser = new MeasurementFrameParser();
        var bytes = new byte[] { 0x00, 0x5A, 0x00, 0xFF }
            .Concat(SampleFrame).Concat(SampleFrame).ToArray();
        var readings = parser.Append(bytes);
        Assert.Equal(2, readings.Count);
        Assert.Equal(new long[] { 1, 2 }, readings.Select(item => item.Sequence));
    }

    [Fact]
    public void BadTailAndInvalidSign_AreSkippedAndNextFrameSurvives()
    {
        var badTail = SampleFrame.ToArray();
        badTail[29] = 0x00;
        var badSign = SampleFrame.ToArray();
        badSign[14] = 0x7F;
        var parser = new MeasurementFrameParser();
        var readings = parser.Append(badTail.Concat(badSign).Concat(SampleFrame).ToArray());
        Assert.Single(readings);
        Assert.Equal(-12262, readings[0].FlowRaw);
    }

    [Fact]
    public void DeviceCommands_MatchProtocolExactly()
    {
        Assert.Equal("4000030060\r\n"u8.ToArray(),
            DeviceSerialService.BuildStartProtocolCommand(3, 60));
        Assert.Equal(new byte[] { 0x34, 0x31, 0x30, 0x30, 0x0D, 0x0A },
            DeviceSerialService.StopProtocolCommand.ToArray());
    }


    [Theory]
    [InlineData(0, 60)]
    [InlineData(51, 60)]
    [InlineData(3, 0)]
    [InlineData(3, 10000)]
    public void StartCommand_RejectsOutOfRangeParameters(int frequency, int duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DeviceSerialService.BuildStartProtocolCommand(frequency, duration));
    }
}
