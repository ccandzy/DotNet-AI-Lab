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

---

# Sprint 6 Day 4

## 完成功能

- AI Role 数据库读取架构
- Repository 层（`IAIRoleRepository` / `AIRoleRepository`）
- Service 层（`IAIRoleService` / `AIRoleService`）
- DI 生命周期调整（`MainViewModel` / `MainWindow` / `DatabaseInitializer` 从 Singleton 改为 Scoped）
- `MainViewModel` 改为异步初始化（`InitializeAsync`），从数据库加载 AI Role

## 技术实现

### 数据流（Entity → Repository → Service → ViewModel）

```
SQLite (aichat.db)
    ↓ EF Core
AIRoleEntity
    ↓ Mapper (AIRoleMapper.ToModel)
AIRole (Domain Model)
    ↑
AIRoleRepository.GetEnabledRolesAsync()
    ↓ 返回 List<AIRoleEntity>
AIRoleService.GetRolesAsync()
    ↓ 返回 List<AIRole>
MainViewModel.InitializeAsync()
    ↓ 绑定到 Roles 集合
UI (ComboBox / List)
```

### Repository 层

- `IAIRoleRepository`：定义数据访问契约，仅暴露 `GetEnabledRolesAsync()`
- `AIRoleRepository`：通过 `AppDbContext` 查询 `AIRoles` 表，过滤 `IsEnabled = true`
- 依赖 `AppDbContext`（Scoped），不直接返回 Domain Model，保持数据源独立性

### Service 层

- `IAIRoleService`：定义业务逻辑契约，`GetRolesAsync()` 返回 `List<AIRole>`
- `AIRoleService`：调用 Repository 获取 Entity，通过 `AIRoleMapper.ToModel` 转换为 Domain Model
- 位于 ViewModel 和 Repository 之间，承担 Mapper 调用和潜在业务规则逻辑

### DI 生命周期调整

| 服务 | 旧生命周期 | 新生命周期 | 原因 |
|------|-----------|-----------|------|
| `AppDbContext` | Scoped | Scoped | EF Core DbContext 本身就是 Scoped |
| `IAIRoleRepository` | — | Scoped | 依赖 DbContext，必须 Scoped |
| `IAIRoleService` | — | Scoped | 依赖 Repository，继承 Scoped |
| `MainViewModel` | Singleton | **Scoped** | 异步初始化需要 DbContext scope，Scoped 确保生命周期与 Scope 匹配 |
| `MainWindow` | Singleton | **Scoped** | ViewModel 已改为 Scoped，Window 随之统一 |
| `DatabaseInitializer` | Transient | **Scoped** | 需要 DbContext，统一改为 Scoped |

**核心原因**：EF Core `DbContext` 是 Scoped 生命周期，不能注入 Singleton 服务。ViewModel 现在需要异步调用 `IAIRoleService.GetRolesAsync()`（底层访问数据库），因此 ViewModel 必须使用 Scoped 生命周期，在 Scope 内完成初始化。

### App 启动流程（更新后）

```
App 启动
  ↓
加载 appsettings.json
  ↓
创建 DI 容器（注册 DbContext / Repository / Service / ViewModel / Window）
  ↓
创建 Root Scope（_appScope）
  ↓
DatabaseInitializer.InitializeAsync()
  ├─ MigrateAsync() — 应用 Migration
  └─ SeedRolesAsync() — 写入默认 AI Role（若表为空）
  ↓
MainViewModel.InitializeAsync()
  └─ _aIRoleService.GetRolesAsync() → 从 SQLite 读取所有 AI Role
  ↓
MainWindow.Show()
  ↓
OnExit → 释放 _appScope 和 _serviceProvider
```

## 新增文件

| 文件路径 | 作用 |
|----------|------|
| `AiChatClient/Repositories/IAIRoleRepository.cs` | AI Role Repository 接口 |
| `AiChatClient/Repositories/Impl/AIRoleRepository.cs` | AI Role Repository 实现（EF Core 数据访问） |
| `AiChatClient/Services/IAIRoleService.cs` | AI Role Service 接口 |
| `AiChatClient/Services/Impl/AIRoleService.cs` | AI Role Service 实现（调用 Repository + Mapper） |

## 架构调整

### 为什么不让 ViewModel 直接访问 EF Core？

- **职责分离**：ViewModel 属于 UI 层，不应感知数据访问细节
- **可测试性**：Repository / Service 可 Mock，ViewModel 单元测试不依赖数据库
- **可维护性**：查询逻辑集中在 Repository，ViewModel 只关心消费数据
- **解耦**：未来切换数据源（如 API）只需替换 Repository 实现

### 为什么增加 Service 层？

- **业务逻辑边界**：Service 承担 Mapper 转换和潜在的领域规则（如权限、过滤、缓存）
- **接口隔离**：ViewModel 依赖 `IAIRoleService` 而非具体实现，支持单元测试和 AOP
- **分层清晰**：Repository 只管数据读写，Service 管业务编排，ViewModel 只管 UI 状态

## 当前状态

- EF Core + SQLite 持久化基础已完成（Day 1-3）
- AI Role 数据访问层已完成（Repository + Service）
- DI 生命周期调整为 Scoped，启动流程支持异步初始化
- **AI Role 已从硬编码改为数据库读取**
- 等待：Conversation / ChatMessage 的 Repository + Service 接入

## 下一步计划

- 实现 `ConversationRepository` + `ConversationService`
- 实现 `ChatMessageRepository` + `ChatMessageService`（或复用 ConversationService）
- 完善对话列表加载流程（启动时从数据库恢复历史对话）
- 考虑是否引入 Unit of Work 模式（当前 DbContext 已天然支持事务）
- 补充单元测试（Repository / Service Mock）
- 考虑将 Seed 角色数扩展为医疗设备相关角色（呼应项目定位）

---

# Sprint 6 Day 6

## 完成功能

- `ConversationRepository` 完整实现（全量 CRUD + XML 注释）
- `ConversationService` 注入 Repository 并实现 `InitializeAsync`
- `MainViewModel` 启动时调用 `InitializeAsync`，历史对话从 SQLite 恢复
- 删除阻塞编译的旧占位方法，全链路打通

## 技术实现

### ConversationRepository

| 方法 | 逻辑 |
|------|------|
| `GetAllAsync()` | 查询所有 Conversation，Include AIRole + Messages，按 UpdatedTime 降序 |
| `GetByIdAsync(id)` | 同上 Include 策略，用 `FirstOrDefaultAsync` 单条查询 |
| `AddAsync(entity)` | `AddAsync` + `SaveChangesAsync`，Guid 由调用方预生成 |
| `UpdateAsync(entity)` | 显式设 `EntityState.Modified` + `SaveChangesAsync`，脱离追踪上下文也能更新 |
| `DeleteAsync(id)` | 先查后删，找不到抛 `KeyNotFoundException`，不静默吞错 |

- 全方法添加 XML `<summary>` / `<param>` / `<returns>` / `<exception>` 注释
- `ConversationEntity.Id` 为 `ValueGeneratedNever`，Guid 由服务层在 `CreateConversation()` 中生成

### ConversationService

- 通过构造函数注入 `IConversationRepository`
- `InitializeAsync()`：调用 `_conversationRepository.GetAllAsync()`，用 `ConversationMapper.ToModel` 逐个转 Domain Model，`Clear()` 后再 `Add` 到 `ObservableCollection`
- `CreateConversation` / `DeleteConversation` / `RenameConversation` 保持原有业务逻辑

### MainViewModel

- `InitializeAsync()` 末尾追加 `await _conversationService.InitializeAsync()`
- 启动顺序：AI Role 先加载 → 对话再加载，确保对话的 AIRole 导航属性完整

### 数据流（新增对话持久化）

```
App 启动
  ↓
MainViewModel.InitializeAsync()
  ├─ _aIRoleService.GetRolesAsync()        → 加载 AI Role（Day 4）
  └─ _conversationService.InitializeAsync() → 加载历史对话（Day 6）
       ↓
ConversationRepository.GetAllAsync()
       ↓ EF Core（Include AIRole + Messages）
ConversationEntity[]
       ↓ ConversationMapper.ToModel
Conversation[]
       ↓
ObservableCollection<Conversation>（UI 绑定）
```

## 新增文件

| 文件路径 | 作用 |
|----------|------|
| `AiChatClient/Repositories/IConversationRepository.cs` | 对话 Repository 接口 |
| `AiChatClient/Repositories/Impl/ConversationRepository.cs` | 对话 Repository 实现（EF Core 全量 CRUD） |

## 修改文件

| 文件路径 | 变更内容 |
|----------|----------|
| `AiChatClient/Services/IConversationService.cs` | 新增 `Task InitializeAsync()` 方法签名 |
| `AiChatClient/Services/Impl/ConversationService.cs` | 注入 `IConversationRepository`；实现 `InitializeAsync` 从 DB 恢复历史对话 |
| `AiChatClient/ViewModels/MainViewModel.cs` | `InitializeAsync` 末尾追加 `await _conversationService.InitializeAsync()` |
| `docs/Sprint 6.md` | 追加本日开发记录 |

## 当前状态

- EF Core + SQLite 持久化基础已完成（Day 1-3）
- AI Role 数据访问层已完成（Day 4）
- **Conversation 数据访问层已完成**（Day 6）：Repository → Service → ViewModel 全链路打通
- 对话列表启动时自动从 SQLite 恢复，AI Role 从数据库读取，不再硬编码
- 待完成：ChatMessage 的 CRUD、新建/删除对话的数据库持久化（当前只在内存 ObservableCollection 中操作）

## 下一步计划

- 实现 `ChatMessageRepository` / `ChatMessageService`，完成消息的数据库读写
- `ConversationService.CreateConversation` / `DeleteConversation` 写入 SQLite（当前仅内存操作）
- 完善 Migration：ChatMessage 完整 CRUD 持久化
- 补充单元测试（Repository / Service Mock）
- 后续可考虑扩展 Seed 角色为医疗设备相关角色（呼应项目定位）
