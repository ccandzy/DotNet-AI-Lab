using AiChatClient.Config;
using AiChatClient.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AiChatClient.Tests;

public sealed class StartupStabilityTests
{
    [Fact]
    public void Configuration_UsesApplicationDirectoryInsteadOfWorkingDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "appsettings.json"), "{\"Marker\":\"application\"}");
            Assert.NotEqual(dir, Directory.GetCurrentDirectory());
            var config = StartupConfiguration.Load(dir);
            using var lifetime = (IDisposable)config;
            Assert.Equal("application", config["Marker"]);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task MissingConfiguration_IsReportedAndStopsStartup()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            Exception? reported = null;
            var windowCreated = false;
            var started = await StartupConfiguration.TryInitializeAsync(() =>
            {
                StartupConfiguration.Load(dir);
                windowCreated = true;
                return Task.CompletedTask;
            }, ex => reported = ex);
            Assert.False(started);
            Assert.False(windowCreated);
            Assert.IsType<FileNotFoundException>(reported);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task CorruptDatabase_IsReportedWithoutContinuingToWindow()
    {
        var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
        File.WriteAllText(file, "not a SQLite database");
        try
        {
            using var services = new ServiceCollection()
                .AddDbContextFactory<AppDbContext>(options => options.UseSqlite($"Data Source={file};Pooling=False"))
                .BuildServiceProvider();
            var initializer = new DatabaseInitializer(services.GetRequiredService<IDbContextFactory<AppDbContext>>());
            Exception? reported = null;
            var started = await StartupConfiguration.TryInitializeAsync(initializer.InitializeAsync, ex => reported = ex);
            Assert.False(started);
            Assert.NotNull(reported);
        }
        finally { File.Delete(file); }
    }
}
