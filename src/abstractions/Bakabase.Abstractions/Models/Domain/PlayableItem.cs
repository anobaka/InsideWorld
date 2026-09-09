using Bakabase.Abstractions.Models.Domain.Constants;

namespace Bakabase.Abstractions.Models.Domain;

/// <summary>
/// Represents a playable item from any data origin.
/// For FileSystem: Key is the file path.
/// For Steam: Key is the AppId (used with steam:// URI).
/// For DLsite/ExHentai: Key is source-specific identifier.
/// </summary>
public record PlayableItem
{
    /// <summary>
    /// The origin that provided this playable item.
    /// </summary>
    public DataOrigin Origin { get; set; }

    /// <summary>
    /// Origin-specific key. Interpretation depends on the origin:
    /// - FileSystem: absolute file path
    /// - Steam: AppId (for steam://rungameid/{key})
    /// - DLsite: WorkId
    /// - ExHentai: GalleryId/Token
    /// </summary>
    public string Key { get; set; } = null!;

    /// <summary>
    /// Optional display name for the UI.
    /// If null, the Key is shown.
    /// </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// One playable item, together with the resource it belongs to.
/// </summary>
/// <remarks>
/// What "pick something to play" answers when the picking and the playing happen on
/// different machines. A <see cref="PlayableItem"/> alone is not enough there: the caller
/// still has to say which resource was played, and it did not choose it.
/// </remarks>
public sealed record PlayableItemPick(int ResourceId, DataOrigin Origin, string Key);
