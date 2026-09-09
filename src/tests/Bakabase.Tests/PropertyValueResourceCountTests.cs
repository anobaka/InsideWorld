using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Dto;
using Bakabase.Abstractions.Services;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.Modules.Property.Components.Properties.Multilevel;
using Bakabase.Modules.Property.Components.Properties.Tags;
using Bakabase.Service.Controllers;
using Bakabase.Service.Models.Input;
using Bakabase.TestKit.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;

namespace Bakabase.Tests;

[TestClass]
public sealed class PropertyValueResourceCountTests
{
    private const string ValueA = "Choice-A";
    private const string ValueB = "choice-b";
    private static readonly string[] ValueIds = [ValueA, ValueB, "unused"];
    private IServiceProvider _services = null!;
    private int _propertyId;
    private int[] _resourceIds = [];

    private IResourceSearchIndexService Index => _services.GetRequiredService<IResourceSearchIndexService>();
    private IResourceService Resources => _services.GetRequiredService<IResourceService>();
    private ICustomPropertyValueService Values => _services.GetRequiredService<ICustomPropertyValueService>();

    [TestInitialize]
    public async Task Setup()
    {
        _services = await TestServiceBuilder.BuildServiceProvider();
    }

    private async Task Seed(PropertyType type = PropertyType.MultipleChoice)
    {
        object options = type switch
        {
            PropertyType.SingleChoice => new SingleChoicePropertyOptions
            {
                Choices = [new() { Label = "A", Value = ValueA }, new() { Label = "B", Value = ValueB }, new() { Label = "Unused", Value = "unused" }]
            },
            PropertyType.MultipleChoice => new MultipleChoicePropertyOptions
            {
                Choices = [new() { Label = "A", Value = ValueA }, new() { Label = "B", Value = ValueB }, new() { Label = "Unused", Value = "unused" }]
            },
            PropertyType.Tags => new TagsPropertyOptions
            {
                Tags = [new(null, "A") { Value = ValueA }, new(null, "B") { Value = ValueB }, new(null, "Unused") { Value = "unused" }]
            },
            PropertyType.Multilevel => new MultilevelPropertyOptions
            {
                Data = [new() { Label = "A", Value = ValueA }, new() { Label = "B", Value = ValueB }, new() { Label = "Unused", Value = "unused" }]
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        var property = await _services.GetRequiredService<ICustomPropertyService>().Add(new CustomPropertyAddOrPutDto
        {
            Name = "Reference counts",
            Type = type,
            Options = JsonConvert.SerializeObject(options)
        });
        _propertyId = property.Id;

        var root = Path.Combine(Path.GetTempPath(), $"reference-counts-{Guid.NewGuid():N}");
        await Resources.AddOrPutRange(new[] { "apple", "apricot", "banana", "empty" }.Select(name =>
            new Resource { Path = Path.Combine(root, name), IsFile = false }).ToList());
        _resourceIds = (await Resources.GetAll()).OrderBy(r => r.FileName).Select(r => r.Id).ToArray();

        await Values.AddRange([
            Value(_resourceIds[0], type == PropertyType.SingleChoice ? ValueA : new List<string> { ValueA, ValueA, ValueB }),
            Value(_resourceIds[0], type == PropertyType.SingleChoice ? ValueA : new List<string> { ValueA },
                PropertyValueScope.Synchronization),
            Value(_resourceIds[1], type == PropertyType.SingleChoice ? ValueA : new List<string> { ValueA }),
            Value(_resourceIds[2], type == PropertyType.SingleChoice ? ValueB : new List<string> { ValueB })
        ]);
    }

    private CustomPropertyValue Value(int resourceId, object? value,
        PropertyValueScope scope = PropertyValueScope.Manual) => new()
    {
        PropertyId = _propertyId,
        ResourceId = resourceId,
        Scope = (int)scope,
        Value = value
    };

    private Task<Dictionary<string, int>?> Counts(IReadOnlySet<int>? resourceIds = null) =>
        Index.GetPropertyValueResourceCountsAsync(PropertyPool.Custom, _propertyId, ValueIds, resourceIds);

    private async Task WaitForCounts(int a, int b)
    {
        var elapsed = Stopwatch.StartNew();
        Dictionary<string, int>? counts;
        do
        {
            counts = await Counts();
            if (counts?.GetValueOrDefault(ValueA) == a && counts?.GetValueOrDefault(ValueB) == b) return;
            await Task.Delay(25);
        } while (elapsed.Elapsed < TimeSpan.FromSeconds(8));

        Assert.IsNotNull(counts);
        Assert.AreEqual(a, counts[ValueA], "The A resource count did not converge after the data change.");
        Assert.AreEqual(b, counts[ValueB], "The B resource count did not converge after the data change.");
    }

    [TestMethod]
    public async Task Counts_ColdIndex_ReturnsUnavailable()
    {
        Assert.IsFalse(Index.IsReady);
        Assert.IsNull(await Counts());
    }

    [TestMethod]
    [DataRow(PropertyType.SingleChoice, 1)]
    [DataRow(PropertyType.MultipleChoice, 2)]
    [DataRow(PropertyType.Tags, 2)]
    [DataRow(PropertyType.Multilevel, 2)]
    public async Task Counts_DeduplicateResourcesAcrossValuesAndScopes(PropertyType type, int expectedB)
    {
        await Seed(type);
        await Index.RebuildAllAsync();

        var counts = await Counts();

        Assert.IsNotNull(counts);
        Assert.AreEqual(2, counts[ValueA]);
        Assert.AreEqual(expectedB, counts[ValueB]);
        Assert.AreEqual(0, counts["unused"]);
        CollectionAssert.AreEquivalent(ValueIds, counts.Keys.ToArray(), "Response keys must preserve the original option IDs.");
    }

    [TestMethod]
    public async Task Counts_ResourceSelection_IntersectsEachOptionWithoutChangingSelection()
    {
        await Seed();
        await Index.RebuildAllAsync();
        var selection = new HashSet<int> { _resourceIds[1], _resourceIds[2], int.MaxValue };

        var counts = await Counts(selection);

        Assert.IsNotNull(counts);
        Assert.AreEqual(1, counts[ValueA]);
        Assert.AreEqual(1, counts[ValueB]);
        Assert.AreEqual(3, selection.Count);
        var emptyCounts = await Counts(new HashSet<int>());
        Assert.IsNotNull(emptyCounts);
        Assert.IsTrue(emptyCounts.Values.All(count => count == 0));
        Assert.AreEqual(2, (await Counts())![ValueA], "Scoped counts must not mutate the global postings.");
    }

    [TestMethod]
    public async Task Counts_CurrentFilter_UsesAllMatchesRegardlessOfPagination()
    {
        await Seed();
        await Index.RebuildAllAsync();
        var filename = (await _services.GetRequiredService<IPropertyService>().GetProperties(PropertyPool.Internal))
            .Single(p => p.Id == (int)InternalProperty.Filename);
        var search = new ResourceSearch
        {
            PageIndex = 2,
            PageSize = 1,
            Group = new ResourceSearchFilterGroup
            {
                Combinator = SearchCombinator.And,
                Filters = [new ResourceSearchFilter
                {
                    Property = filename,
                    PropertyPool = filename.Pool,
                    PropertyId = filename.Id,
                    Operation = SearchOperation.StartsWith,
                    DbValue = "ap"
                }]
            }
        };

        var matches = (await Resources.GetAllIds(search)).ToHashSet();
        var counts = await Counts(matches);

        Assert.AreEqual(2, matches.Count);
        Assert.IsNotNull(counts);
        Assert.AreEqual(2, counts[ValueA]);
        Assert.AreEqual(1, counts[ValueB]);
    }

    [TestMethod]
    public async Task Counts_TagsOnlySearch_RestrictsCountsToPinnedResources()
    {
        await Seed();
        await Resources.Pin(_resourceIds[2], true);
        await Index.RebuildAllAsync();

        var matches = (await Resources.GetAllIds(new ResourceSearch { Tags = [ResourceTag.Pinned] })).ToHashSet();
        var counts = await Counts(matches);

        CollectionAssert.AreEquivalent(new[] { _resourceIds[2] }, matches.ToArray());
        Assert.IsNotNull(counts);
        Assert.AreEqual(0, counts[ValueA]);
        Assert.AreEqual(1, counts[ValueB]);
    }

    [TestMethod]
    public async Task ControllerCounts_CombinesKeywordAndTags_AndIncludesUnusedOptions()
    {
        await Seed();
        await Resources.Pin(_resourceIds[1], true);
        await Resources.Pin(_resourceIds[2], true);
        await Index.RebuildAllAsync();
        var controller = ActivatorUtilities.CreateInstance<PropertyController>(_services);

        var response = await controller.GetValueResourceCounts(PropertyPool.Custom, _propertyId,
            new ResourceSearchInputModel
            {
                Keyword = "ap",
                Tags = [ResourceTag.Pinned],
                Page = 20,
                PageSize = 1
            });

        Assert.IsNotNull(response.Data);
        Assert.IsTrue(response.Data.IsReady);
        Assert.AreEqual(1, response.Data.Counts[ValueA]);
        Assert.AreEqual(0, response.Data.Counts[ValueB]);
        Assert.AreEqual(0, response.Data.Counts["unused"]);
    }

    [TestMethod]
    public async Task ControllerCounts_MultilevelCountsExactNodeReferences_AndMatchesQuickSearch()
    {
        await Seed(PropertyType.Multilevel);
        await _services.GetRequiredService<ICustomPropertyService>().Put(_propertyId, new CustomPropertyAddOrPutDto
        {
            Name = "Reference counts",
            Type = PropertyType.Multilevel,
            Options = JsonConvert.SerializeObject(new MultilevelPropertyOptions
            {
                Data = [new()
                {
                    Label = "A",
                    Value = ValueA,
                    Children = [new() { Label = "B", Value = ValueB }, new() { Label = "Unused", Value = "unused" }]
                }]
            })
        });
        await Index.RebuildAllAsync();
        var controller = ActivatorUtilities.CreateInstance<PropertyController>(_services);

        var response = await controller.GetValueResourceCounts(PropertyPool.Custom, _propertyId,
            new ResourceSearchInputModel());

        Assert.IsNotNull(response.Data);
        Assert.IsTrue(response.Data.IsReady);
        Assert.AreEqual(2, response.Data.Counts[ValueA], "A parent must not count resources referring only to a descendant.");
        Assert.AreEqual(2, response.Data.Counts[ValueB]);
        Assert.AreEqual(0, response.Data.Counts["unused"]);

        var property = await _services.GetRequiredService<IPropertyService>().GetProperty(PropertyPool.Custom, _propertyId);
        foreach (var valueId in ValueIds)
        {
            var matches = await Resources.GetAllIds(new ResourceSearch
            {
                Group = new ResourceSearchFilterGroup
                {
                    Combinator = SearchCombinator.And,
                    Filters = [new ResourceSearchFilter
                    {
                        Property = property,
                        PropertyPool = property.Pool,
                        PropertyId = property.Id,
                        Operation = SearchOperation.Contains,
                        DbValue = new List<string> { valueId }
                    }]
                }
            });
            Assert.AreEqual(response.Data.Counts[valueId], matches.Length,
                "A node's count must match the resources returned when searching that exact node.");
        }
    }

    [TestMethod]
    public async Task Counts_IncrementalChanges_TrackValueAdditionUpdateAndResourceDeletion()
    {
        await Seed();
        await Index.RebuildAllAsync();
        await WaitForCounts(2, 2);

        await Values.AddRange([Value(_resourceIds[3], new List<string> { ValueA })]);
        await WaitForCounts(3, 2);

        var manual = (await Values.GetAllDbModels(v =>
            v.ResourceId == _resourceIds[0] && v.PropertyId == _propertyId && v.Scope == (int)PropertyValueScope.Manual)).Single();
        await Values.UpdateDbModel(manual with { Value = null });
        await WaitForCounts(3, 1); // Synchronization still references A on this resource.

        var synchronized = (await Values.GetAllDbModels(v =>
            v.ResourceId == _resourceIds[0] && v.PropertyId == _propertyId && v.Scope == (int)PropertyValueScope.Synchronization)).Single();
        await Values.UpdateDbModel(synchronized with { Value = null });
        await WaitForCounts(2, 1);

        await Resources.DeleteByKeys([_resourceIds[1]]);
        await WaitForCounts(1, 1);
    }

    [TestMethod]
    public async Task Counts_ConcurrentReadsDuringIncrementalUpdates_RemainSafe()
    {
        await Seed();
        await Index.RebuildAllAsync();

        var readCounts = Task.Run(async () =>
        {
            for (var i = 0; i < 100; i++)
            {
                var counts = await Counts(new HashSet<int>(_resourceIds));
                Assert.IsNotNull(counts);
                Assert.IsTrue(counts.Values.All(count => count >= 0 && count <= _resourceIds.Length));
                await Task.Delay(1);
            }
        });
        var reindex = Task.Run(async () =>
        {
            for (var i = 0; i < 30; i++)
            {
                Index.InvalidateResources(_resourceIds);
                await Task.Delay(5);
            }
        });

        await Task.WhenAll(readCounts, reindex);
        await WaitForCounts(2, 2);
    }
}
