namespace Bakabase.Modules.RemoteAccess.Abstractions.Components;

/// <summary>
/// Where remote-access state lives on disk, split into "tell me" and "make it" on
/// purpose.
/// </summary>
/// <remarks>
/// <para>
/// The all-in-one ships with remote access off, and a user who never turns it on must
/// never find a <c>remote-access</c> folder in their data directory — an empty folder
/// appearing after an upgrade is exactly the kind of thing that reads as the app doing
/// something behind your back.
/// </para>
/// <para>
/// The catch is that the obvious way to resolve the path,
/// <c>AppService.RequestAppDataDirectory</c>, creates the directory as a side effect of
/// being asked. So <see cref="Path"/> resolves without touching the disk and every read
/// goes through it; <see cref="Ensure"/> is called only when something is actually
/// being written.
/// </para>
/// </remarks>
public interface IRemoteAccessDataDirectory
{
    /// <summary>The directory path. Creates nothing.</summary>
    string Path { get; }

    /// <summary>Creates the directory if needed and returns it. Write paths only.</summary>
    string Ensure();
}
