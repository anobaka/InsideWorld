﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Configuration;
using Bakabase.Abstractions.Components.CustomProperty;
using Bakabase.Abstractions.Extensions;
using Bakabase.Abstractions.Models.Db;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Dto;
using Bakabase.InsideWorld.Business.Components.Conversion;
using Bakabase.InsideWorld.Business.Components.Migration;
using Bakabase.InsideWorld.Business.Components.StandardValue.Abstractions;
using Bakabase.InsideWorld.Business.Extensions;
using Bakabase.InsideWorld.Business.Models.Domain;
using Bakabase.InsideWorld.Business.Models.Input;
using Bakabase.InsideWorld.Business.Models.View;
using Bakabase.InsideWorld.Models.Configs;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Extensions;
using Bakabase.InsideWorld.Models.Models.Dtos;
using Bakabase.InsideWorld.Models.Models.Entities;
using Bakabase.Modules.CustomProperty.Extensions;
using Bakabase.Modules.CustomProperty.Helpers;
using Bakabase.Modules.CustomProperty.Properties.Boolean;
using Bakabase.Modules.CustomProperty.Properties.Choice;
using Bakabase.Modules.CustomProperty.Properties.DateTime;
using Bakabase.Modules.CustomProperty.Properties.Number;
using Bakabase.Modules.CustomProperty.Properties.Text;
using Bakabase.Modules.CustomProperty.Properties.Time;
using Bootstrap.Components.Configuration.Abstractions;
using Bootstrap.Components.Miscellaneous.ResponseBuilders;
using Bootstrap.Extensions;
using Bootstrap.Models.ResponseModels;
using static Bakabase.InsideWorld.Business.Components.ThirdParty.Pixiv.Models.PixivSearchResponse;
using CustomPropertyValue = Bakabase.Abstractions.Models.Domain.CustomPropertyValue;

namespace Bakabase.InsideWorld.Business.Services
{
    /// <summary>
    /// todo: add the automatic migrations
    /// </summary>
    public class MigrationService
    {
        private readonly CustomResourcePropertyService _customResourcePropertyService;
        private readonly CustomPropertyService _customPropertyService;
        private readonly CustomPropertyValueService _customPropertyValueService;
        private readonly ConversionService _conversionService;
        private readonly IBOptionsManager<MigrationOptions> _migrationOptions;
        private readonly PropertyValueConverter _propertyValueConverter;
        private readonly V190Migrator _v190Migrator;
        private readonly Dictionary<StandardValueType, IStandardValueHandler> _valueConverters;

        public MigrationService(CustomResourcePropertyService customResourcePropertyService,
            CustomPropertyService customPropertyService,
            CustomPropertyValueService customPropertyValueService,
            ConversionService conversionService, IBOptionsManager<MigrationOptions> migrationOptions,
            PropertyValueConverter propertyValueConverter, V190Migrator v190Migrator, IEnumerable<IStandardValueHandler> valueConverters)
        {
            _customResourcePropertyService = customResourcePropertyService;
            _customPropertyService = customPropertyService;
            _customPropertyValueService = customPropertyValueService;
            _conversionService = conversionService;
            _migrationOptions = migrationOptions;
            _propertyValueConverter = propertyValueConverter;
            _v190Migrator = v190Migrator;
            _valueConverters = valueConverters.ToDictionary(d => d.Type, d => d);
        }

        public async Task<List<MigrationTargetViewModel>> GetMigrationTargets()
        {
            var targets = new List<MigrationTargetViewModel>();
            var doneProperties = _migrationOptions.Value?.Properties;
            var toStringForListString = (Func<object, string>?) (s => string.Join(InternalOptions.TextSeparator, (s as IEnumerable<string>)!));
            var propertyAndKeys =
                new List<(ResourceProperty Property, string? PropertyKey, Func<object, string>? BuildValueLabel)>
                {
                    (ResourceProperty.Publisher, null,toStringForListString),
                    (ResourceProperty.Name, null, null),
                    (ResourceProperty.Introduction, null, null),
                    (ResourceProperty.Rate, null, null),
                    (ResourceProperty.Language, null, null),
                    (ResourceProperty.ReleaseDt, null, null),
                    (ResourceProperty.Original, null,toStringForListString),
                    (ResourceProperty.Volume, nameof(VolumeDto.Index), null),
                    (ResourceProperty.Volume, nameof(VolumeDto.Title), null),
                    (ResourceProperty.Volume, nameof(VolumeDto.Name), null),
                    (ResourceProperty.Series, null, null),
                    (ResourceProperty.Favorites, null, null),
                };

            var customPropertyValueKeys = await _customResourcePropertyService.GetAllKeys();
            propertyAndKeys.AddRange(
                customPropertyValueKeys.Select(k => (Property: ResourceProperty.CustomProperty, Key: (string?) k,
                    BuildValueLabel: toStringForListString)));

            foreach (var (property, propertyKey, buildValueLabel) in propertyAndKeys)
            {
                if (doneProperties?.Any(x => x.Property == property && x.PropertyKey == propertyKey) != true)
                {
                    var (fromType, resourceRawValues) =
                        await _v190Migrator.PreparePropertyValuesForObsoleteProperties(property, propertyKey);
                    var target = await _buildSimpleMigrationTargetViewModel(resourceRawValues.Values.ToList()!,
                        property,
                        propertyKey, buildValueLabel ?? (s => s.ToString()!));
                    targets.Add(target);
                }
            }

            _mergeByProperty(targets);
            return targets.Where(t => t.DataCount > 0).ToList();
        }

        private void _mergeByProperty(List<MigrationTargetViewModel> targets)
        {
            foreach (var p in SpecificEnumUtils<ResourceProperty>.Values)
            {
                var pts = targets.Where(t => t.Property == p).ToList();
                if (pts.Count > 1)
                {
                    var index = targets.IndexOf(pts[0]);
                    targets.RemoveAll(pts.Contains);
                    var volumeTarget = new MigrationTargetViewModel
                    {
                        DataCount = pts.Sum(s => s.DataCount),
                        Property = p,
                        PropertyKey = null,
                        SubTargets = pts
                    };
                    targets.Insert(index, volumeTarget);
                }
            }
        }

        private async Task<MigrationTargetViewModel> _buildSimpleMigrationTargetViewModel<T>(List<T> data,
            ResourceProperty property, string? propertyKey, Func<T, string> toString)
        {
            var distinctData = data.Distinct().ToArray();
            var model = new MigrationTargetViewModel
            {
                Data = distinctData,
                DataCount = distinctData.Length,
                Property = property,
                PropertyKey = propertyKey,
                TargetCandidates = []
            };

            var valueType = V190Migrator.GetStandardValueTypeForObsoleteProperties(property, propertyKey);
            var candidateTargetTypes = _valueConverters[valueType].DefaultConversionLoss
                .Where(c => c.Value?.HasFlag(StandardValueConversionLoss.All) != true).Select(c => c.Key)
                .ToArray();

            foreach (var tc in candidateTargetTypes)
            {
                var lossData = new Dictionary<StandardValueConversionLoss, List<string>>();
                foreach (var d in data)
                {
                    var (nv, loss) = await _conversionService.CheckConversionLoss(d, valueType, tc);
                    if (loss.HasValue)
                    {
                        foreach (var f in loss.Value.GetFlags())
                        {
                            lossData.GetOrAdd(f, () => []).Add(toString(d));
                        }
                    }
                }

                var candidate = new MigrationTargetViewModel.PropertyTypeCandidate
                {
                    Type = tc.ToCustomValueType(),
                    LossData = lossData.ToDictionary(s => (int) s.Key, s => s.Value.Distinct().ToList())
                };

                model.TargetCandidates.Add(candidate);
            }

            return model;
        }

        public async Task<BaseResponse> ApplyMigration(MigrationTargetApplyInputModel target)
        {
            var targetProperty = await _customPropertyService.GetByKey(target.TargetPropertyId!.Value);
            var currentPropertyValues =
                (await _customPropertyValueService.GetAll(x => x.PropertyId == targetProperty.Id))
                .Where(x => !string.IsNullOrEmpty(x.Value)).ToDictionary(x => x.ResourceId, x => x);

            var (fromType, resourceRawValues) =
                await _v190Migrator.PreparePropertyValuesForObsoleteProperties(target.Property, target.PropertyKey);

            var noValueResourceIds = resourceRawValues.Keys.Except(currentPropertyValues.Keys).ToHashSet();
            var newValues = new Dictionary<int, CustomPropertyValue>();
            foreach (var (rid, rv) in resourceRawValues)
            {
                if (noValueResourceIds.Contains(rid))
                {
                    var nv = await _propertyValueConverter.Convert(fromType, rv, targetProperty);
                    newValues[rid] = nv;
                }
            }

            await _customPropertyService.Update(targetProperty.ToDbModel()!);
            await _customPropertyValueService.AddRange(newValues.Values);

            await _migrationOptions.SaveAsync(m =>
            {
                if (m.Properties?.Any(x => x.Property == target.Property && x.PropertyKey == target.PropertyKey) !=
                    true)
                {
                    (m.Properties ??= []).Add(new MigrationOptions.PropertyMigrationOptions
                        {PropertyKey = target.PropertyKey, Property = target.Property});
                }
            });

            return BaseResponseBuilder.Ok;
        }
    }
}