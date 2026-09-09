using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Tasks;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Dto;
using Bakabase.Abstractions.Models.Input;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bakabase.Modules.Property;
using Bakabase.Modules.Property.Abstractions.Models.Db;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.Modules.Property.Components.Properties.Choice.Abstractions;
using Bakabase.Modules.Property.Components.Properties.Multilevel;
using Bakabase.Modules.Property.Components.Properties.Tags;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.StandardValue.Extensions;
using Bakabase.TestKit.Utils;
using Bootstrap.Components.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Bakabase.Tests;

/// <summary>
/// Regression coverage for the canonical business-value representation of
/// <see cref="PropertyMarkEffect.Value"/> and the legacy V220 rows that were incorrectly
/// seeded with serialized database values. A partial sync must not reinterpret an old option
/// id as a label and create a second reference whose label is that id.
/// </summary>
[TestClass]
public sealed class PathMarkReferenceValueRegressionTests
{
    private string _testRoot = null!;
    private IServiceProvider _sp = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _sp = await TestServiceBuilder.BuildServiceProvider();
        _testRoot = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            $"PathMarkReferenceValueRegressionTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            try
            {
                Directory.Delete(_testRoot, true);
            }
            catch
            {
                // Best-effort cleanup. A failed assertion should remain the test's failure.
            }
        }
    }

    [TestMethod]
    public async Task PartialSync_V220ReferenceEffectsCanonicalizeWithoutChangingDbValues_AndFiltersMatch()
    {
        var resourceService = _sp.GetRequiredService<IResourceService>();
        var propertyService = _sp.GetRequiredService<ICustomPropertyService>();
        var propertyValueService = _sp.GetRequiredService<ICustomPropertyValueService>();
        var pathMarkService = _sp.GetRequiredService<IPathMarkService>();
        var effectService = _sp.GetRequiredService<IPathMarkEffectService>();

        // The reported production case extracts the value from a directory layer.
        // Keep that exact path in the single-choice case; the other reference types share
        // the same resource while exercising their distinct standard-value shapes.
        var resourcePath = Path.Combine(_testRoot, "Action");
        Directory.CreateDirectory(resourcePath);
        await resourceService.AddOrPutRange([
            new Resource
            {
                Path = resourcePath,
                IsFile = false
            }
        ]);
        var resource = (await resourceService.GetAll()).Should().ContainSingle().Subject;

        var singleChoiceId = Guid.NewGuid().ToString();
        var multipleChoiceId = Guid.NewGuid().ToString();
        var tagId = Guid.NewGuid().ToString();
        var multilevelRootId = Guid.NewGuid().ToString();
        var multilevelLeafId = Guid.NewGuid().ToString();

        var singleChoice = await AddProperty(
            "Legacy single choice",
            PropertyType.SingleChoice,
            new SingleChoicePropertyOptions
            {
                Choices = [new ChoiceOptions {Label = "Action", Value = singleChoiceId}]
            });
        var multipleChoice = await AddProperty(
            "Legacy multiple choice",
            PropertyType.MultipleChoice,
            new MultipleChoicePropertyOptions
            {
                Choices = [new ChoiceOptions {Label = "Comedy", Value = multipleChoiceId}]
            });
        var tags = await AddProperty(
            "Legacy tags",
            PropertyType.Tags,
            new TagsPropertyOptions
            {
                Tags = [new TagsPropertyOptions.TagOptions("Genre", "Drama") {Value = tagId}]
            });
        var multilevel = await AddProperty(
            "Legacy multilevel",
            PropertyType.Multilevel,
            new MultilevelPropertyOptions
            {
                Data =
                [
                    new MultilevelDataOptions
                    {
                        Label = "Region",
                        Value = multilevelRootId,
                        Children =
                        [
                            new MultilevelDataOptions
                            {
                                Label = "Japan",
                                Value = multilevelLeafId
                            }
                        ]
                    }
                ]
            });

        var legacyCases = new[]
        {
            (Property: singleChoice, SerializedDbValue: singleChoiceId, CanonicalEffectValue: "Action"),
            (Property: multipleChoice, SerializedDbValue: multipleChoiceId, CanonicalEffectValue: "Comedy"),
            (Property: tags, SerializedDbValue: tagId, CanonicalEffectValue: "Genre,Drama"),
            (Property: multilevel, SerializedDbValue: multilevelLeafId, CanonicalEffectValue: "Region,Japan")
        };
        var contextMarkIds = new Dictionary<int, int>();

        foreach (var (property, serializedDbValue, canonicalEffectValue) in legacyCases)
        {
            // V220 created a Synced mark plus an effect copied verbatim from
            // CustomPropertyValues.Value.  It did not collect the path label again.
            var contextMark = property.Id == singleChoice.Id
                ? await AddLayerPropertyMark(property.Id)
                : await AddPropertyMark(property.Id, canonicalEffectValue);
            await pathMarkService.MarkAsSynced(contextMark.Id);
            contextMarkIds[property.Id] = contextMark.Id;

            await propertyValueService.AddDbModelRange([
                new CustomPropertyValueDbModel
                {
                    ResourceId = resource.Id,
                    PropertyId = property.Id,
                    Scope = (int) PropertyValueScope.Synchronization,
                    Value = serializedDbValue
                }
            ]);
            await effectService.AddPropertyEffects([
                new PropertyMarkEffect
                {
                    MarkId = contextMark.Id,
                    PropertyPool = PropertyPool.Custom,
                    PropertyId = property.Id,
                    ResourceId = resource.Id,
                    Value = serializedDbValue,
                    Priority = contextMark.Priority
                }
            ]);
        }

        // Sync an unrelated mark. The four migrated marks stay Synced and therefore enter
        // the run only through ContextOldEffectsByMarkId -- the beta.144 corruption path.
        var triggerProperty = await AddProperty("Partial-sync trigger", PropertyType.SingleLineText);
        await AddPropertyMark(triggerProperty.Id, "touch");
        await SyncPendingPropertyMarks();

        var refreshedSingleChoice = await propertyService.GetByKey(singleChoice.Id);
        var refreshedMultipleChoice = await propertyService.GetByKey(multipleChoice.Id);
        var refreshedTags = await propertyService.GetByKey(tags.Id);
        var refreshedMultilevel = await propertyService.GetByKey(multilevel.Id);

        var singleOptions = refreshedSingleChoice.Options.Should()
            .BeOfType<SingleChoicePropertyOptions>().Subject;
        singleOptions.Choices.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ChoiceOptions {Label = "Action", Value = singleChoiceId});

        var multipleOptions = refreshedMultipleChoice.Options.Should()
            .BeOfType<MultipleChoicePropertyOptions>().Subject;
        multipleOptions.Choices.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ChoiceOptions {Label = "Comedy", Value = multipleChoiceId});

        var tagOptions = refreshedTags.Options.Should().BeOfType<TagsPropertyOptions>().Subject;
        tagOptions.Tags.Should().ContainSingle(t =>
            t.Group == "Genre" && t.Name == "Drama" && t.Value == tagId);

        var multilevelOptions = refreshedMultilevel.Options.Should()
            .BeOfType<MultilevelPropertyOptions>().Subject;
        var multilevelRoot = multilevelOptions.Data.Should().ContainSingle().Subject;
        multilevelRoot.Label.Should().Be("Region");
        multilevelRoot.Value.Should().Be(multilevelRootId);
        var multilevelLeaf = multilevelRoot.Children.Should().ContainSingle().Subject;
        multilevelLeaf.Label.Should().Be("Japan");
        multilevelLeaf.Value.Should().Be(multilevelLeafId);

        foreach (var (property, serializedDbValue, canonicalEffectValue) in legacyCases)
        {
            var stored = (await propertyValueService.GetAllDbModels(v =>
                    v.ResourceId == resource.Id && v.PropertyId == property.Id))
                .Should().ContainSingle().Subject;
            stored.Value.Should().Be(serializedDbValue,
                "a V220 DB value must not be converted as if its UUID were a business label");

            var normalizedEffect = (await effectService.GetPropertyEffectsByMarkId(contextMarkIds[property.Id]))
                .Should().ContainSingle().Subject;
            normalizedEffect.Value.Should().Be(canonicalEffectValue,
                "legacy DB-valued effects should be rewritten to the canonical serialized business value");
        }

        // Filtering operates on DB ids. This catches the user-visible half of the regression:
        // every original human-labelled option must still select the id stored on the resource.
        await RebuildSearchIndex();
        await AssertReferenceSearchMatches(resource.Id, refreshedSingleChoice,
            SearchOperation.Equals, singleChoiceId);
        await AssertReferenceSearchMatches(resource.Id, refreshedMultipleChoice,
            SearchOperation.Contains, new List<string> {multipleChoiceId});
        await AssertReferenceSearchMatches(resource.Id, refreshedTags,
            SearchOperation.Contains, new List<string> {tagId});
        await AssertReferenceSearchMatches(resource.Id, refreshedMultilevel,
            SearchOperation.Contains, new List<string> {multilevelLeafId});
    }

    [TestMethod]
    public async Task PartialSync_Beta144PollutedSingleChoice_RepairsValueAndSearchWithoutDeletingChoices()
    {
        var resourceService = _sp.GetRequiredService<IResourceService>();
        var propertyService = _sp.GetRequiredService<ICustomPropertyService>();
        var propertyValueService = _sp.GetRequiredService<ICustomPropertyValueService>();
        var pathMarkService = _sp.GetRequiredService<IPathMarkService>();
        var effectService = _sp.GetRequiredService<IPathMarkEffectService>();

        var resourcePath = Path.Combine(_testRoot, "Movie");
        Directory.CreateDirectory(resourcePath);
        await resourceService.AddOrPutRange([new Resource {Path = resourcePath, IsFile = false}]);
        var resource = (await resourceService.GetAll()).Should().ContainSingle().Subject;

        // beta.144 interpreted the V220 effect's old DB id (U0) as a business label,
        // creating U1 and moving the synchronized property value to it.
        var originalChoiceId = Guid.NewGuid().ToString();
        var pollutedChoiceId = Guid.NewGuid().ToString();
        var property = await AddProperty(
            "Polluted single choice",
            PropertyType.SingleChoice,
            new SingleChoicePropertyOptions
            {
                Choices =
                [
                    new ChoiceOptions {Label = "Action", Value = originalChoiceId},
                    new ChoiceOptions {Label = originalChoiceId, Value = pollutedChoiceId}
                ]
            });

        var contextMark = await AddPropertyMark(property.Id, "Action");
        await pathMarkService.MarkAsSynced(contextMark.Id);
        await propertyValueService.AddDbModelRange([
            new CustomPropertyValueDbModel
            {
                ResourceId = resource.Id,
                PropertyId = property.Id,
                Scope = (int) PropertyValueScope.Synchronization,
                Value = pollutedChoiceId
            }
        ]);
        await effectService.AddPropertyEffects([
            new PropertyMarkEffect
            {
                MarkId = contextMark.Id,
                PropertyPool = PropertyPool.Custom,
                PropertyId = property.Id,
                ResourceId = resource.Id,
                Value = originalChoiceId,
                Priority = contextMark.Priority
            }
        ]);

        var triggerProperty = await AddProperty("Partial-sync trigger", PropertyType.SingleLineText);
        await AddPropertyMark(triggerProperty.Id, "touch");
        await SyncPendingPropertyMarks();

        var refreshedProperty = await propertyService.GetByKey(property.Id);
        var refreshedOptions = refreshedProperty.Options.Should()
            .BeOfType<SingleChoicePropertyOptions>().Subject;
        refreshedOptions.Choices.Should().HaveCount(2,
            "path-mark sync cannot safely delete an orphan option that may have been user-created");
        refreshedOptions.Choices.Should().Contain(c =>
            c.Label == "Action" && c.Value == originalChoiceId);
        refreshedOptions.Choices.Should().Contain(c =>
            c.Label == originalChoiceId && c.Value == pollutedChoiceId);

        var stored = (await propertyValueService.GetAllDbModels(v =>
                v.ResourceId == resource.Id && v.PropertyId == property.Id))
            .Should().ContainSingle().Subject;
        stored.Value.Should().Be(originalChoiceId,
            "the polluted U1 reference must be repaired to the original human-labelled option U0");

        var normalizedEffect = (await effectService.GetPropertyEffectsByMarkId(contextMark.Id))
            .Should().ContainSingle().Subject;
        normalizedEffect.Value.Should().Be("Action",
            "the V220 DB-valued effect should be persisted in canonical business-value form");

        var reloadedResource = await resourceService.Get(resource.Id, ResourceAdditionalItem.Properties);
        var reloadedValue = reloadedResource!.Properties![(int) PropertyPool.Custom][property.Id]
            .Values!.Should().ContainSingle(v => v.Scope == (int) PropertyValueScope.Synchronization).Subject;
        reloadedValue.Value.Should().Be(originalChoiceId);
        reloadedValue.BizValue.Should().Be("Action");

        await RebuildSearchIndex();
        await AssertReferenceSearchMatches(resource.Id, refreshedProperty,
            SearchOperation.Equals, originalChoiceId);
    }

    [TestMethod]
    public async Task Resync_SameEffectKey_UpdatesThePersistedEffectValue()
    {
        var resourceService = _sp.GetRequiredService<IResourceService>();
        var propertyService = _sp.GetRequiredService<ICustomPropertyService>();
        var propertyValueService = _sp.GetRequiredService<ICustomPropertyValueService>();
        var pathMarkService = _sp.GetRequiredService<IPathMarkService>();
        var effectService = _sp.GetRequiredService<IPathMarkEffectService>();

        var resourcePath = Path.Combine(_testRoot, "Movie");
        Directory.CreateDirectory(resourcePath);
        await resourceService.AddOrPutRange([new Resource {Path = resourcePath, IsFile = false}]);
        var resource = (await resourceService.GetAll()).Should().ContainSingle().Subject;

        var property = await AddProperty("Mutable mark value", PropertyType.SingleChoice);
        var mark = await AddPropertyMark(property.Id, "Before");

        await SyncPendingPropertyMarks();

        var initialEffect = (await effectService.GetPropertyEffectsByMarkId(mark.Id))
            .Should().ContainSingle().Subject;
        initialEffect.Value.Should().Be("Before");

        var markToUpdate = (await pathMarkService.GetAll()).Single(m => m.Id == mark.Id);
        markToUpdate.ConfigJson = BuildPropertyMarkConfig(property.Id, "After");
        markToUpdate.Priority = 75;
        await pathMarkService.Update(markToUpdate);

        await SyncPendingPropertyMarks();

        var updatedEffect = (await effectService.GetPropertyEffectsByMarkId(mark.Id))
            .Should().ContainSingle("the same effect key must be updated, not left stale or duplicated")
            .Subject;
        updatedEffect.Value.Should().Be("After");
        updatedEffect.Priority.Should().Be(75,
            "priority is part of the effect data and must be refreshed together with its value");

        var refreshedProperty = await propertyService.GetByKey(property.Id);
        var options = refreshedProperty.Options.Should().BeOfType<SingleChoicePropertyOptions>().Subject;
        var afterChoiceId = options.Choices.Should().ContainSingle(c => c.Label == "After").Which.Value;

        var stored = (await propertyValueService.GetAllDbModels(v =>
                v.ResourceId == resource.Id && v.PropertyId == property.Id))
            .Should().ContainSingle().Subject;
        stored.Value.Should().Be(afterChoiceId);

        var typedDbValue = stored.Value!.DeserializeAsStandardValue(
            PropertySystem.Property.GetDbValueType(PropertyType.SingleChoice));
        PropertySystem.Property.ToBizValue(refreshedProperty.ToProperty(), typedDbValue)
            .Should().Be("After");
    }

    [TestMethod]
    public async Task PartialSync_GuidShapedBusinessLabel_RemainsALabel_WhenReExtractionWorksOrIsAmbiguous()
    {
        var resourceService = _sp.GetRequiredService<IResourceService>();
        var propertyService = _sp.GetRequiredService<ICustomPropertyService>();
        var propertyValueService = _sp.GetRequiredService<ICustomPropertyValueService>();
        var pathMarkService = _sp.GetRequiredService<IPathMarkService>();
        var effectService = _sp.GetRequiredService<IPathMarkEffectService>();

        var resourcePath = Path.Combine(_testRoot, "Movie");
        Directory.CreateDirectory(resourcePath);
        await resourceService.AddOrPutRange([new Resource {Path = resourcePath, IsFile = false}]);
        var resource = (await resourceService.GetAll()).Should().ContainSingle().Subject;

        // A GUID is a perfectly valid user-facing label.  Legacy-value detection must not
        // classify it as a DB reference merely because it has the same lexical shape.
        var guidLabel = Guid.NewGuid().ToString();
        var property = await AddProperty("GUID-shaped label", PropertyType.SingleChoice);
        var mark = await AddPropertyMark(property.Id, guidLabel);

        await SyncPendingPropertyMarks();

        var initiallyRefreshedProperty = await propertyService.GetByKey(property.Id);
        var initialOptions = initiallyRefreshedProperty.Options.Should()
            .BeOfType<SingleChoicePropertyOptions>().Subject;
        var choice = initialOptions.Choices.Should()
            .ContainSingle(c => c.Label == guidLabel).Which;
        choice.Value.Should().NotBe(guidLabel,
            "the GUID-shaped text is the business label, not a caller-supplied DB id");

        var initialEffect = (await effectService.GetPropertyEffectsByMarkId(mark.Id))
            .Should().ContainSingle().Subject;
        initialEffect.Value.Should().Be(guidLabel);

        // The mark is now context-only.  An unrelated sync exercises the compatibility
        // classifier without recollecting this fixed value as a pending mark.
        var triggerProperty = await AddProperty("Partial-sync trigger", PropertyType.SingleLineText);
        await AddPropertyMark(triggerProperty.Id, "touch");
        await SyncPendingPropertyMarks();

        var finallyRefreshedProperty = await propertyService.GetByKey(property.Id);
        var finalOptions = finallyRefreshedProperty.Options.Should()
            .BeOfType<SingleChoicePropertyOptions>().Subject;
        finalOptions.Choices.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(choice);

        var finalEffect = (await effectService.GetPropertyEffectsByMarkId(mark.Id))
            .Should().ContainSingle().Subject;
        finalEffect.Value.Should().Be(guidLabel);

        var stored = (await propertyValueService.GetAllDbModels(v =>
                v.ResourceId == resource.Id && v.PropertyId == property.Id))
            .Should().ContainSingle().Subject;
        stored.Value.Should().Be(choice.Value);

        var reloadedResource = await resourceService.Get(resource.Id, ResourceAdditionalItem.Properties);
        var reloadedValue = reloadedResource!.Properties![(int) PropertyPool.Custom][property.Id]
            .Values!.Should().ContainSingle(v => v.Scope == (int) PropertyValueScope.Synchronization).Subject;
        reloadedValue.BizValue.Should().Be(guidLabel);

        // Now remove the independent path/config evidence and create the exact lexical
        // ambiguity that a DB -> Biz -> DB round-trip cannot solve: the legitimate label
        // is also another choice's id. A later partial sync must preserve, not guess.
        var ambiguousProperty = await propertyService.GetByKey(property.Id);
        var ambiguousOptions = ambiguousProperty.Options.Should()
            .BeOfType<SingleChoicePropertyOptions>().Subject;
        ambiguousOptions.Choices!.Add(new ChoiceOptions {Label = "Decoy", Value = guidLabel});
        await propertyService.Put(ambiguousProperty);

        var ambiguousMark = (await pathMarkService.Get(mark.Id))!;
        ambiguousMark.ConfigJson = "{";
        await pathMarkService.Update(ambiguousMark);
        await pathMarkService.MarkAsSynced(mark.Id);

        var secondTriggerProperty = await AddProperty("Second partial-sync trigger", PropertyType.SingleLineText);
        await AddPropertyMark(secondTriggerProperty.Id, "touch again");
        await SyncPendingPropertyMarks();

        var preservedEffect = (await effectService.GetPropertyEffectsByMarkId(mark.Id))
            .Should().ContainSingle().Subject;
        preservedEffect.Value.Should().Be(guidLabel);

        var preservedValue = (await propertyValueService.GetAllDbModels(v =>
                v.ResourceId == resource.Id && v.PropertyId == property.Id))
            .Should().ContainSingle().Subject;
        preservedValue.Value.Should().Be(choice.Value,
            "an ambiguous business label must not be reinterpreted as the decoy choice id");

        var preservedResource = await resourceService.Get(resource.Id, ResourceAdditionalItem.Properties);
        var preservedResourceValue = preservedResource!.Properties![(int) PropertyPool.Custom][property.Id]
            .Values!.Should().ContainSingle(v => v.Scope == (int) PropertyValueScope.Synchronization).Subject;
        preservedResourceValue.BizValue.Should().Be(guidLabel);
    }

    private async Task<CustomProperty> AddProperty(string name, PropertyType type, object? options = null)
    {
        return await _sp.GetRequiredService<ICustomPropertyService>().Add(new CustomPropertyAddOrPutDto
        {
            Name = name,
            Type = type,
            Options = options == null ? null : JsonConvert.SerializeObject(options)
        });
    }

    private async Task<PathMark> AddPropertyMark(int propertyId, string fixedValue)
    {
        return await _sp.GetRequiredService<IPathMarkService>().Add(new PathMark
        {
            Path = _testRoot,
            Type = PathMarkType.Property,
            ConfigJson = BuildPropertyMarkConfig(propertyId, fixedValue),
            Priority = 50
        });
    }

    private async Task<PathMark> AddLayerPropertyMark(int propertyId)
    {
        return await _sp.GetRequiredService<IPathMarkService>().Add(new PathMark
        {
            Path = _testRoot,
            Type = PathMarkType.Property,
            ConfigJson = JsonConvert.SerializeObject(new PropertyMarkConfig
            {
                MatchMode = PathMatchMode.Layer,
                Layer = 1,
                Pool = PropertyPool.Custom,
                PropertyId = propertyId,
                ValueType = PropertyValueType.Dynamic,
                ValueLayer = 1,
                ApplyScope = PathMarkApplyScope.MatchedOnly
            }),
            Priority = 50
        });
    }

    private string BuildPropertyMarkConfig(int propertyId, string fixedValue)
    {
        return JsonConvert.SerializeObject(new PropertyMarkConfig
        {
            MatchMode = PathMatchMode.Layer,
            Layer = 1,
            Pool = PropertyPool.Custom,
            PropertyId = propertyId,
            ValueType = PropertyValueType.Fixed,
            FixedValue = fixedValue,
            ApplyScope = PathMarkApplyScope.MatchedOnly
        });
    }

    private async Task SyncPendingPropertyMarks()
    {
        await _sp.GetRequiredService<PathMarkSyncService>().SyncMarks(
            null,
            null,
            new PauseToken(),
            CancellationToken.None);
    }

    private async Task RebuildSearchIndex()
    {
        var index = _sp.GetRequiredService<IResourceSearchIndexService>();
        await index.RebuildAllAsync(CancellationToken.None);
        await index.WaitForReadyAsync(TimeSpan.FromSeconds(10));
    }

    private async Task AssertReferenceSearchMatches(
        int expectedResourceId,
        CustomProperty property,
        SearchOperation operation,
        object dbValue)
    {
        var result = await _sp.GetRequiredService<IResourceService>().Search(new ResourceSearch
        {
            PageSize = 100,
            Group = new ResourceSearchFilterGroup
            {
                Combinator = SearchCombinator.And,
                Filters =
                [
                    new ResourceSearchFilter
                    {
                        PropertyPool = PropertyPool.Custom,
                        PropertyId = property.Id,
                        Operation = operation,
                        DbValue = dbValue,
                        Property = property.ToProperty()
                    }
                ]
            }
        });
        result.Data.Should().ContainSingle(r => r.Id == expectedResourceId);
    }
}
