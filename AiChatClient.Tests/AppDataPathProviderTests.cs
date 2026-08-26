using AiChatClient.Data;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AiChatClient.Tests;

public sealed class AppDataPathProviderTests
{
    [Fact]
    public void GetConnectionString_ImportsLegacyDatabaseWithoutDeletingSource()
    {
        using var directory = new TemporaryDirectory();
        var currentDirectory = Path.Combine(directory.Path, "working");
        var appDataDirectory = Path.Combine(directory.Path, "app-data");
        Directory.CreateDirectory(currentDirectory);
        var legacyPath = Path.Combine(currentDirectory, "aichat.db");
        File.WriteAllText(legacyPath, "legacy-data");

        var connectionString = AppDataPathProvider.GetConnectionString(
            CreateConfiguration(),
            appDataDirectory,
            currentDirectory,
            currentDirectory);

        var destinationPath =
            new SqliteConnectionStringBuilder(connectionString).DataSource;

        Assert.Equal(
            Path.Combine(appDataDirectory, "AiChatClient", "aichat.db"),
            destinationPath);
        Assert.Equal("legacy-data", File.ReadAllText(destinationPath));
        Assert.Equal("legacy-data", File.ReadAllText(legacyPath));
    }

    [Fact]
    public void GetConnectionString_DoesNotOverwriteExistingDatabase()
    {
        using var directory = new TemporaryDirectory();
        var currentDirectory = Path.Combine(directory.Path, "working");
        var appDataDirectory = Path.Combine(directory.Path, "app-data");
        var destinationDirectory = Path.Combine(appDataDirectory, "AiChatClient");
        Directory.CreateDirectory(currentDirectory);
        Directory.CreateDirectory(destinationDirectory);
        File.WriteAllText(Path.Combine(currentDirectory, "aichat.db"), "legacy");
        var destinationPath = Path.Combine(destinationDirectory, "aichat.db");
        File.WriteAllText(destinationPath, "current");

        _ = AppDataPathProvider.GetConnectionString(
            CreateConfiguration(),
            appDataDirectory,
            currentDirectory,
            currentDirectory);
        _ = AppDataPathProvider.GetConnectionString(
            CreateConfiguration(),
            appDataDirectory,
            currentDirectory,
            currentDirectory);

        Assert.Equal("current", File.ReadAllText(destinationPath));
    }

    [Fact]
    public void GetConnectionString_RequiresConfiguredConnectionString()
    {
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(
            () => AppDataPathProvider.GetConnectionString(configuration));
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Data Source=aichat.db"
                })
            .Build();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AiChatClient.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
