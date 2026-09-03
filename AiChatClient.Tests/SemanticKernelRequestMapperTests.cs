using AiChatClient.Dtos;
using AiChatClient.Models;
using AiChatClient.Services.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Models;

namespace AiChatClient.Tests;

public sealed class SemanticKernelRequestMapperTests
{
    [Fact]
    public void CreateChatHistory_PreservesRolesContentAndOrder()
    {
        var messages = new[]
        {
            new ChatRequestMessage { Role = ChatRole.System, Content = "system" },
            new ChatRequestMessage { Role = ChatRole.User, Content = "user" },
            new ChatRequestMessage { Role = ChatRole.Assistant, Content = "assistant" },
            new ChatRequestMessage { Role = ChatRole.Tool, Content = "tool" }
        };

        var history = SemanticKernelRequestMapper.CreateChatHistory(messages);

        Assert.Collection(
            history,
            message =>
            {
                Assert.Equal(AuthorRole.System, message.Role);
                Assert.Equal("system", message.Content);
            },
            message =>
            {
                Assert.Equal(AuthorRole.User, message.Role);
                Assert.Equal("user", message.Content);
            },
            message =>
            {
                Assert.Equal(AuthorRole.Assistant, message.Role);
                Assert.Equal("assistant", message.Content);
            },
            message =>
            {
                Assert.Equal(AuthorRole.Tool, message.Role);
                Assert.Equal("tool", message.Content);
            });
    }

    [Fact]
    public void CreateExecutionSettings_MapsGenerationSettingsAndDisablesDeepSeekThinking()
    {
        var settings = SemanticKernelRequestMapper.CreateExecutionSettings(
            new GenerationSettings
            {
                Temperature = 0.25,
                TopP = 0.75,
                MaxTokens = 321
            },
            "DeepSeek");

        Assert.Equal(0.25, settings.Temperature);
        Assert.Equal(0.75, settings.TopP);
        Assert.Equal(321, settings.MaxTokens);
        Assert.NotNull(settings.FunctionChoiceBehavior);
        Assert.NotNull(settings.ExtensionData);
        Assert.True(settings.ExtensionData.ContainsKey("thinking"));
    }

    [Fact]
    public void CreateExecutionSettings_PreservesProviderDefaults()
    {
        var settings = SemanticKernelRequestMapper.CreateExecutionSettings(
            new GenerationSettings(),
            "DeepSeek");

        Assert.Null(settings.Temperature);
        Assert.Null(settings.TopP);
        Assert.Null(settings.MaxTokens);
    }

    [Fact]
    public void CreateExecutionSettings_DoesNotSendDeepSeekThinkingSettingToOllama()
    {
        var settings = SemanticKernelRequestMapper.CreateExecutionSettings(
            new GenerationSettings
            {
                Temperature = 0.4,
                TopP = 0.8,
                MaxTokens = 2048
            },
            "Ollama");

        Assert.Equal(0.4, settings.Temperature);
        Assert.Equal(0.8, settings.TopP);
        Assert.Equal(2048, settings.MaxTokens);
        Assert.NotNull(settings.FunctionChoiceBehavior);
        Assert.True(
            settings.ExtensionData is null
            || !settings.ExtensionData.ContainsKey("thinking"));
    }
}
