using System;
using System.Collections.Generic;
using AiChatClient.Models;

namespace AiChatClient.Dtos
{
    /// <summary>
    /// 表示一次统一的 AI 聊天请求。
    /// 用于在应用内部传递消息列表、目标模型和生成参数，
    /// 由聊天服务转换为 Semantic Kernel 请求。
    /// </summary>
    public class ChatRequest
    {
        /// <summary>
        /// 本次请求包含的聊天消息。
        /// </summary>
        public IReadOnlyList<ChatRequestMessage> Messages { get; init; } = Array.Empty<ChatRequestMessage>();
        public string Provider { get; init; } = string.Empty;
        /// <summary>
        /// 本次请求使用的模型标识。
        /// </summary>
        public string Model { get; init; } = string.Empty;

        /// <summary>
        /// 本次请求的可选生成参数。
        /// </summary>
        public GenerationSettings Settings { get; init; } = new();

    }
}
