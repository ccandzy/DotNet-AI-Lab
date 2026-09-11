using Microsoft.Extensions.Configuration;

namespace AiChatClient.Config;

internal static class StartupConfiguration
{
    internal static IConfigurationRoot Load(string? applicationDirectory = null) =>
        new ConfigurationBuilder()
            .SetBasePath(applicationDirectory ?? AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
            .Build();

    internal static async Task<bool> TryInitializeAsync(Func<Task> initialize, Action<Exception> reportFailure)
    {
        try { await initialize(); return true; }
        catch (Exception ex) { reportFailure(ex); return false; }
    }
}
