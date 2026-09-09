using Bakabase.Modules.Property.Abstractions.Components;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.Modules.Property.Components.Properties.Choice.Abstractions;
using Bakabase.Modules.Property.Components.Properties.Multilevel;
using Bakabase.Modules.Property.Components.Properties.Tags;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.StandardValue.Models.Domain;

namespace Bakabase.Modules.Property.Components;

/// <summary>
/// Applies the same label identity rules to options saved by editors/importers as to values prepared
/// by descriptors. Never removes a previously persisted ID: resource values and saved filters may
/// still refer to it. Only newly introduced duplicates are folded into the first existing option.
/// </summary>
public static class ReferencePropertyOptionsNormalizer
{
    public static void Normalize(object? options, object? previousOptions = null)
    {
        if (options is not IReferencePropertyOptions { IgnoreCase: true } referenceOptions)
        {
            return;
        }

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        var preservedIds = GetIds(previousOptions).ToHashSet(StringComparer.Ordinal);
        var comparer = referenceOptions.GetLabelComparer();
        switch (options)
        {
            case SingleChoicePropertyOptions single:
                single.Choices = NormalizeChoices(single.Choices);
                single.DefaultValue = MapId(single.DefaultValue);
                break;
            case MultipleChoicePropertyOptions multiple:
                multiple.Choices = NormalizeChoices(multiple.Choices);
                multiple.DefaultValue = MapIds(multiple.DefaultValue);
                break;
            case TagsPropertyOptions tags:
                tags.Tags = NormalizeList(tags.Tags, t => new TagValue(t.Group, t.Name), t => t.Value,
                    new TagComparer(comparer));
                break;
            case MultilevelPropertyOptions multilevel:
                multilevel.Data = NormalizeNodes(multilevel.Data);
                multilevel.DefaultValue = MapIds(multilevel.DefaultValue);
                break;
        }

        string? MapId(string? id) => id == null ? null : aliases.GetValueOrDefault(id, id);
        List<string>? MapIds(List<string>? ids) => ids?.Select(id => MapId(id)!).Distinct().ToList();

        List<ChoiceOptions>? NormalizeChoices(List<ChoiceOptions>? choices) =>
            NormalizeList(choices, c => c.Label, c => c.Value, comparer);

        List<MultilevelDataOptions>? NormalizeNodes(List<MultilevelDataOptions>? nodes)
        {
            var result = NormalizeList(nodes, n => n.Label, n => n.Value, comparer,
                (target, duplicate) =>
                {
                    if (duplicate.Children?.Count > 0)
                    {
                        target.Children ??= [];
                        target.Children.AddRange(duplicate.Children);
                    }
                });
            if (result != null)
            {
                foreach (var node in result)
                {
                    node.Children = NormalizeNodes(node.Children);
                }
            }

            return result;
        }

        List<T>? NormalizeList<T, TKey>(List<T>? values, Func<T, TKey> key, Func<T, string> id,
            IEqualityComparer<TKey> keyComparer, Action<T, T>? merge = null) where TKey : notnull
        {
            if (values == null) return null;
            var firstByLabel = new Dictionary<TKey, T>(keyComparer);
            // An editor may insert a new option before an old one. Persisted IDs still take
            // precedence, and every old ID remains independently addressable.
            foreach (var value in values.Where(v => preservedIds.Contains(id(v))))
            {
                firstByLabel.TryAdd(key(value), value);
            }

            var result = new List<T>();
            foreach (var value in values)
            {
                if (!preservedIds.Contains(id(value)) && firstByLabel.TryGetValue(key(value), out var first))
                {
                    if (id(value) != id(first)) aliases[id(value)] = id(first);
                    merge?.Invoke(first, value);
                    continue;
                }

                firstByLabel.TryAdd(key(value), value);
                result.Add(value);
            }

            return result;
        }
    }

    private static IEnumerable<string> GetIds(object? options) => options switch
    {
        SingleChoicePropertyOptions single => single.Choices?.Select(c => c.Value) ?? [],
        MultipleChoicePropertyOptions multiple => multiple.Choices?.Select(c => c.Value) ?? [],
        TagsPropertyOptions tags => tags.Tags?.Select(t => t.Value) ?? [],
        MultilevelPropertyOptions multilevel => multilevel.Data?.ExtractValues(false) ?? [],
        _ => []
    };

    private sealed class TagComparer(StringComparer comparer) : IEqualityComparer<TagValue>
    {
        public bool Equals(TagValue? x, TagValue? y) => ReferenceEquals(x, y) ||
            x != null && y != null && comparer.Equals(x.Group, y.Group) && comparer.Equals(x.Name, y.Name);

        public int GetHashCode(TagValue tag) => HashCode.Combine(
            tag.Group == null ? 0 : comparer.GetHashCode(tag.Group), comparer.GetHashCode(tag.Name));
    }
}
