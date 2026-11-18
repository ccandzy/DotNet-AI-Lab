namespace AiChatClient.Services.Mcp;

/// <summary>
/// resources/list 返回的资源目录信息；正文通过 resources/read 单独读取。
/// </summary>
public sealed record McpResourceInfo(
    string Uri,
    string Name,
    string? MimeType);
