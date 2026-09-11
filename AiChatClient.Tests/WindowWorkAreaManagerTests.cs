namespace AiChatClient.Tests;

public sealed class WindowWorkAreaManagerTests
{
    [Theory]
    [InlineData(0, 0, 1920, 1080, 80, 0, 1920, 1080, 80, 0, 1840, 1080)]
    [InlineData(0, 0, 1920, 1080, 0, 0, 1840, 1080, 0, 0, 1840, 1080)]
    [InlineData(0, 0, 1920, 1080, 0, 40, 1920, 1080, 0, 40, 1920, 1040)]
    [InlineData(0, 0, 1920, 1080, 0, 0, 1920, 1040, 0, 0, 1920, 1040)]
    [InlineData(-1920, 0, 0, 1080, -1840, 0, 0, 1080, 80, 0, 1840, 1080)]
    public void CalculateMaximizedBounds_UsesMonitorRelativeWorkArea(
        int monitorLeft, int monitorTop, int monitorRight, int monitorBottom,
        int workLeft, int workTop, int workRight, int workBottom,
        int expectedLeft, int expectedTop, int expectedWidth, int expectedHeight)
    {
        var bounds = WindowWorkAreaManager.CalculateMaximizedBounds(
            new WindowWorkAreaManager.ScreenRect(monitorLeft, monitorTop, monitorRight, monitorBottom),
            new WindowWorkAreaManager.ScreenRect(workLeft, workTop, workRight, workBottom));

        Assert.Equal(expectedLeft, bounds.Left);
        Assert.Equal(expectedTop, bounds.Top);
        Assert.Equal(expectedWidth, bounds.Width);
        Assert.Equal(expectedHeight, bounds.Height);
    }
}
