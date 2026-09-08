using AiChatClient.Config;

namespace AiChatClient.Tests;

public sealed class AIConfigurationSelectionResolverTests
{
    [Fact]
    public void Resolve_SelectsConversationModelWhenProviderDidNotChange()
    {
        var thinking = new AIModelOptions
        {
            Name = "Thinking",
            ModelId = "qwen3:4b",
            IsEnabled = true
        };
        var instruct = new AIModelOptions
        {
            Name = "Instruct",
            ModelId = "qwen3:4b-instruct",
            IsEnabled = true
        };
        var provider = new AIProviderOptions
        {
            Name = "Ollama",
            Models = [thinking, instruct]
        };

        var selection = AIConfigurationSelectionResolver.Resolve(
            [provider],
            currentProvider: provider,
            conversationModelId: thinking.ModelId);

        Assert.Same(provider, selection.Provider);
        Assert.Same(thinking, selection.Model);
        Assert.Equal([thinking, instruct], selection.Models);
    }

    [Fact]
    public void Resolve_FallsBackToVisibleEnabledModelForUnknownSavedModel()
    {
        var instruct = new AIModelOptions
        {
            Name = "Instruct",
            ModelId = "qwen3:4b-instruct",
            IsEnabled = true
        };
        var provider = new AIProviderOptions
        {
            Name = "Ollama",
            Models = [instruct]
        };

        var selection = AIConfigurationSelectionResolver.Resolve(
            [provider],
            currentProvider: provider,
            conversationModelId: "removed-model");

        Assert.Same(instruct, selection.Model);
    }
}
