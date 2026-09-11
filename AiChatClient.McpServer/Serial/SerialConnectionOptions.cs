using System.IO.Ports;

namespace AiChatClient.McpServer.Serial;

internal sealed record SerialConnectionOptions(
    string PortName,
    int BaudRate,
    int DataBits,
    Parity Parity,
    StopBits StopBits,
    Handshake Handshake);

internal enum DeviceMeasurementState
{
    Disconnected,
    PortOpen,
    Starting,
    Measuring,
    Faulted
}
