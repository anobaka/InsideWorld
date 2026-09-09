namespace Bakabase.Modules.Property.Abstractions.Components;

/// <summary>
/// Controls matching labels to reference IDs; labels and IDs themselves are never case-normalized.
/// Existing IDs remain valid when this setting changes. Matching uses the first equivalent option.
/// </summary>
public interface IReferencePropertyOptions
{
    bool IgnoreCase { get; set; }
}
