using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiChatClient.Data;

public class DatabaseInitializer
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;


    public DatabaseInitializer(
        IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }


    public async Task InitializeAsync()
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync();

        // 1. 自动创建/更新数据库
        await context.Database.MigrateAsync();


        // 2. 初始化默认角色
        await SeedRolesAsync(context);
    }


    /// <summary>
    /// 在启动时为空数据库写入默认角色。
    /// </summary>
    private static async Task SeedRolesAsync(AppDbContext context)
    {
        // 已经存在角色，不重复添加
        if (await context.AIRoles.AnyAsync())
        {
            return;
        }


        var roles = new List<AIRoleEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),

                Name = "普通助手",

                Description =
                    "通用AI助手",

                SystemPrompt =
                    "你是一个通用助手，帮助用户回答问题。",

                Model = "",

                Temperature = 0.2,

                IsEnabled = true,

                CreateTime = DateTime.Now
            },


            new()
            {
                Id = Guid.NewGuid(),

                Name = ".NET架构师",

                Description =
                    "负责.NET架构设计和技术方案",

                SystemPrompt =
                    """
                    你是一名资深.NET架构师。
                    回答时关注架构设计、
                    性能、可维护性和最佳实践。
                    """,

                Model = "",

                Temperature = 0.1,

                IsEnabled = true,

                CreateTime = DateTime.Now
            },


            new()
            {
                Id = Guid.NewGuid(),

                Name = "WPF专家",

                Description =
                    "WPF桌面开发专家",

                SystemPrompt =
                    """
                    你是一名WPF专家。
                    擅长MVVM、Prism、
                    性能优化和UI设计。
                    """,

                Model = "",

                Temperature = 0.2,

                IsEnabled = true,

                CreateTime = DateTime.Now
            },


            new()
            {
                Id = Guid.NewGuid(),

                Name = "英语老师",

                Description =
                    "帮助用户学习英语",

                SystemPrompt =
                    """
                    你是一名专业英语老师。
                    使用中文解释复杂语法。
                    主动纠正错误。
                    根据用户水平调整难度。
                    """,

                Model = "",

                Temperature = 0.3,

                IsEnabled = true,

                CreateTime = DateTime.Now
            }
        };


        await context.AIRoles.AddRangeAsync(roles);

        await context.SaveChangesAsync();
    }
}
