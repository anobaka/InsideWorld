using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Dto;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bakabase.Modules.Property.Abstractions.Models.Db;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components.Properties.Choice;
using Bakabase.TestKit.Utils;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Bakabase.Tests;

/// <summary>
/// A stored db value that matches no option must read back as absent on EVERY path, not just
/// inside the descriptor. <see cref="Resource.Property.PropertyValue"/> used to fall back to
/// the raw db value whenever the biz value came back null, which put bare choice ids (and,
/// for Tags / Multilevel, values of the wrong shape) straight into the UI.
/// </summary>
[TestClass]
public class ResourcePropertyValueMissBehaviorTests
{
    [TestMethod]
    public void PropertyValue_NullBizValue_DoesNotFallBackToDbValue()
    {
        var value = new Resource.Property.PropertyValue(
            (int) PropertyValueScope.Synchronization, new List<string> {"uuid-deleted"}, null, null);

        value.Value.Should().BeEquivalentTo(new[] {"uuid-deleted"});
        value.BizValue.Should().BeNull();
        value.AliasAppliedBizValue.Should().BeNull();
    }

    [TestMethod]
    public void PropertyValue_AliasAppliedStillFallsBackToBizValue()
    {
        // Alias application is optional, so this fallback is the one that must stay.
        var value = new Resource.Property.PropertyValue(
            (int) PropertyValueScope.Synchronization, new List<string> {"uuid-a"},
            new List<string> {"A"}, null);

        value.AliasAppliedBizValue.Should().BeEquivalentTo(new[] {"A"});
    }

    /// <summary>
    /// End to end: a MultipleChoice value pointing at a choice the property no longer has must
    /// come back from the resource read with no biz value at all.
    /// </summary>
    [TestMethod]
    public async Task ResourceRead_DeletedChoice_YieldsNoBizValue()
    {
        var sp = await TestServiceBuilder.BuildServiceProvider();
        var customPropertyService = sp.GetRequiredService<ICustomPropertyService>();
        var propertyValueService = sp.GetRequiredService<ICustomPropertyValueService>();
        var resourceService = sp.GetRequiredService<IResourceService>();

        var genre = await customPropertyService.Add(new CustomPropertyAddOrPutDto
        {
            Name = "Genre",
            Type = PropertyType.MultipleChoice,
            Options = JsonConvert.SerializeObject(new MultipleChoicePropertyOptions
            {
                Choices = [new() {Label = "Action", Value = "uuid-action"}]
            })
        });

        await resourceService.AddOrPutRange([
            new Resource {Path = Path.Combine(Path.GetTempPath(), "miss-behavior-probe"), IsFile = false}
        ]);
        var resourceId = (await resourceService.GetAll()).Single().Id;

        await propertyValueService.AddDbModelRange([
            new CustomPropertyValueDbModel
            {
                ResourceId = resourceId,
                PropertyId = genre.Id,
                // Serialized ListString holding a single id the property no longer carries.
                Value = "uuid-deleted",
                Scope = (int) PropertyValueScope.Synchronization
            }
        ]);

        var reloaded = await resourceService.Get(resourceId, ResourceAdditionalItem.Properties);
        var value = reloaded!.Properties![(int) PropertyPool.Custom][genre.Id].Values!.Single();

        value.Value.Should().BeEquivalentTo(new[] {"uuid-deleted"}, "the db value is kept as stored");
        value.BizValue.Should().BeNull("a db value matching no choice must not reach the UI");
        value.AliasAppliedBizValue.Should().BeNull();
    }
}
