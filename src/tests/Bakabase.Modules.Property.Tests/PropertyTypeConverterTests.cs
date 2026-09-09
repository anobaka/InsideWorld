using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Dto;
using Bakabase.Modules.Property.Abstractions.Components;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.TestKit.Utils;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using DomainProperty = Bakabase.Abstractions.Models.Domain.Property;

namespace Bakabase.Modules.Property.Tests;

/// <summary>
/// Direct coverage for <see cref="IPropertyTypeConverter"/> — the component that wraps
/// StandardValue conversion for property-type changes. Exercises single-value conversion,
/// the preview path (which records only values whose display actually changes), and batch
/// conversion. Reference-type option mutation is already covered by ChoiceConversion /
/// TagAndMultilevelConversion, so these tests focus on the converter's own contract.
/// </summary>
[TestClass]
public sealed class PropertyTypeConverterTests
{
    private static IPropertyTypeConverter _converter = null!;
    private static ICustomPropertyService _customProperties = null!;

    [ClassInitialize]
    public static async Task ClassInit(TestContext _)
    {
        var sp = await TestServiceBuilder.BuildServiceProvider();
        _converter = sp.GetRequiredService<IPropertyTypeConverter>();
        _customProperties = sp.GetRequiredService<ICustomPropertyService>();
    }

    private static DomainProperty Prop(PropertyType type) => new(PropertyPool.Custom, 1, type);

    [TestMethod]
    public async Task ConvertValues_TextToIgnoreCaseChoice_ReusesFirstLabelAcrossBatch()
    {
        var options = new SingleChoicePropertyOptions { IgnoreCase = true };
        var target = new DomainProperty(PropertyPool.Custom, 2, PropertyType.SingleChoice, Options: options);
        var result = await _converter.ConvertValuesAsync(Prop(PropertyType.SingleLineText), target,
            new object?[] { "Title", "TITLE", "title" });

        Assert.AreEqual(1, options.Choices!.Count);
        Assert.AreEqual("Title", options.Choices[0].Label);
        Assert.IsTrue(result.NewDbValues.All(v => Equals(v, options.Choices[0].Value)));
    }

    [TestMethod]
    public async Task SaveIgnoreCaseOptions_PreservesOldReferenceIdsAndFoldsNewEditorDuplicates()
    {
        var options = new SingleChoicePropertyOptions
        {
            Choices = [new() { Label = "Title", Value = "old1" }, new() { Label = "title", Value = "old2" }]
        };
        var created = await _customProperties.Add(new CustomPropertyAddOrPutDto
        {
            Name = "Case-sensitive existing choices",
            Type = PropertyType.SingleChoice,
            Options = JsonConvert.SerializeObject(options)
        });
        options.IgnoreCase = true;
        options.Choices.Add(new() { Label = "TITLE", Value = "new" });
        options.DefaultValue = "new";
        var saved = await _customProperties.Put(created.Id, new CustomPropertyAddOrPutDto
        {
            Name = created.Name,
            Type = created.Type,
            Options = JsonConvert.SerializeObject(options)
        });
        var savedOptions = (SingleChoicePropertyOptions) saved.Options!;
        CollectionAssert.AreEqual(new[] { "old1", "old2" }, savedOptions.Choices!.Select(c => c.Value).ToArray());
        Assert.AreEqual("old1", savedOptions.DefaultValue);
        Assert.IsTrue(savedOptions.IgnoreCase);
    }

    [TestMethod]
    public async Task AddIgnoreCaseOptions_FoldsNewEditorDuplicates()
    {
        var created = await _customProperties.Add(new CustomPropertyAddOrPutDto
        {
            Name = "New case-insensitive choices",
            Type = PropertyType.SingleChoice,
            Options = JsonConvert.SerializeObject(new SingleChoicePropertyOptions
            {
                IgnoreCase = true,
                Choices = [new() { Label = "Title", Value = "first" }, new() { Label = "TITLE", Value = "second" }]
            })
        });
        var savedOptions = (SingleChoicePropertyOptions) created.Options!;
        Assert.AreEqual("Title", savedOptions.Choices!.Single().Label);
        Assert.AreEqual("first", savedOptions.Choices[0].Value);
    }

    [TestMethod]
    public async Task PreviewConversion_UsesSameFirstLabelAsIgnoreCaseTypeChange()
    {
        var source = new DomainProperty(PropertyPool.Custom, 1, PropertyType.SingleChoice,
            Options: new SingleChoicePropertyOptions
            {
                IgnoreCase = true,
                Choices = [new() { Label = "Title", Value = "first" }, new() { Label = "TITLE", Value = "second" }]
            });
        var preview = await _converter.PreviewConversionAsync(source, PropertyType.MultipleChoice,
            new object?[] { "first", "second" });
        Assert.AreEqual(1, preview.Changes.Count);
        Assert.AreEqual("TITLE", preview.Changes[0].FromDisplay);
        Assert.AreEqual("Title", preview.Changes[0].ToDisplay);
    }

    #region ConvertValueAsync

    [TestMethod]
    public async Task ConvertValue_NumberToText()
    {
        var result = await _converter.ConvertValueAsync(
            Prop(PropertyType.Number), Prop(PropertyType.SingleLineText), 123m);
        Assert.AreEqual("123", result.NewDbValue);
    }

    [TestMethod]
    public async Task ConvertValue_TextToNumber()
    {
        var result = await _converter.ConvertValueAsync(
            Prop(PropertyType.SingleLineText), Prop(PropertyType.Number), "42");
        Assert.AreEqual(42m, result.NewDbValue);
    }

    [TestMethod]
    public async Task ConvertValue_TextToNumber_NonNumeric_YieldsNull()
    {
        var result = await _converter.ConvertValueAsync(
            Prop(PropertyType.SingleLineText), Prop(PropertyType.Number), "not a number");
        Assert.IsNull(result.NewDbValue);
    }

    [TestMethod]
    public async Task ConvertValue_BooleanToText()
    {
        // Boolean -> String conversion yields "1" / "0".
        var trueResult = await _converter.ConvertValueAsync(
            Prop(PropertyType.Boolean), Prop(PropertyType.SingleLineText), true);
        Assert.AreEqual("1", trueResult.NewDbValue);

        var falseResult = await _converter.ConvertValueAsync(
            Prop(PropertyType.Boolean), Prop(PropertyType.SingleLineText), false);
        Assert.AreEqual("0", falseResult.NewDbValue);
    }

    [TestMethod]
    public async Task ConvertValue_NullDbValue_YieldsNull()
    {
        var result = await _converter.ConvertValueAsync(
            Prop(PropertyType.Number), Prop(PropertyType.SingleLineText), null);
        Assert.IsNull(result.NewDbValue);
    }

    [TestMethod]
    public async Task ConvertValue_SameType_PreservesValue()
    {
        var result = await _converter.ConvertValueAsync(
            Prop(PropertyType.SingleLineText), Prop(PropertyType.SingleLineText), "hello");
        Assert.AreEqual("hello", result.NewDbValue);
    }

    #endregion

    #region PreviewConversionAsync

    [TestMethod]
    public async Task Preview_CountsEveryValue()
    {
        var preview = await _converter.PreviewConversionAsync(
            Prop(PropertyType.Number), PropertyType.SingleLineText, new object?[] { 1m, 2m, 3m });
        Assert.AreEqual(3, preview.TotalCount);
    }

    [TestMethod]
    public async Task Preview_RecordsOnlyValuesWhoseDisplayChanges()
    {
        // "not a number" -> Number is lossy (becomes null) so its display changes and is
        // recorded; "123" -> Number keeps the same display and is not recorded.
        var preview = await _converter.PreviewConversionAsync(
            Prop(PropertyType.SingleLineText), PropertyType.Number, new object?[] { "not a number", "123" });
        Assert.AreEqual(2, preview.TotalCount);
        Assert.AreEqual(1, preview.Changes.Count);
    }

    [TestMethod]
    public async Task Preview_ReportsSourceAndTargetBizTypes()
    {
        var preview = await _converter.PreviewConversionAsync(
            Prop(PropertyType.Number), PropertyType.SingleLineText, new object?[] { 1m });
        Assert.AreEqual(StandardValueType.Decimal, preview.FromBizType);
        Assert.AreEqual(StandardValueType.String, preview.ToBizType);
    }

    #endregion

    #region ConvertValuesAsync

    [TestMethod]
    public async Task ConvertValues_Batch_ConvertsEveryValue()
    {
        var result = await _converter.ConvertValuesAsync(
            Prop(PropertyType.Number), Prop(PropertyType.SingleLineText), new object?[] { 1m, 2m, 3m });
        CollectionAssert.AreEqual(new object?[] { "1", "2", "3" }, result.NewDbValues);
    }

    [TestMethod]
    public async Task ConvertValues_EmptyInput_YieldsEmptyResult()
    {
        var result = await _converter.ConvertValuesAsync(
            Prop(PropertyType.Number), Prop(PropertyType.SingleLineText), new object?[] { });
        Assert.AreEqual(0, result.NewDbValues.Count);
        Assert.IsFalse(result.PropertyOptionsChanged);
    }

    [TestMethod]
    public async Task ConvertValues_NonReferenceTypes_ReportNoOptionsChange()
    {
        var result = await _converter.ConvertValuesAsync(
            Prop(PropertyType.Number), Prop(PropertyType.SingleLineText), new object?[] { 1m, 2m });
        Assert.IsFalse(result.PropertyOptionsChanged);
    }

    #endregion
}
