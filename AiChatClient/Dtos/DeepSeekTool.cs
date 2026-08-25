using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiChatClient.Dtos;

/// <summary>
/// DeepSeek Chat Completions API 中的一个工具定义。
/// </summary>
public sealed class DeepSeekTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public DeepSeekFunctionDefinition Function { get; set; } = new();
}

/// <summary>
/// DeepSeek API 中 function 类型工具的定义。
/// </summary>
public sealed class DeepSeekFunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DeepSeekFunctionParameters? Parameters { get; set; }
}

/// <summary>
/// DeepSeek API 所需的 function 参数 JSON Schema。
/// </summary>
public sealed class DeepSeekFunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, DeepSeekFunctionParameter> Properties { get; } = new();

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

/// <summary>
/// DeepSeek function JSON Schema 中的单个参数。
/// </summary>
public sealed class DeepSeekFunctionParameter
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// DeepSeek API 中 assistant 发起的一次完整工具调用。
/// </summary>
public sealed class DeepSeekToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public DeepSeekFunctionCall Function { get; set; } = new();
}

/// <summary>
/// DeepSeek API 中工具调用的函数名称与参数。
/// </summary>
public sealed class DeepSeekFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "{}";
}

/// <summary>
/// DeepSeek 流式响应中的工具调用片段。
/// </summary>
public sealed class DeepSeekToolCallDelta
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("function")]
    public DeepSeekFunctionCallDelta? Function { get; set; }
}

/// <summary>
/// DeepSeek 流式响应中函数调用的增量字段。
/// </summary>
public sealed class DeepSeekFunctionCallDelta
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

/// <summary>
/// DeepSeek thinking 模式开关。
/// </summary>
public sealed class DeepSeekThinking
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "disabled";
}
