using System.Text.RegularExpressions;

namespace AiChatClient.Services.SemanticKernel;

/// <summary>
/// Detects requests where answering without a native tool would be unreliable.
/// </summary>
internal static partial class ToolIntentDetector
{
    public static bool RequiresToolCall(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return CurrentTimePattern().IsMatch(input)
            || CalculationPattern().IsMatch(input)
            || SerialDeviceOperationPattern().IsMatch(input);
    }

    [GeneratedRegex(
        @"当前(?:的)?(?:本地)?时间|现在(?:是)?几点|现在(?:的)?时间|今天(?:是)?(?:几号|星期几)|current\s+(?:local\s+)?time|what\s+time\s+is\s+it",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentTimePattern();

    [GeneratedRegex(
        @"(?:计算|算一下|帮我算|calculate)\s*[：:]?\s*[-+*/().\d\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CalculationPattern();

    [GeneratedRegex(
        @"大激发.{0,12}(?:开始|启动|停止|结束|打开|关闭|连接|断开|状态)|(?:开始|启动|停止|结束).{0,8}(?:测量|测定)|(?:打开|关闭|连接|断开|查询|查看).{0,12}(?:串口|大激发设备)|(?:列出|查看|查询|刷新|有哪些).{0,12}(?:串口|COM\s*端口)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SerialDeviceOperationPattern();
}
