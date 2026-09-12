using Bakabase.Infrastructures.Components.App.Upgrade.Abstractions;

namespace Bakabase.Client.Remoting.Components.Updating;

/// <summary>
/// Where the client looks for its own updates.
/// </summary>
/// <remarks>
/// <para>
/// A prefix of its own, and that is the whole point. Velopack's updater installs
/// whatever the feed at <c>&lt;base&gt;/&lt;rid&gt;/</c> offers, so pointing the client at the
/// all-in-one's feed would have it replace itself with the all-in-one on the next
/// check — and the reverse, if the client's packages ever landed there, would do the
/// same to every existing installation. The release pipeline publishes the two under
/// separate prefixes for the same reason.
/// </para>
/// <para>
/// The environment variable mirrors the server's <c>BAKABASE_UPDATE_URL</c>, under a
/// name of its own so an operator pointing one flavour at a private mirror does not
/// silently move the other.
/// </para>
/// </remarks>
public sealed class ClientUpdateSource : IAppUpdateSource
{
    public const string EnvVarName = "BAKABASE_CLIENT_UPDATE_URL";

    public const string DefaultBaseUrl = "https://cdn-public.anobaka.com/app/bakabase-client/releases/";

    public string GetBaseUrl()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvVarName);

        return string.IsNullOrWhiteSpace(envOverride) ? DefaultBaseUrl : envOverride.Trim();
    }
}
