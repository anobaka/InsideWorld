﻿using Bakabase.Abstractions.Extensions;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Services;
using Bakabase.Modules.BulkModification.Abstractions.Components;
using Bakabase.Modules.BulkModification.Abstractions.Models;
using Bakabase.Modules.BulkModification.Abstractions.Services;
using Bakabase.Modules.BulkModification.Components;
using Bakabase.Modules.BulkModification.Extensions;
using Bakabase.Modules.BulkModification.Models.Db;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.StandardValue.Abstractions.Configurations;
using Bakabase.Modules.StandardValue.Extensions;
using Bootstrap.Components.Orm.Infrastructures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ResourceDiff = Bakabase.Abstractions.Models.Domain.ResourceDiff;

namespace Bakabase.Modules.BulkModification.Services
{
    public class BulkModificationService<TDbContext>(
        IServiceProvider serviceProvider,
        ResourceService<TDbContext, BulkModificationDiffDbModel, int> diffOrm)
        : ResourceService<TDbContext, BulkModificationDbModel, int>(serviceProvider), IBulkModificationService
        where TDbContext : DbContext, IBulkModificationDbContext
    {
        protected IResourceService ResourceService => GetRequiredService<IResourceService>();
        protected IPropertyService PropertyService => GetRequiredService<IPropertyService>();

        public async Task<Abstractions.Models.BulkModification?> Get(int id)
        {
            var bm = await GetByKey(id);
            if (bm == null)
            {
                return null;
            }

            return await bm.ToDomainModel(GetRequiredService<IPropertyService>());
        }

        public async Task Add(Abstractions.Models.BulkModification bm)
        {
            await Add(bm.ToDbModel());
        }

        public async Task Put(Abstractions.Models.BulkModification bm)
        {
            await Update(bm.ToDbModel());
        }

        public async Task Delete(int id)
        {
            await RemoveByKey(id);
        }

        public async Task Duplicate(int id)
        {
            var bm = await GetByKey(id);
            bm.Id = 0;
            bm.CreatedAt = DateTime.Now;
            await base.Add(bm);
        }

        public async Task Patch(int id, PatchBulkModification model)
        {
            var bm = await Get(id);
            if (bm != null)
            {
                bm.IsActive = model.IsActive ?? bm.IsActive;
                bm.Name = model.Name ?? bm.Name;
                bm.Filter = model.Filter ?? bm.Filter;
                bm.Variables = model.Variables ?? bm.Variables;
                bm.Processes = model.Processes ?? bm.Processes;
                await Put(bm);
            }
        }

        public async Task<List<Abstractions.Models.BulkModification>> GetAll()
        {
            var dbModels = await base.GetAll();
            var domainModels = await dbModels.ToDomainModels(GetRequiredService<IPropertyService>());
            return domainModels.OrderByDescending(x => x.Id).ToList();
        }

        public async Task<int[]> Filter(int id)
        {
            var bm = await Get(id);
            if (bm != null)
            {
                var filter = bm.Filter;
                var resources = await ResourceService.Search(new ResourceSearch
                {
                    Group = filter,
                    PageSize = int.MaxValue
                });
                var resourceIds = resources.Data?.Select(r => r.Id).ToList() ?? [];
                bm.FilteredResourceIds = resourceIds;
                await Put(bm);
                return resourceIds.ToArray();
            }

            return [];
        }

        public async Task Preview(int id)
        {
            var bm = await Get(id);
            if (bm != null)
            {
                bm.FilteredResourceIds ??= (await Filter(id)).ToList();
                if (bm.FilteredResourceIds!.Any() && bm.Processes?.Any() == true)
                {
                    var resources = await ResourceService.GetByKeys(bm.FilteredResourceIds.ToArray());
                    var allDiffs = new List<BulkModificationDiff>();

                    foreach (var resource in resources)
                    {
                        var variableMap = new Dictionary<string, (StandardValueType Type, object? Value)>();
                        if (bm.Variables?.Any() == true)
                        {
                            foreach (var v in bm.Variables)
                            {
                                var rp = resource.Properties?.GetValueOrDefault((int) v.PropertyPool)
                                    ?.GetValueOrDefault(v.PropertyId);
                                var variableValue = rp?.Values?.FirstOrDefault(x => x.Scope == (int) v.Scope)?.BizValue;
                                if (v.Preprocesses?.Any() == true)
                                {
                                    foreach (var preprocess in v.Preprocesses)
                                    {
                                        var processor =
                                            BulkModificationInternals.ProcessorMap[v.Property.Type.GetBizValueType()];
                                        var options =
                                            preprocess.Options?.ConvertToProcessorOptions(variableMap, v.Property);
                                        variableValue = processor.Process(variableValue, preprocess.Operation, options);
                                    }
                                }

                                variableMap[v.Key] = (v.Property.Type.GetBizValueType(), variableValue);
                            }
                        }

                        var copyProperties = resource.Properties?.Copy() ?? [];

                        foreach (var process in bm.Processes)
                        {
                            var processor =
                                BulkModificationInternals.ProcessorMap[process.Property.Type.GetBizValueType()];

                            var currentValue = resource.Properties?.GetPropertyBizValue(process.PropertyPool,
                                process.PropertyId, PropertyValueScope.Manual)?.Value;

                            var newValue = currentValue;
                            if (process.Steps != null)
                            {
                                foreach (var step in process.Steps)
                                {
                                    var options =
                                        step.Options?.ConvertToProcessorOptions(variableMap, process.Property);
                                    newValue = processor.Process(newValue, step.Operation, options);
                                }
                            }

                            copyProperties.SetPropertyBizValue(process.Property, newValue, PropertyValueScope.Manual);
                        }

                        allDiffs.Add(new BulkModificationDiff
                        {
                            ResourceId = resource.Id,
                            Diffs = resource.Properties.Compare(copyProperties),
                            BulkModificationId = bm.Id,
                            ResourcePath = resource.Path
                        });
                    }

                    await diffOrm.RemoveAll(x => x.BulkModificationId == bm.Id);
                    await diffOrm.AddRange(allDiffs.Select(d => d.ToDbModel()));
                }
            }
        }

        public async Task Apply(int id)
        {
            await ApplyOrRevert(id, diff => diff.Value1, diff => diff.Value2);
        }

        public async Task Revert(int id)
        {
            await ApplyOrRevert(id, diff => diff.Value2, diff => diff.Value1);
        }

        private async Task ApplyOrRevert(int id, Func<ResourceDiff, object?> selectExpected,
            Func<ResourceDiff, object?> selectTarget)
        {
            var bm = await Get(id);
            if (bm == null)
            {
                return;
            }

            if (!bm.IsActive)
            {
                throw new Exception("Can't operate on a inactive bulk modification.");
            }

            var dbBmDiffs = await diffOrm.GetAll(x => x.BulkModificationId == id);
            var bmDiffs = await dbBmDiffs.ToDomainModels(PropertyService);
            var resourceIds = dbBmDiffs.Select(r => r.ResourceId).ToList();
            var resourcesMap = (await ResourceService.GetByKeys(resourceIds.ToArray())).ToDictionary(d => d.Id, d => d);
            var propertyMap = await bmDiffs.PreparePropertyMap(PropertyService, true);

            if (bmDiffs.Count != resourcesMap.Count)
            {
                throw new Exception($"Some of resources do not exist anymore, please perform a preview again.");
            }

            foreach (var bmDiff in bmDiffs)
            {
                var resource = resourcesMap[bmDiff.ResourceId];
                if (bmDiff.Diffs.Any())
                {
                    resource.Properties ??= [];
                    foreach (var rDiff in bmDiff.Diffs)
                    {
                        var property = propertyMap[rDiff.PropertyPool][rDiff.PropertyId];
                        var currentValue = resource.Properties.GetPropertyBizValue(rDiff.PropertyPool, rDiff.PropertyId,
                            PropertyValueScope.Manual);

                        var expectedValue = selectExpected(rDiff);

                        var stdHandler = StandardValueInternals.HandlerMap[property.Type.GetBizValueType()];
                        if (!stdHandler.Compare(currentValue, expectedValue))
                        {
                            throw new Exception(
                                $"Validation failed: Current value [{currentValue?.SerializeAsStandardValue(property.Type.GetBizValueType())}] of property {property.Name} of resource {resource.Path} does not match the expected value [{expectedValue?.SerializeAsStandardValue(property.Type.GetBizValueType())}]. Please perform a preview again.");
                        }

                        var targetValue = selectTarget(rDiff);

                        resource.Properties.SetPropertyBizValue(property, targetValue, PropertyValueScope.Manual);
                    }
                }
            }

            await ResourceService.AddOrPutRange(resourcesMap.Values.ToList());
        }
    }
}