using System.Globalization;
using AiChatClient.Plugins;

namespace AiChatClient.Tests;

public sealed class PluginTests
{
    [Fact]
    public void Calculator_UsesExistingArithmeticBehavior()
    {
        var plugin = new CalculatorPlugin();

        var result = plugin.Calculate("1 + 2 * 3");

        Assert.Equal("7", result);
    }

    [Fact]
    public void Calculator_ReturnsFriendlyErrorForMissingExpression()
    {
        var plugin = new CalculatorPlugin();

        var result = plugin.Calculate("  ");

        Assert.Equal("缺少有效的 expression 参数。", result);
    }

    [Fact]
    public void Calculator_HonorsCancellation()
    {
        var plugin = new CalculatorPlugin();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => plugin.Calculate("1 + 1", cancellation.Token));
    }

    [Fact]
    public void Time_ReturnsRoundTripTimestampWithOffset()
    {
        var plugin = new TimePlugin();

        var result = plugin.GetCurrentTime();

        Assert.True(
            DateTimeOffset.TryParseExact(
                result,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _));
    }

    [Fact]
    public void Time_HonorsCancellation()
    {
        var plugin = new TimePlugin();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => plugin.GetCurrentTime(cancellation.Token));
    }
}
