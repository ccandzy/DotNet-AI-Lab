using System.ClientModel;
using AiChatClient.Config;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;

namespace AiChatClient.Services.Rag;

/// <summary>
/// Generates vectors through the same Semantic Kernel/OpenAI-compatible path as SemanticKernelDemo.
/// </summary>
public sealed class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingOptions _options;
    private readonly Lazy<IEmbeddingGenerator<string, Embedding<float>>> _generator;

    public SemanticKernelEmbeddingService(IOptions<EmbeddingOptions> options)
    {
        _options = options.Value;
        _generator = new Lazy<IEmbeddingGenerator<string, Embedding<float>>>(
            CreateGenerator,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (inputs.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        if (inputs.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Embedding 输入不能包含空文本。", nameof(inputs));
        }

        GeneratedEmbeddings<Embedding<float>> embeddings;
        try
        {
            embeddings = await _generator.Value.GenerateAsync(
                inputs,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"无法调用 Embedding 服务。Endpoint={_options.Endpoint}，ModelId={_options.ModelId}。{exception.Message}",
                exception);
        }

        if (embeddings.Count != inputs.Count)
        {
            throw new InvalidOperationException(
                $"Embedding 服务应返回 {inputs.Count} 个向量，实际返回 {embeddings.Count} 个。");
        }

        return embeddings
            .Select(embedding => (ReadOnlyMemory<float>)embedding.Vector.ToArray())
            .ToArray();
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateGenerator()
    {
        if (!Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(
                $"Embedding:Endpoint 不是有效的绝对地址：{_options.Endpoint}");
        }

        if (string.IsNullOrWhiteSpace(_options.ModelId))
        {
            throw new InvalidOperationException("Embedding:ModelId 不能为空。");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException(
                "Embedding:ApiKey 不能为空；Ollama 可使用占位值 ollama。");
        }

        var openAIClient = new OpenAIClient(
            new ApiKeyCredential(_options.ApiKey),
            new OpenAIClientOptions { Endpoint = endpoint });
        var builder = Kernel.CreateBuilder();

#pragma warning disable SKEXP0010
        builder.AddOpenAIEmbeddingGenerator(
            modelId: _options.ModelId,
            openAIClient: openAIClient);
#pragma warning restore SKEXP0010

        return builder.Build()
            .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    }
}
