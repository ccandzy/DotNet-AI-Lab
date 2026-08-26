namespace AiChatClient.Services.Tools.Impl;

/// <summary>
/// 基于 DI 注册工具集合的默认解析器。
/// </summary>
public sealed class ToolResolver : IToolResolver
{
    private readonly IEnumerable<ITool> _tools;

    public ToolResolver(IEnumerable<ITool> tools)
    {
        _tools = tools;
    }

    public ITool? Resolve(string toolName)
    {
        return _tools.FirstOrDefault(tool =>
            string.Equals(tool.Definition.Name, toolName, StringComparison.OrdinalIgnoreCase));
    }
}
