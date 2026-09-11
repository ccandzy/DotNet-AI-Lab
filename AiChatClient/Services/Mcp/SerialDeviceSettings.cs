using System.Text.Json;

namespace AiChatClient.Services.Mcp;

public sealed record SerialDeviceSettings(
    string PortName,
    int BaudRate = 115200,
    int DataBits = 8,
    string Parity = "None",
    string StopBits = "One",
    string Handshake = "None",
    int Frequency = 3,
    int DurationSeconds = 60,
    int ProfileVersion = 2);

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
            if (!File.Exists(SettingsPath)) return new SerialDeviceSettings(string.Empty);
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<SerialDeviceSettings>(json)
                ?? new SerialDeviceSettings(string.Empty);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(SerialDeviceSettings.ProfileVersion), out _))
            {
                // 第一版错误地使用了 9600 且没有测量指令参数；升级到已验证项目的协议配置。
                settings = settings with
                {
                    BaudRate = 115200,
                    Frequency = 3,
                    DurationSeconds = 60,
                    ProfileVersion = 2
                };
                Save(settings);
            }
            return settings;
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

internal interface ISerialDeviceSettingsPersistence
{
    SerialDeviceSettings Load();
    void Save(SerialDeviceSettings settings);
}

internal sealed class LocalSerialDeviceSettingsPersistence : ISerialDeviceSettingsPersistence
{
    public SerialDeviceSettings Load() => SerialDeviceSettingsStore.Load();
    public void Save(SerialDeviceSettings settings) => SerialDeviceSettingsStore.Save(settings);
}
