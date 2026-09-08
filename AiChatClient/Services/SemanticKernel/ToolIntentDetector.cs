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
            || CalculationPattern().IsMatch(input);
    }

    [GeneratedRegex(
        @"当前(?:的)?(?:本地)?时间|现在(?:是)?几点|现在(?:的)?时间|今天(?:是)?(?:几号|星期几)|current\s+(?:local\s+)?time|what\s+time\s+is\s+it",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CurrentTimePattern();

    [GeneratedRegex(
        @"(?:计算|算一下|帮我算|calculate)\s*[：:]?\s*[-+*/().\d\s]+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CalculationPattern();
}
