using AiChatClient.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiChatClient.Data;

public class DatabaseInitializer
{
    private readonly AppDbContext _context;


    public DatabaseInitializer(
        AppDbContext context)
    {
        _context = context;
    }


    public async Task InitializeAsync()
    {
        // 1. 自动创建/更新数据库
        await _context.Database.MigrateAsync();


        // 2. 初始化默认角色
        await SeedRolesAsync();
    }


    private async Task SeedRolesAsync()
    {
        // 已经存在角色，不重复添加
        if (await _context.AIRoles.AnyAsync())
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


        await _context.AIRoles.AddRangeAsync(roles);

        await _context.SaveChangesAsync();
    }
}