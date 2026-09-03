using Models;

namespace AiChatClient.Dtos;

/// <summary>
/// 一条发送给 AI Provider 的内部协议消息。
/// 新版只持久化普通聊天消息；SK 内部管理临时的工具调用消息。
/// </summary>
public sealed class ChatRequestMessage
{
    public ChatRole Role { get; init; }

    public string? Content { get; init; }

}
