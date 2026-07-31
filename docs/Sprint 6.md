###### 引入EF Core框架 Day 1

```xml-doc
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.26" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.26" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.10">
```

新建Entities、Mappers、AppDbContext

软件结构：

AiChatClient

├── Models
│   └── 现有业务/UI模型
│
├── Entities
│   ├── AIRoleEntity.cs
│   ├── ConversationEntity.cs
│   └── ChatMessageEntity.cs
│
├── Data
│   └── AppDbContext.cs
│
├── Mappers
│   ├── AIRoleMapper.cs
│   ├── ConversationMapper.cs
│   └── ChatMessageMapper.cs
│
└── Repositories

---

# Sprint 6 Day 3

## 完成功能

- EF Core + SQLite 持久化层搭建完成
- 数据库 Migration 创建并通过（`InitialCreate`）
- SQLite 本地数据库 `aichat.db` 自动创建
- `DatabaseInitializer` 负责启动时执行 `MigrateAsync()` + AI Role Seed 数据初始化
- `appsettings.json` 新增 `ConnectionStrings:DefaultConnection` 配置项
- `App.xaml.cs` 注册 `AppDbContext`、`DatabaseInitializer`，启动时调用 `InitializeAsync()`
- `*.db` 加入 `.gitignore`，防止 SQLite 数据库文件被误提交

## 技术实现

### EF Core 数据模型

| 实体 | 表名 | 说明 |
|------|------|------|
| `AIRoleEntity` | `AIRoles` | AI 角色，含 SystemPrompt / Temperature / IsEnabled 等 |
| `ConversationEntity` | `Conversations` | 对话，外键关联 AIRole，级联删除 |
| `ChatMessageEntity` | `ChatMessages` | 消息，外键关联 Conversation，按 ConversationId + Timestamp 建联合索引 |

三个实体均在 `AppDbContext.OnModelCreating` 中使用 Fluent API 配置，包括主键、字段约束、外键关系和索引。

### 设计时工厂 & 迁移支持

- `AppContextFactory` 实现 `IDesignTimeDbContextFactory<AppDbContext>`，使 EF Tools（`dotnet ef`）可在无运行时上下文时创建 DbContext。
- Migration `InitialCreate`（`20260731083832_InitialCreate`）由 `dotnet ef migrations add` 生成，包含三张表的建表和索引 DDL，以及级联删除外键约束。

### 启动初始化流程

```csharp
OnStartup()
  → BuildConfiguration()          // 读取 appsettings.json
  → ConfigureServices()           // 注册 DbContext + DatabaseInitializer
  → BuildServiceProvider()
  → CreateScope()
  → DatabaseInitializer.InitializeAsync()
        ├─ _context.Database.MigrateAsync()  // 应用迁移，自动建库
        └─ SeedRolesAsync()                   // 无数据时写入 4 个默认 AI Role
  → Show MainWindow
```

### 默认 AI Role Seed

启动时检测 `AIRoles` 表为空，自动写入 4 个角色：

| 角色名 | Temperature | SystemPrompt 核心内容 |
|--------|-------------|----------------------|
| 普通助手 | 0.2 | 通用助手，帮助用户回答问题 |
| .NET架构师 | 0.1 | 关注架构设计、性能、可维护性和最佳实践 |
| WPF专家 | 0.2 | 擅长 MVVM / Prism / 性能优化 / UI 设计 |
| 英语老师 | 0.3 | 中文解释语法，主动纠错，根据水平调整难度 |

## 新增文件

| 文件路径 | 作用 |
|----------|------|
| `AiChatClient/Entities/AIRoleEntity.cs` | AI 角色实体 |
| `AiChatClient/Entities/ConversationEntity.cs` | 对话实体 |
| `AiChatClient/Entities/ChatMessageEntity.cs` | 聊天消息实体 |
| `AiChatClient/Data/AppDbContext.cs` | EF Core 数据库上下文 |
| `AiChatClient/Data/AppContextFactory.cs` | 设计时工厂（供 EF Tools 使用） |
| `AiChatClient/Data/DatabaseInitializer.cs` | 启动时迁移 + Seed 初始化 |
| `AiChatClient/Migrations/20260731083832_InitialCreate.cs` | 初始 Migration |
| `AiChatClient/Migrations/20260731083832_InitialCreate.Designer.cs` | Migration 设计器 |
| `AiChatClient/Migrations/AppDbContextModelSnapshot.cs` | 模型快照 |
| `.gitignore` | 新增 `*.db` 规则 |

## 修改文件

| 文件路径 | 变更内容 |
|----------|----------|
| `AiChatClient/App.xaml.cs` | 注册 AppDbContext、DatabaseInitializer；OnStartup 改为 async；启动流程加入初始化调用 |
| `AiChatClient/appsettings.json` | 新增 `ConnectionStrings.DefaultConnection = Data Source=aichat.db` |
| `docs/Sprint 6.md` | 追加本日开发记录 |

## 当前状态

- EF Core 持久化层基础已就绪
- 数据库文件 `aichat.db` 已在本机创建，结构包含三张表
- 启动自动迁移和 Seed 已验证通过
- 等待后续：Repository 层、Service 层接入持久化

## 下一步计划

- 实现 Repository 层（`AIRoleRepository` / `ConversationRepository`）
- 实现 `ConversationService` 与 `DatabaseInitializer` 的持久化集成
- 完成 Mapper 层（Entity ↔ Domain Model 转换）
- 考虑是否需要 Unit of Work 模式
