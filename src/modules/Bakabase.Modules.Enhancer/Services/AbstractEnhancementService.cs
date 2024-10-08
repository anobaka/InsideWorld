﻿using System.Linq.Expressions;
using Bakabase.Abstractions.Extensions;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bakabase.Modules.Enhancer.Abstractions.Components;
using Bakabase.Modules.Enhancer.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.Enhancer.Abstractions.Services;
using Bakabase.Modules.Enhancer.Components;
using Bakabase.Modules.Enhancer.Extensions;
using Bakabase.Modules.Property.Abstractions.Components;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.StandardValue.Abstractions.Components;
using Bakabase.Modules.StandardValue.Extensions;
using Bootstrap.Components.Miscellaneous.ResponseBuilders;
using Bootstrap.Components.Orm.Infrastructures;
using Bootstrap.Extensions;
using Bootstrap.Models.ResponseModels;
using Microsoft.EntityFrameworkCore;

namespace Bakabase.Modules.Enhancer.Services
{
    public abstract class AbstractEnhancementService<TDbContext>(IServiceProvider serviceProvider)
        : ResourceService<TDbContext, Bakabase.Abstractions.Models.Db.Enhancement, int>(serviceProvider),
            IEnhancementService where TDbContext : DbContext
    {
        protected ICustomPropertyValueService CustomPropertyValueService =>
            GetRequiredService<ICustomPropertyValueService>();

        protected IReservedPropertyValueService ReservedPropertyValueService =>
            GetRequiredService<IReservedPropertyValueService>();

        protected IEnhancerDescriptors EnhancerDescriptors => GetRequiredService<IEnhancerDescriptors>();

        public async Task<List<Enhancement>> GetAll(
            Expression<Func<Bakabase.Abstractions.Models.Db.Enhancement, bool>>? exp,
            EnhancementAdditionalItem additionalItem = EnhancementAdditionalItem.None)
        {
            var data = await base.GetAll(exp);
            var doModels = data.Select(d => d.ToDomainModel()).ToList();
            await Populate(doModels, additionalItem);
            return doModels;
        }

        protected async Task Populate(List<Enhancement> data, EnhancementAdditionalItem additionalItem)
        {
            foreach (var ai in SpecificEnumUtils<EnhancementAdditionalItem>.Values)
            {
                if (additionalItem.HasFlag(ai))
                {
                    switch (ai)
                    {
                        case EnhancementAdditionalItem.None:
                            break;
                        case EnhancementAdditionalItem.GeneratedPropertyValue:
                        {
                            var reservedPropertyEnhancements =
                                data.Where(d => d.PropertyPool == PropertyPool.Reserved).ToList();
                            if (reservedPropertyEnhancements.Any())
                            {
                                var builtinPropertyMap = GetRequiredService<BuiltinPropertyMap>();
                                var scopes = data
                                    .Select(d => EnhancerDescriptors.TryGet(d.EnhancerId)?.PropertyValueScope)
                                    .ToHashSet();
                                var reservedValueMap =
                                    (await ReservedPropertyValueService.GetAll(v => scopes.Contains(v.Scope)))
                                    .GroupBy(d => d.Scope)
                                    .ToDictionary(d => d.Key,
                                        d => d.GroupBy(x => x.ResourceId).ToDictionary(x => x.Key, x => x.First()));
                                foreach (var d in reservedPropertyEnhancements)
                                {
                                    d.ReservedPropertyValue = reservedValueMap.GetValueOrDefault(
                                            EnhancerDescriptors.TryGet(d.EnhancerId)?.PropertyValueScope ?? 0)
                                        ?.GetValueOrDefault(d.ResourceId);
                                    d.Property =
                                        builtinPropertyMap.GetValueOrDefault((ResourceProperty) d.PropertyId!.Value);
                                }
                            }

                            var customPropertyEnhancements =
                                data.Where(d => d.PropertyPool == PropertyPool.Custom).ToList();
                            if (customPropertyEnhancements.Any())
                            {
                                var scopes = data.Select(d =>
                                    EnhancerDescriptors.TryGet(d.EnhancerId)?.PropertyValueScope);
                                var scopedValueMap = (await CustomPropertyValueService.GetAll(
                                        x => scopes.Contains(x.Scope),
                                        CustomPropertyValueAdditionalItem.BizValue, false)).GroupBy(d => d.Scope)
                                    .ToDictionary(d => d.Key,
                                        d => d.GroupBy(x => x.PropertyId).ToDictionary(x => x.Key,
                                            x => x.GroupBy(y => y.ResourceId)
                                                .ToDictionary(z => z.Key, z => z.First())));
                                foreach (var d in customPropertyEnhancements)
                                {
                                    var scope = EnhancerDescriptors.TryGet(d.EnhancerId)?.PropertyValueScope;
                                    if (scope.HasValue)
                                    {
                                        d.CustomPropertyValue = scopedValueMap.GetValueOrDefault((int) scope.Value)
                                            ?.GetValueOrDefault(d.PropertyId!.Value)?.GetValueOrDefault(d.ResourceId);
                                        d.Property = d.CustomPropertyValue?.Property?.ToProperty();
                                    }
                                }
                            }

                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
            }
        }

        public async Task AddRange(List<Enhancement> enhancements)
        {
            var dbValuesMap = enhancements.ToDictionary(e => e.ToDbModel(), e => e);
            await base.AddRange(dbValuesMap.Keys.ToList());
            foreach (var (k, v) in dbValuesMap)
            {
                v.Id = k.Id;
            }
        }

        public async Task UpdateRange(List<Enhancement> enhancements)
        {
            var dbValues = enhancements.Select(e => e.ToDbModel());
            await base.UpdateRange(dbValues);
        }

        public async Task<BaseResponse> RemoveAll(
            Expression<Func<Bakabase.Abstractions.Models.Db.Enhancement, bool>> selector,
            bool removeGeneratedCustomPropertyValues)
        {
            var enhancements = await base.GetAll(selector);
            var keys = enhancements
                .Select(e => (Scope: EnhancerDescriptors.TryGet(e.EnhancerId)?.PropertyValueScope,
                    e.ResourceId)).Where(x => x.Scope.HasValue).ToList();
            var scopes = keys.Select(d => d.Scope).ToHashSet();
            var resourceIds = keys.Select(d => d.ResourceId).ToHashSet();
            await using var tran = await DbContext.Database.BeginTransactionAsync();
            await base.RemoveRange(enhancements);
            if (removeGeneratedCustomPropertyValues)
            {
                // avoid query entire table
                var cpValues = await CustomPropertyValueService.GetAllDbModels(x =>
                    scopes.Contains(x.Scope) && resourceIds.Contains(x.ResourceId));
                cpValues = cpValues.Where(v => keys.Any(k => k.Scope == v.Scope && k.ResourceId == v.ResourceId))
                    .ToList();
                await CustomPropertyValueService.RemoveRange(cpValues);
            }

            await tran.CommitAsync();
            return BaseResponseBuilder.Ok;
        }
    }
}