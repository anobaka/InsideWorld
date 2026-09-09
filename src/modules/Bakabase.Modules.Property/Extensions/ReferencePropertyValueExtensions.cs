using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.Modules.Property.Components.Properties.Multilevel;
using Bakabase.Modules.Property.Components.Properties.Tags;

namespace Bakabase.Modules.Property.Extensions;

public static class ReferencePropertyValueExtensions
{
    /// <summary>Lists stable option IDs, including unused options and every level of a tree.</summary>
    public static IEnumerable<string> GetReferenceValueIds(this Bakabase.Abstractions.Models.Domain.Property property)
    {
        return property.Options switch
        {
            SingleChoicePropertyOptions options => options.Choices?.Select(c => c.Value) ?? [],
            MultipleChoicePropertyOptions options => options.Choices?.Select(c => c.Value) ?? [],
            TagsPropertyOptions options => options.Tags?.Select(t => t.Value) ?? [],
            MultilevelPropertyOptions options => EnumerateNodes(options.Data),
            _ => []
        };
    }

    private static IEnumerable<string> EnumerateNodes(IEnumerable<MultilevelDataOptions>? nodes)
    {
        foreach (var node in nodes ?? [])
        {
            yield return node.Value;
            foreach (var childId in EnumerateNodes(node.Children))
            {
                yield return childId;
            }
        }
    }
}
