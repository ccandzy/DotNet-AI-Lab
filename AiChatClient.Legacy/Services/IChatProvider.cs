using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AiChatClient.Dtos;

namespace AiChatClient.Services
{
    public interface IChatProvider
    {
      
        public string ProviderName { get; }
        //public string Model { get; }

        public string ApiChatUrl { get; }

        /// <summary>
        /// 在发送请求前配置 Provider 特有的 HTTP 请求头。
        /// </summary>
        void ConfigureRequest(HttpRequestMessage request);

        /// <summary>
        /// 将统一聊天请求转换为当前 Provider 的 HTTP 请求内容。
        /// </summary>
        HttpContent CreateHttpContent(ChatRequest request);

        ChatChunk? Deserialize(string payload);
    }
}
