# DotNet AI Lab

Learning AI Application Development with .NET by building a production-quality AI desktop client.

## Projects

- `AiChatClient`: the current WPF client. DeepSeek chat, streaming, and automatic function calling are orchestrated by Semantic Kernel.
- `AiChatClient.Legacy`: the preserved hand-written Provider/SSE/tool-calling implementation. Its internal `AiChatClient` namespaces intentionally remain unchanged.
- `SemanticKernelDemo`: the minimal offline and DeepSeek learning sample.
- `AiChatClient.Tests`: offline tests for plugins, SK request mapping, streaming events, cancellation, configuration validation, and database import.

Both desktop clients resolve SQLite to `%LocalAppData%\AiChatClient\aichat.db`. On first use, an existing relative `aichat.db` is copied to that location without overwriting either file. Do not run the Legacy and Semantic Kernel clients at the same time.

The current Semantic Kernel client supports DeepSeek only. Ollama remains available in `AiChatClient.Legacy` until its prerelease SK connector passes the required streaming and function-calling compatibility checks.

### Local configuration

Keep real API keys in each project's ignored `appsettings.Local.json`; never commit that file. The SK client filters its UI to the DeepSeek provider even when the local override still contains legacy Ollama configuration.

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
