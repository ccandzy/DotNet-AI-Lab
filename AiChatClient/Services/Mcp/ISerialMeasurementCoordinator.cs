namespace AiChatClient.Services.Mcp;

public interface ISerialMeasurementCoordinator
{
    event EventHandler<SerialDeviceStatusChangedEventArgs>? StatusChanged;

    Task<IReadOnlyList<string>> ListPortsAsync(CancellationToken cancellationToken = default);
    Task<string> OpenAsync(SerialDeviceSettings settings, CancellationToken cancellationToken = default);
    Task<string> OpenSavedAsync(CancellationToken cancellationToken = default);
    Task<string> StartAsync(SerialDeviceSettings settings, CancellationToken cancellationToken = default);
    Task<string> StartSavedAsync(CancellationToken cancellationToken = default);
    Task<string> StopAsync(CancellationToken cancellationToken = default);
    Task<string> CloseAsync(CancellationToken cancellationToken = default);
    Task<string> GetStatusAsync(CancellationToken cancellationToken = default);
}

public sealed class SerialDeviceStatusChangedEventArgs(string status) : EventArgs
{
    public string Status { get; } = status;
}
