using System.ComponentModel;
using System.Globalization;
using Microsoft.SemanticKernel;

namespace AiChatClient.Plugins;

public sealed class TimePlugin
{
    [KernelFunction("current_time")]
    [Description("获取当前本地时间，结果使用 ISO 8601 格式并包含时区偏移。")]
    public string GetCurrentTime(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return DateTimeOffset.Now.ToString(
            "O",
            CultureInfo.InvariantCulture);
    }
}
