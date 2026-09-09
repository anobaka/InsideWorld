using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bakabase.Client.Components.Connection;

/// <summary>The few fields a user-machine handler needs about a resource.</summary>
/// <param name="Path">The file or folder the resource is, as the server sees it.</param>
/// <param name="Directory">Its containing folder, as the server sees it.</param>
public sealed record UpstreamResource(int Id, string? Path, string? Directory, string? DisplayName);

/// <summary>
/// The reads the client makes on its own behalf, rather than on the browser's.
/// </summary>
/// <remarks>
/// A handler that runs an action here still needs the server's answer to "which file?" —
/// the library lives there. This is that channel: signed like any other request, and
/// deliberately tiny, because every method here is a second definition of an endpoint
/// the frontend already calls.
/// </remarks>
public interface IUpstreamApi
{
    /// <summary>Null when the server did not answer or does not know that id.</summary>
    Task<UpstreamResource?> GetResourceAsync(int id, CancellationToken ct = default);
}

public sealed class UpstreamApi(HttpClient http, IUpstreamTarget target) : IUpstreamApi
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = {new JsonStringEnumConverter()}
    };

    public async Task<UpstreamResource?> GetResourceAsync(int id, CancellationToken ct = default)
    {
        // /resource/keys rather than a per-id route, because it is the one the server
        // already marks as reachable from another device.
        var resources = await ReadAsync<List<UpstreamResource>>($"/resource/keys?ids={id}", ct);

        return resources?.FirstOrDefault(r => r.Id == id);
    }

    private async Task<T?> ReadAsync<T>(string pathAndQuery, CancellationToken ct) where T : class
    {
        var destination = target.BaseAddress;

        if (string.IsNullOrEmpty(destination) || !Uri.TryCreate(destination, UriKind.Absolute, out var root))
        {
            return null;
        }

        try
        {
            var response = await http.GetAsync(new Uri(root, pathAndQuery), ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return (await JsonSerializer.DeserializeAsync<Envelope<T>>(
                await response.Content.ReadAsStreamAsync(ct), Json, ct))?.Data;
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or TaskCanceledException &&
                                  !ct.IsCancellationRequested)
        {
            // Unreachable and unparseable both mean the same thing to a caller: it has
            // no answer, and the action it was about to take cannot be taken.
            return null;
        }
    }

    private sealed record Envelope<T>(int Code, string? Message, T? Data);
}
