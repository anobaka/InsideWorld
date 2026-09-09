using Bakabase.Components;
using Bakabase.Client.Components;
using Microsoft.Extensions.Hosting;

namespace Bakabase.Client.App;

/// <summary>
/// Presents the client's forwarding host to the shell as an <see cref="IShellHost"/>.
/// </summary>
/// <remarks>
/// The adapter exists so <c>Bakabase.Shell</c> never has to reference either flavour's
/// host: this entry project is the only assembly that sees both. Its twin on the
/// all-in-one side is <c>BakabaseShellHost</c>, and the pair of them is the whole of
/// what makes one build the app and the other the client.
/// </remarks>
internal sealed class ClientShellHost(ClientHost inner) : IShellHost
{
    public IHost? Host => inner.Host;

    public Task<bool> Start(string[] args) => inner.Start(args);

    public void Dispose() => inner.Dispose();
}
