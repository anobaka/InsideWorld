using System.Globalization;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.Property.Abstractions.Components;
using Bakabase.Modules.Property.Components;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.Modules.Property.Components.Properties.Choice.Abstractions;
using Bakabase.Modules.Property.Components.Properties.Multilevel;
using Bakabase.Modules.Property.Components.Properties.Tags;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.StandardValue.Models.Domain;
using Newtonsoft.Json;
using DomainProperty = Bakabase.Abstractions.Models.Domain.Property;

namespace Bakabase.Modules.Property.Tests;

[TestClass]
public sealed class ReferencePropertyIgnoreCaseTests
{
    [DataTestMethod]
    [DataRow(PropertyType.SingleChoice)]
    [DataRow(PropertyType.MultipleChoice)]
    [DataRow(PropertyType.Tags)]
    [DataRow(PropertyType.Multilevel)]
    public void MatchingAndCreation_ReuseFirstOptionAndKeepItsText(PropertyType type)
    {
        var descriptor = PropertySystem.Property.GetDescriptor(type);
        var options = descriptor.InitializeOptions()!;
        ((IReferencePropertyOptions) options).IgnoreCase = true;
        var property = new DomainProperty(PropertyPool.Custom, 1, type, Options: options);

        var first = descriptor.PrepareDbValue(property, BizValue(type, "Series", "Title"));
        var second = descriptor.PrepareDbValue(property, BizValue(type, "series", "title"));
        var matchOnly = descriptor.PrepareDbValue(property, BizValue(type, "SERIES", "TITLE"),
            PropertyValueMatchPolicy.MatchOnly);
        Assert.IsTrue(first.PropertyChanged);
        Assert.IsFalse(second.PropertyChanged);
        Assert.IsFalse(matchOnly.PropertyChanged);
        Assert.AreEqual(JsonConvert.SerializeObject(first.DbValue), JsonConvert.SerializeObject(second.DbValue));
        Assert.AreEqual(JsonConvert.SerializeObject(first.DbValue), JsonConvert.SerializeObject(matchOnly.DbValue));
        Assert.AreEqual(JsonConvert.SerializeObject(BizValue(type, "Series", "Title")),
            JsonConvert.SerializeObject(descriptor.GetBizValue(property, second.DbValue)));
    }

    [DataTestMethod]
    [DataRow(PropertyType.SingleChoice)]
    [DataRow(PropertyType.MultipleChoice)]
    [DataRow(PropertyType.Tags)]
    [DataRow(PropertyType.Multilevel)]
    public void Toggle_PreservesExistingIdsAndLabelsAndMatchesFirst(PropertyType type)
    {
        var descriptor = PropertySystem.Property.GetDescriptor(type);
        var options = descriptor.InitializeOptions()!;
        var property = new DomainProperty(PropertyPool.Custom, 1, type, Options: options);
        Assert.IsFalse(((IReferencePropertyOptions) options).IgnoreCase);
        var first = descriptor.PrepareDbValue(property, BizValue(type, "Series", "Title"));
        var second = descriptor.PrepareDbValue(property, BizValue(type, "series", "title"));
        Assert.AreNotEqual(JsonConvert.SerializeObject(first.DbValue), JsonConvert.SerializeObject(second.DbValue));

        ((IReferencePropertyOptions) options).IgnoreCase = true;
        var before = JsonConvert.SerializeObject(options);
        var latest = descriptor.PrepareDbValue(property, BizValue(type, "SERIES", "TITLE"));
        Assert.IsFalse(latest.PropertyChanged);
        Assert.AreEqual(JsonConvert.SerializeObject(first.DbValue), JsonConvert.SerializeObject(latest.DbValue));
        Assert.AreEqual(before, JsonConvert.SerializeObject(options));
        Assert.AreEqual(JsonConvert.SerializeObject(BizValue(type, "series", "title")),
            JsonConvert.SerializeObject(descriptor.GetBizValue(property, second.DbValue)));
    }

    [DataTestMethod]
    [DataRow(PropertyType.MultipleChoice)]
    [DataRow(PropertyType.Tags)]
    [DataRow(PropertyType.Multilevel)]
    public void InputVariants_AreStoredAsOneReference(PropertyType type)
    {
        var descriptor = PropertySystem.Property.GetDescriptor(type);
        var options = descriptor.InitializeOptions()!;
        ((IReferencePropertyOptions) options).IgnoreCase = true;
        var property = new DomainProperty(PropertyPool.Custom, 1, type, Options: options);
        object input = type switch
        {
            PropertyType.MultipleChoice => new List<string> { "Title", "TITLE", "title" },
            PropertyType.Tags => new List<TagValue> { new("Series", "Title"), new("SERIES", "TITLE") },
            _ => new List<List<string>> { new() { "Series", "Title" }, new() { "series", "title" } }
        };
        var result = descriptor.PrepareDbValue(property, input);
        Assert.AreEqual(1, ((List<string>) result.DbValue!).Count);
    }

    [TestMethod]
    public void ChoiceOptionsMutation_HonorsIgnoreCaseEvenWhenCallerAllowsDuplicates()
    {
        var options = new SingleChoicePropertyOptions { IgnoreCase = true };
        Assert.IsTrue(options.AddChoices(false, [" Title ", "TITLE", "title"], ["first", "second", "third"]));
        Assert.AreEqual(1, options.Choices!.Count);
        Assert.AreEqual("Title", options.Choices[0].Label);
        Assert.AreEqual("first", options.Choices[0].Value);
    }

    [TestMethod]
    public void TagIdentity_UsesBothGroupAndNameWithoutChangingGroupSpelling()
    {
        var options = new TagsPropertyOptions { IgnoreCase = true };
        var values = PropertyValueFactory.Tags.MatchDbValue(options,
            [new("Artist", "Alice"), new("ARTIST", "ALICE"), new("Author", "Alice")], true)!;
        Assert.AreEqual(2, values.Count);
        Assert.AreEqual("Artist", options.Tags![0].Group);
        Assert.AreEqual("Alice", options.Tags[0].Name);
        Assert.AreEqual("Author", options.Tags[1].Group);
    }

    [TestMethod]
    public void MultilevelIdentity_UsesEverySegmentAndExtendsExistingBranch()
    {
        var options = new MultilevelPropertyOptions { IgnoreCase = true };
        var first = PropertyValueFactory.Multilevel.MatchDbValue(options, [["Series", "Title"]], true)!;
        var values = PropertyValueFactory.Multilevel.MatchDbValue(options,
            [["SERIES", "TITLE"], ["series", "Sequel"], ["Other", "Title"]], true)!;
        Assert.AreEqual(first[0], values[0]);
        Assert.AreEqual(2, options.Data!.Count);
        Assert.AreEqual("Series", options.Data[0].Label);
        Assert.AreEqual("Title", options.Data[0].Children![0].Label);
        Assert.AreEqual("Sequel", options.Data[0].Children![1].Label);
        Assert.AreNotEqual(values[0], values[2]);
    }

    [TestMethod]
    public void MatchOnly_DoesNotAddMissingOptions()
    {
        var options = new SingleChoicePropertyOptions { IgnoreCase = true };
        Assert.IsNull(PropertyValueFactory.SingleChoice.MatchDbValue(options, "New"));
        Assert.IsNull(options.Choices);
    }

    [TestMethod]
    public void Comparison_IsOrdinalAcrossCultures()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var options = new SingleChoicePropertyOptions { IgnoreCase = true };
            var first = PropertyValueFactory.SingleChoice.MatchDbValue(options, "FILE", true);
            Assert.AreEqual(first, PropertyValueFactory.SingleChoice.MatchDbValue(options, "file", true));
            Assert.AreEqual("FILE", options.Choices!.Single().Label);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [TestMethod]
    public void OptionSerialization_PreservesSettingAndOldOptionsDefaultToCaseSensitive()
    {
        Assert.IsFalse(JsonConvert.DeserializeObject<SingleChoicePropertyOptions>("{}")!.IgnoreCase);
        var serialized = JsonConvert.SerializeObject(new TagsPropertyOptions { IgnoreCase = true });
        Assert.IsTrue(JsonConvert.DeserializeObject<TagsPropertyOptions>(serialized)!.IgnoreCase);
    }

    [TestMethod]
    public void OptionEditor_NewDuplicatesUseExistingIdAndDefaultWithoutRemovingOldIds()
    {
        var previous = new SingleChoicePropertyOptions
        {
            Choices = [new() { Label = "Title", Value = "old-1" }, new() { Label = "title", Value = "old-2" }]
        };
        var updated = new SingleChoicePropertyOptions
        {
            IgnoreCase = true,
            Choices = [new() { Label = "TITLE", Value = "new" }, .. previous.Choices],
            DefaultValue = "new"
        };
        ReferencePropertyOptionsNormalizer.Normalize(updated, previous);
        CollectionAssert.AreEqual(new[] { "old-1", "old-2" }, updated.Choices!.Select(c => c.Value).ToArray());
        CollectionAssert.AreEqual(new[] { "Title", "title" }, updated.Choices.Select(c => c.Label).ToArray());
        Assert.AreEqual("old-1", updated.DefaultValue);
    }

    [TestMethod]
    public void OptionImport_FirstNewLabelWinsAndDefaultIsRemapped()
    {
        var options = new MultipleChoicePropertyOptions
        {
            IgnoreCase = true,
            Choices = [new() { Label = "Title", Value = "first" }, new() { Label = "TITLE", Value = "second" }],
            DefaultValue = ["second", "first"]
        };
        ReferencePropertyOptionsNormalizer.Normalize(options);
        Assert.AreEqual("Title", options.Choices!.Single().Label);
        CollectionAssert.AreEqual(new[] { "first" }, options.DefaultValue!);
    }

    [TestMethod]
    public void OptionImport_MergesNewMultilevelBranchesWithoutLosingTheirChildren()
    {
        var options = new MultilevelPropertyOptions
        {
            IgnoreCase = true,
            Data =
            [
                new() { Label = "Series", Value = "p1", Children = [new() { Label = "Title", Value = "c1" }] },
                new() { Label = "SERIES", Value = "p2", Children =
                    [new() { Label = "TITLE", Value = "c2" }, new() { Label = "Sequel", Value = "c3" }] }
            ],
            DefaultValue = ["p2", "c2", "c3"]
        };
        ReferencePropertyOptionsNormalizer.Normalize(options);
        Assert.AreEqual("Series", options.Data!.Single().Label);
        CollectionAssert.AreEqual(new[] { "Title", "Sequel" }, options.Data[0].Children!.Select(c => c.Label).ToArray());
        CollectionAssert.AreEqual(new[] { "p1", "c1", "c3" }, options.DefaultValue!);
    }

    [TestMethod]
    public void OptionEditor_TagsPreserveOldIdsAndOnlyFoldNewGroupAndNameVariants()
    {
        var previous = new TagsPropertyOptions
        {
            Tags = [new("Group", "Name") { Value = "old" }]
        };
        var updated = new TagsPropertyOptions
        {
            IgnoreCase = true,
            Tags = [new("GROUP", "NAME") { Value = "new" }, .. previous.Tags,
                new("Other", "Name") { Value = "other" }]
        };
        ReferencePropertyOptionsNormalizer.Normalize(updated, previous);
        CollectionAssert.AreEqual(new[] { "old", "other" }, updated.Tags!.Select(t => t.Value).ToArray());
        Assert.AreEqual("Group", updated.Tags[0].Group);
    }

    private static object BizValue(PropertyType type, string group, string label) => type switch
    {
        PropertyType.SingleChoice => label,
        PropertyType.MultipleChoice => new List<string> { label },
        PropertyType.Tags => new List<TagValue> { new(group, label) },
        PropertyType.Multilevel => new List<List<string>> { new() { group, label } },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
