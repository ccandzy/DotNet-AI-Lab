namespace AiChatClient.Config;

/// <summary>
/// Resolves the visible provider/model pair for a conversation without relying on stale UI state.
/// </summary>
internal static class AIConfigurationSelectionResolver
{
    public static AIConfigurationSelection Resolve(
        IReadOnlyList<AIProviderOptions> providers,
        AIProviderOptions? currentProvider,
        string? conversationModelId)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var savedModelProvider = string.IsNullOrWhiteSpace(conversationModelId)
            ? null
            : providers.FirstOrDefault(candidate => candidate.Models.Any(model =>
                model.IsEnabled
                && string.Equals(
                    model.ModelId,
                    conversationModelId,
                    StringComparison.OrdinalIgnoreCase)));

        var provider = savedModelProvider
            ?? providers.FirstOrDefault(candidate => ReferenceEquals(
                candidate,
                currentProvider))
            ?? providers.FirstOrDefault();
        var models = provider?.Models
            .Where(model => model.IsEnabled)
            .ToArray()
            ?? Array.Empty<AIModelOptions>();
        var model = string.IsNullOrWhiteSpace(conversationModelId)
            ? null
            : models.FirstOrDefault(candidate => string.Equals(
                candidate.ModelId,
                conversationModelId,
                StringComparison.OrdinalIgnoreCase));
        model ??= models.FirstOrDefault();

        return new AIConfigurationSelection(provider, models, model);
    }
}

internal sealed record AIConfigurationSelection(
    AIProviderOptions? Provider,
    IReadOnlyList<AIModelOptions> Models,
    AIModelOptions? Model);
