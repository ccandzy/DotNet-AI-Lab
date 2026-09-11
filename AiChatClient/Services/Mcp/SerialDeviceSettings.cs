using System.Text.Json;

namespace AiChatClient.Services.Mcp;

public sealed record SerialDeviceSettings(
    string PortName,
    int BaudRate = 9600,
    int DataBits = 8,
    string Parity = "None",
    string StopBits = "One",
    string Handshake = "None");

internal static class SerialDeviceSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiChatClient", "serial-settings.json");

    public static SerialDeviceSettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<SerialDeviceSettings>(File.ReadAllText(SettingsPath))
                    ?? new SerialDeviceSettings(string.Empty)
                : new SerialDeviceSettings(string.Empty);
        }
        catch
        {
            return new SerialDeviceSettings(string.Empty);
        }
    }

    public static void Save(SerialDeviceSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
