using System.Threading.Channels;
using AiChatClient.Dtos;
using Microsoft.SemanticKernel;

namespace AiChatClient.Services.SemanticKernel;

internal interface IKernelFactory
{
    Kernel CreateKernel(
        ChatRequest request,
        ChannelWriter<ChatStreamEvent> eventWriter);
}
