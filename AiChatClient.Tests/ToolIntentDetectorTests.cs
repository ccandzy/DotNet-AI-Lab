using AiChatClient.Services.SemanticKernel;

namespace AiChatClient.Tests;

public sealed class ToolIntentDetectorTests
{
    [Theory]
    [InlineData("现在几点？")]
    [InlineData("请告诉我当前本地时间")]
    [InlineData("What time is it?")]
    [InlineData("帮我计算 12 * (3 + 4)")]
    [InlineData("大激发停止测量")]
    [InlineData("请停止当前测量")]
    [InlineData("请查询大激发设备状态")]
    [InlineData("请打开设备串口")]
    [InlineData("有哪些可用串口")]
    public void RequiresToolCall_RecognizesDeterministicToolRequests(string input)
    {
        Assert.True(ToolIntentDetector.RequiresToolCall(input));
    }

    [Theory]
    [InlineData("你好")]
    [InlineData("介绍一下异步编程")]
    [InlineData("介绍一下测量原理")]
    [InlineData("")]
    public void RequiresToolCall_DoesNotForceToolsForOrdinaryQuestions(string input)
    {
        Assert.False(ToolIntentDetector.RequiresToolCall(input));
    }
}
