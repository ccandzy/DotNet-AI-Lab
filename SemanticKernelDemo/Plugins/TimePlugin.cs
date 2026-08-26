using System.ComponentModel;
using System.Globalization;
using Microsoft.SemanticKernel;

namespace SemanticKernelDemo.Plugins;

/// <summary>
/// 一个普通的 C# 类。注册到 Kernel 后，它会成为一个 Plugin。
/// </summary>
public sealed class TimePlugin
{
    // KernelFunction 将此方法暴露为模型可选择调用的 Function；名称会发送给模型。
    [KernelFunction("get_current_time")]
    [Description("获取当前时间。未指定时区时，返回运行此程序的电脑的本地时间。结果为包含 UTC 偏移量的 ISO 8601 格式。")]
    public string GetCurrentTime(
        [Description("可选的 Windows 时区 ID。为空时使用本机本地时区，例如 China Standard Time。")] string? timeZoneId = null)
    {
        // 没有参数时使用运行程序的本机时区；参数存在时由 Windows 时区 ID 转换。
        var now = string.IsNullOrWhiteSpace(timeZoneId)
            ? DateTimeOffset.Now
            : TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

        // "O" 是 round-trip ISO 8601 格式，会保留 UTC 偏移量。
        return now.ToString("O", CultureInfo.InvariantCulture);
    }
}
