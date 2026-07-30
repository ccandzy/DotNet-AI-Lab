###### 引入EF Core框架 Day 1

    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.26" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.26" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="10.0.10">

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
