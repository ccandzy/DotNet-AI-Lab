using AiChatClient.Services.SemanticKernel;

namespace AiChatClient.Tests;

public sealed class ToolIntentDetectorTests
{
    [Theory]
    [InlineData("现在几点？")]
    [InlineData("请告诉我当前本地时间")]
    [InlineData("What time is it?")]
    [InlineData("帮我计算 12 * (3 + 4)")]
    public void RequiresToolCall_RecognizesDeterministicToolRequests(string input)
    {
        Assert.True(ToolIntentDetector.RequiresToolCall(input));
    }

    [Theory]
    [InlineData("你好")]
    [InlineData("介绍一下异步编程")]
    [InlineData("")]
    public void RequiresToolCall_DoesNotForceToolsForOrdinaryQuestions(string input)
    {
        Assert.False(ToolIntentDetector.RequiresToolCall(input));
    }
}
