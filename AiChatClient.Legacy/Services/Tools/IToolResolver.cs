namespace AiChatClient.Services.Tools;

/// <summary>
/// 根据工具名称查找已注册的业务工具。
/// </summary>
public interface IToolResolver
{
    ITool? Resolve(string toolName);
}
