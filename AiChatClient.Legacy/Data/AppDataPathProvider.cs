using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace AiChatClient.Data;

/// <summary>
/// Resolves the application database to one stable per-user location and imports
/// the legacy relative database on first use without modifying the source file.
/// </summary>
public static class AppDataPathProvider
{
    private const string ApplicationDirectoryName = "AiChatClient";
    private const string DefaultDatabaseFileName = "aichat.db";

    public static string GetConnectionString(
        IConfiguration configuration,
        string? localApplicationDataRoot = null,
        string? currentDirectory = null,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var configuredConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "未找到 ConnectionStrings:DefaultConnection。");

        var connectionStringBuilder = new SqliteConnectionStringBuilder(configuredConnectionString);
        var configuredDataSource = string.IsNullOrWhiteSpace(connectionStringBuilder.DataSource)
            ? DefaultDatabaseFileName
            : connectionStringBuilder.DataSource;

        var databaseFileName = Path.GetFileName(configuredDataSource);
        if (string.IsNullOrWhiteSpace(databaseFileName))
        {
            databaseFileName = DefaultDatabaseFileName;
        }

        var appDataRoot = localApplicationDataRoot
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            throw new InvalidOperationException("无法确定当前用户的 LocalApplicationData 目录。");
        }

        var applicationDataDirectory = Path.Combine(appDataRoot, ApplicationDirectoryName);
        Directory.CreateDirectory(applicationDataDirectory);

        var destinationPath = Path.GetFullPath(
            Path.Combine(applicationDataDirectory, databaseFileName));

        ImportLegacyDatabaseIfRequired(
            configuredDataSource,
            destinationPath,
            currentDirectory ?? Directory.GetCurrentDirectory(),
            baseDirectory ?? AppContext.BaseDirectory);

        connectionStringBuilder.DataSource = destinationPath;
        return connectionStringBuilder.ToString();
    }

    private static void ImportLegacyDatabaseIfRequired(
        string configuredDataSource,
        string destinationPath,
        string currentDirectory,
        string baseDirectory)
    {
        if (File.Exists(destinationPath))
        {
            return;
        }

        foreach (var candidate in GetLegacyCandidates(
                     configuredDataSource,
                     currentDirectory,
                     baseDirectory))
        {
            if (!File.Exists(candidate)
                || string.Equals(
                    candidate,
                    destinationPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Copy(candidate, destinationPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(destinationPath))
            {
                // Another startup completed the one-time import first.
            }

            return;
        }
    }

    private static IEnumerable<string> GetLegacyCandidates(
        string configuredDataSource,
        string currentDirectory,
        string baseDirectory)
    {
        var candidates = new List<string>();

        if (Path.IsPathRooted(configuredDataSource))
        {
            candidates.Add(Path.GetFullPath(configuredDataSource));
        }
        else
        {
            candidates.Add(Path.GetFullPath(
                Path.Combine(currentDirectory, configuredDataSource)));
            candidates.Add(Path.GetFullPath(
                Path.Combine(currentDirectory, "AiChatClient.Legacy", configuredDataSource)));
            candidates.Add(Path.GetFullPath(
                Path.Combine(currentDirectory, "..", "AiChatClient.Legacy", configuredDataSource)));
            candidates.Add(Path.GetFullPath(
                Path.Combine(baseDirectory, configuredDataSource)));
        }

        return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
