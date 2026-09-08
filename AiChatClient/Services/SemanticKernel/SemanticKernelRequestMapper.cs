using AiChatClient.Config;
using AiChatClient.Dtos;
using AiChatClient.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Models;

namespace AiChatClient.Services.SemanticKernel;

internal static class SemanticKernelRequestMapper
{
    public static ChatHistory CreateChatHistory(
        IEnumerable<ChatRequestMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var history = new ChatHistory();

        foreach (var message in messages)
        {
            var content = message.Content ?? string.Empty;

            switch (message.Role)
            {
                case ChatRole.System:
                    history.AddSystemMessage(content);
                    break;
                case ChatRole.User:
                    history.AddUserMessage(content);
                    break;
                case ChatRole.Assistant:
                    history.AddAssistantMessage(content);
                    break;
                case ChatRole.Tool:
                    history.AddMessage(AuthorRole.Tool, content);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(message.Role),
                        message.Role,
                        "不支持的聊天角色。");
            }
        }

        return history;
    }

    public static OpenAIPromptExecutionSettings CreateExecutionSettings(
        GenerationSettings settings,
        string providerName,
        bool enableTools = false,
        bool requireToolCall = false)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxTokens = settings.MaxTokens
        };

        if (enableTools)
        {
            var options = new FunctionChoiceBehaviorOptions
            {
                AllowParallelCalls = false,
                AllowConcurrentInvocation = false
            };
            executionSettings.FunctionChoiceBehavior = requireToolCall
                ? FunctionChoiceBehavior.Required(
                    autoInvoke: true,
                    options: options)
                : FunctionChoiceBehavior.Auto(
                    autoInvoke: true,
                    options: options);
        }

        if (string.Equals(
                providerName,
                AIProviderNames.DeepSeek,
                StringComparison.OrdinalIgnoreCase))
        {
            executionSettings.ExtensionData = new Dictionary<string, object>
            {
                ["thinking"] = new { type = "disabled" }
            };
        }

        return executionSettings;
    }
}
