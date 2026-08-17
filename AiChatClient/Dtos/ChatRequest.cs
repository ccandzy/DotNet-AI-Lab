using System;
using System.Collections.Generic;
using AiChatClient.Models;

namespace AiChatClient.Dtos
{
    /// <summary>
    /// 表示一次统一的 AI 聊天请求。
    /// 用于在应用内部传递消息列表、目标模型和生成参数，
    /// 由具体的 <see cref="IChatProvider"/> 转换为对应 Provider 的请求格式。
    /// </summary>
    public class ChatRequest
    {
        /// <summary>
        /// 本次请求包含的聊天消息。
        /// </summary>
        public IReadOnlyList<ChatMessage> Messages { get; init; } = Array.Empty<ChatMessage>();
        public string Provider { get; init; } = string.Empty;
        /// <summary>
        /// 本次请求使用的模型标识。
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// 本次请求使用的温度参数。
        /// </summary>
        public double Temperature { get; init; }
    }
}
