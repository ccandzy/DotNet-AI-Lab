using System.ComponentModel;
using AiChatClient.Services.Mcp;
using Microsoft.SemanticKernel;

namespace AiChatClient.Plugins;

public sealed class SerialDevicePlugin(ISerialMeasurementCoordinator coordinator)
{
    [KernelFunction("list_serial_ports")]
    [Description("列出当前计算机上可用于大激发设备的串口。")]
    public async Task<string> ListSerialPortsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var ports = await coordinator.ListPortsAsync(cancellationToken);
            return ports.Count == 0 ? "未发现可用串口。" : string.Join("、", ports);
        });
    }

    [KernelFunction("open_serial")]
    [Description("使用用户在右侧设备面板最后保存的参数打开大激发设备串口。不要猜测端口或串口参数。")]
    public Task<string> OpenSerialAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => coordinator.OpenSavedAsync(cancellationToken));

    [KernelFunction("start_measurement")]
    [Description("启动大激发设备测量。自动使用用户最后保存的串口、频率和测定时间配置。")]
    public Task<string> StartMeasurementAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => coordinator.StartSavedAsync(cancellationToken));

    [KernelFunction("stop_measurement")]
    [Description("停止大激发设备测量，但保持串口连接。")]
    public Task<string> StopMeasurementAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => coordinator.StopAsync(cancellationToken));

    [KernelFunction("close_serial")]
    [Description("停止测量并关闭大激发设备串口。")]
    public Task<string> CloseSerialAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => coordinator.CloseAsync(cancellationToken));

    [KernelFunction("get_device_status")]
    [Description("查询大激发设备的串口连接和测量状态。")]
    public Task<string> GetDeviceStatusAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() => coordinator.GetStatusAsync(cancellationToken));

    private static async Task<string> ExecuteAsync(Func<Task<string>> operation)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return $"设备操作失败：{exception.Message}";
        }
    }
}
