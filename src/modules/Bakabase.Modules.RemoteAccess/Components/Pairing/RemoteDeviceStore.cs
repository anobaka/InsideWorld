using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;

namespace Bakabase.Modules.RemoteAccess.Components.Pairing;

public interface IRemoteDeviceStore
{
    /// <summary>
    /// The current contents, or an empty set when nothing has been written yet. Never
    /// creates the file or its directory — this runs on every signed request.
    /// </summary>
    RemoteDeviceStoreData Read();

    /// <summary>
    /// Applies a change and writes it. Creates the directory and file on first use.
    /// Serialized against other mutations, and the write is atomic, so a crash halfway
    /// through leaves the previous contents rather than a truncated file.
    /// </summary>
    Task<T> MutateAsync<T>(Func<RemoteDeviceStoreData, T> mutate, CancellationToken ct = default);

    Task MutateAsync(Action<RemoteDeviceStoreData> mutate, CancellationToken ct = default);

    /// <summary>Whether anything has been persisted. Used by tests and by the first-run check.</summary>
    bool Exists { get; }
}

/// <summary>
/// <c>devices.json</c> under the remote-access data directory.
/// </summary>
/// <remarks>
/// Held in memory after the first read because every signed request needs it, and
/// written back only when something changes.
/// </remarks>
public sealed class RemoteDeviceStore(IRemoteAccessDataDirectory directory) : IRemoteDeviceStore
{
    public const string FileName = "devices.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = {new JsonStringEnumConverter()}
    };

    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private RemoteDeviceStoreData? _cache;
    private readonly Lock _cacheGate = new();

    private string FilePath => Path.Combine(directory.Path, FileName);

    public bool Exists => File.Exists(FilePath);

    public RemoteDeviceStoreData Read()
    {
        lock (_cacheGate)
        {
            if (_cache != null)
            {
                return _cache;
            }
        }

        var loaded = Load();

        lock (_cacheGate)
        {
            return _cache ??= loaded;
        }
    }

    private RemoteDeviceStoreData Load()
    {
        var path = FilePath;
        if (!File.Exists(path))
        {
            return new RemoteDeviceStoreData();
        }

        try
        {
            return JsonSerializer.Deserialize<RemoteDeviceStoreData>(File.ReadAllText(path), SerializerOptions)
                   ?? new RemoteDeviceStoreData();
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            // A corrupt file must not take the server down on startup. Treating it as
            // empty means paired devices have to pair again, which is visible and
            // recoverable; throwing here would not be.
            return new RemoteDeviceStoreData();
        }
    }

    public async Task MutateAsync(Action<RemoteDeviceStoreData> mutate, CancellationToken ct = default) =>
        await MutateAsync<object?>(data =>
        {
            mutate(data);
            return null;
        }, ct);

    public async Task<T> MutateAsync<T>(Func<RemoteDeviceStoreData, T> mutate, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            var data = Read();
            var result = mutate(data);

            var dir = directory.Ensure();
            var path = Path.Combine(dir, FileName);
            var temp = path + ".tmp";

            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(data, SerializerOptions), ct);
            File.Move(temp, path, true);

            lock (_cacheGate)
            {
                _cache = data;
            }

            return result;
        }
        finally
        {
            _writeGate.Release();
        }
    }
}
