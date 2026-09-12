using System.Threading.Tasks;
using Bakabase.Shell.Components;
using Bakabase.Service.Components;
using Microsoft.Extensions.Hosting;

namespace Bakabase.App;

/// <summary>
/// Presents the all-in-one server host to the shell as an <see cref="IShellHost"/>.
/// </summary>
/// <remarks>
/// The adapter exists so <c>Bakabase.Shell</c> never has to reference
/// <c>Bakabase.Service</c>: this entry project is the only assembly that sees both.
/// A client flavour supplies its own implementation over the local forwarding layer.
/// </remarks>
internal sealed class BakabaseShellHost(BakabaseHost inner) : IShellHost
{
    public IHost? Host => inner.Host;

    public Task<bool> Start(string[] args) => inner.Start(args);

    public void Dispose() => inner.Dispose();
}
