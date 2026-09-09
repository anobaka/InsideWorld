namespace Bakabase.Client.Components.UserMachine;

/// <summary>
/// Builds URLs that come back through this client's own forwarding layer.
/// </summary>
/// <remarks>
/// Handing a player one of these is what lets an unmapped library play at all: the
/// player opens a plain loopback URL with no credentials, and the forwarding layer signs
/// and relays it. The device key stays in this process, where a player could never have
/// held it anyway.
/// </remarks>
public sealed class LoopbackAddressProvider(int port) : ILoopbackAddressProvider
{
    public string BaseAddress { get; } = $"http://127.0.0.1:{port}";

    public string BuildRawFileUrl(string serverPath) =>
        $"{BaseAddress}/file/raw?fullname={Uri.EscapeDataString(serverPath)}";
}
