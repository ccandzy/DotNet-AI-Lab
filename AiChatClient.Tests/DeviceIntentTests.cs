using AiChatClient.ViewModels;

namespace AiChatClient.Tests;

public sealed class DeviceIntentTests
{
    [Theory]
    [InlineData("大激发开始测量")]
    [InlineData(" 大激发开始测定。 ")]
    public void StartPhrases_AreRecognized(string input)
    {
        Assert.True(MainViewModel.TryGetDeviceIntent(input, out var intent));
        Assert.Equal(DeviceIntent.Start, intent);
    }

    [Theory]
    [InlineData("大激发停止测量")]
    [InlineData("大激发停止测定！")]
    public void StopPhrases_AreRecognized(string input)
    {
        Assert.True(MainViewModel.TryGetDeviceIntent(input, out var intent));
        Assert.Equal(DeviceIntent.Stop, intent);
    }

    [Theory]
    [InlineData("请解释大激发开始测量是什么意思")]
    [InlineData("开始测量")]
    [InlineData("")]
    public void OtherChat_DoesNotTriggerDevice(string input)
    {
        Assert.False(MainViewModel.TryGetDeviceIntent(input, out _));
    }
}
