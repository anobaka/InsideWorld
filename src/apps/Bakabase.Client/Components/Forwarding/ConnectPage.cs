using System.Reflection;

namespace Bakabase.Client.Components.Forwarding;

/// <summary>
/// The connect page, embedded in this assembly.
/// </summary>
/// <remarks>
/// <para>
/// One self-contained HTML file with no build step and no dependencies. That is what
/// makes it compatible with the rule that the client package carries no web frontend:
/// the frontend is the server's, downloaded from the server, and this is not it — it
/// is the few kilobytes needed to ask which server, shown only when there is no answer
/// to that question yet.
/// </para>
/// <para>
/// It is embedded rather than shipped as a file so it cannot go missing from an
/// install, and so nothing on disk can be swapped for a page that would then be
/// running at this client's own origin with its <c>/client</c> API in reach.
/// </para>
/// </remarks>
public static class ConnectPage
{
    private const string ResourceName = "Bakabase.Client.Assets.connect.html";

    private static readonly Lazy<string> Content = new(() =>
    {
        using var stream = typeof(ConnectPage).Assembly.GetManifestResourceStream(ResourceName) ??
                           throw new InvalidOperationException(
                               $"'{ResourceName}' is missing from {typeof(ConnectPage).Assembly.GetName().Name}. " +
                               "It is an EmbeddedResource in the client csproj; a build that drops it leaves " +
                               "the client with no way to reach a server at all.");

        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    });

    public static string Html => Content.Value;
}
