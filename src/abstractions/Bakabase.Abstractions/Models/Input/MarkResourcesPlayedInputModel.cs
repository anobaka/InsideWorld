namespace Bakabase.Abstractions.Models.Input;

/// <param name="Item">
/// The file that was played, identifying which part of a multi-file resource it was.
/// Optional; the resource's own timestamp is updated either way.
/// </param>
public record MarkResourcePlayed(int ResourceId, string? Item);

public record MarkResourcesPlayedInputModel
{
    public List<MarkResourcePlayed> Items { get; set; } = [];
}
