# Sprint 6 扩展记录

## DbContext 生命周期重构

### 背景

WPF 应用启动后只创建了一个 DI Scope。`AddDbContext<AppDbContext>` 注册的上下文因此会在整个应用运行期被多个 Repository 共享，存在并发访问、实体长期跟踪和意外保存的风险。

### 改造内容

- DI 注册由 `AddDbContext<AppDbContext>` 改为 `AddDbContextFactory<AppDbContext>`。
- `AIRoleRepository`、`ConversationRepository`、`ChatMessageRepository` 及 `DatabaseInitializer` 改为注入 `IDbContextFactory<AppDbContext>`。
- 每个数据访问方法使用 `await using` 创建并释放短生命周期 Context。
- 只读查询使用 `AsNoTracking()`，避免 EF Core 长期追踪 UI 不需要的实体。
- 会话和消息的批量删除使用 `ExecuteDeleteAsync()` 在数据库端执行，不再将待删除记录加载到内存。
- 角色保存锁重命名为 `_rolePersistenceLock`；它只保证用户快速连续选择角色时的保存顺序，不再承担共享 Context 的线程安全职责。

### 生命周期

```text
一次 Repository 操作
  -> IDbContextFactory.CreateDbContextAsync()
  -> 查询或写入 SQLite
  -> Context 自动释放
```

## 重命名持久化修复

### 问题

旧逻辑先将内存会话的旧标题映射到 Entity 并保存，随后才更新内存标题。因此 UI 会显示新标题，但 SQLite 保存的仍是旧标题，重启后名称回退。

### 修复

- 先把新标题和更新时间写入数据库 Entity。
- 数据库保存成功后，再更新 `Conversation` 内存模型和 UI。
- 数据库保存失败时，UI 不会错误显示未持久化的新名称。

## Repository 写入流程审查

| Repository | 结论 |
|------------|------|
| `ConversationRepository` | 已修复重命名保存顺序；角色更新先更新 Entity 再保存，顺序正确。 |
| `ChatMessageRepository` | 仅包含新增、查询、按会话删除；消息内容在映射前已确定，不存在先保存旧值的问题。 |
| `AIRoleRepository` | 当前只提供角色查询，不包含写入流程。 |

## 验证

- 执行 `dotnet build AiChatClient.sln --no-restore`。
- 构建成功：0 个错误；保留项目既有警告。
