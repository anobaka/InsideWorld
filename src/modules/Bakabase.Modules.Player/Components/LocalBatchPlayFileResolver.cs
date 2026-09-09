using Bakabase.Abstractions.Components.Configuration;
using Bakabase.Modules.Player.Abstractions.Components;

namespace Bakabase.Modules.Player.Components;

/// <summary>
/// The files are on this machine, so a path is a path.
/// </summary>
/// <remarks>
/// Files may have moved since they were cached, and silently feeding a player dead paths
/// produces confusing in-player errors instead of a count of what went missing — so the
/// check happens here rather than being left to the player.
/// </remarks>
public sealed class LocalBatchPlayFileResolver : IBatchPlayFileResolver
{
    public string? Resolve(string path) => File.Exists(path) ? ToOsSafePath(path) : null;

    private static string ToOsSafePath(string path) =>
        OperatingSystem.IsWindows()
            ? path.Replace(InternalOptions.DirSeparator, InternalOptions.WindowsSpecificDirSeparator)
            : path;
}
