# DotNet AI Lab

Learning AI Application Development with .NET by building a production-quality AI desktop client.

## Projects

- `AiChatClient`: the current WPF client. DeepSeek and Ollama chat, streaming, and automatic function calling are orchestrated by Semantic Kernel.
- `AiChatClient.Legacy`: the preserved hand-written Provider/SSE/tool-calling implementation. Its internal `AiChatClient` namespaces intentionally remain unchanged.
- `SemanticKernelDemo`: the minimal offline and DeepSeek learning sample.
- `AiChatClient.Tests`: offline tests for plugins, SK request mapping, streaming events, cancellation, configuration validation, and database import.

Both desktop clients resolve SQLite to `%LocalAppData%\AiChatClient\aichat.db`. On first use, an existing relative `aichat.db` is copied to that location without overwriting either file. Do not run the Legacy and Semantic Kernel clients at the same time.

The current Semantic Kernel client supports DeepSeek and Ollama. Ollama is connected through its OpenAI-compatible `/v1/chat/completions` endpoint, so the application does not depend on the prerelease Semantic Kernel Ollama connector.

### Local configuration

Keep real API keys and machine-specific URLs in each project's ignored `appsettings.Local.json`; never commit that file. A remote Ollama provider can be configured as follows:

```json
{
  "Name": "Ollama",
  "BaseUrl": "http://192.168.137.2:11434/v1",
  "ApiKey": "ollama",
  "Models": [
    {
      "Name": "Qwen3 4B Thinking",
      "ModelId": "qwen3:4b",
      "IsEnabled": true
    }
  ]
}
```

The `ollama` API key is a non-secret placeholder required by the OpenAI client and ignored by Ollama. Ensure Ollama listens on the LAN interface and that Windows Firewall allows inbound TCP traffic on port `11434`. The base URL must include `/v1`.

The current `qwen3:4b` digest (`359d7dd4bcda`) is the thinking variant. It may spend many output tokens reasoning before producing final content, so leave MaxTokens on the provider default or choose a sufficiently large value. The client displays the final answer and keeps the existing busy/cancel state while the model is thinking. `qwen3-embedding:0.6b` is reserved for a future RAG integration and must not be listed as a chat model.

## Roadmap
1
- [x] WPF Project Setup
- [x] Dependency Injection
- [x] HttpClient
- [x] Ollama Integration
- [x] MVVM
- [x] Streaming
- [x] Conversation History
- [x] Conversation Management

2、
- [x] Configuration Management
- [ ] Multi-Model Support
- [x] Persistence(SQLite)
- [x] Markdown Rendering
- [ ] Logging & Diagnostics

3、
- [x] Semantic Kernel
- [x] Function Calling
- [ ] RAG
- [ ] MCP
- [ ] AI Agent

4、
- [ ] Plugin Architecture
- [ ] Unit Test
- [ ] Performance Optimization
- [ ] GitHub Release
- [ ] Documentation

## Tech Stack

- .NET 9
- WPF
- CommunityToolkit.Mvvm
- Ollama
- C#

## Project Structure

...
