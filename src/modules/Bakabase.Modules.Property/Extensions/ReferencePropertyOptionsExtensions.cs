using Bakabase.Modules.Property.Abstractions.Components;

namespace Bakabase.Modules.Property.Extensions;

public static class ReferencePropertyOptionsExtensions
{
    public static StringComparer GetLabelComparer(this IReferencePropertyOptions? options) =>
        options?.IgnoreCase == true ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}
