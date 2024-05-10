﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Configuration;
using Bakabase.Abstractions.Components.StandardValue;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants;

namespace Bakabase.InsideWorld.Business.Components.StandardValue.ValueHandlers
{
    public class ListStringValueHandler(SpecialTextService specialTextService)
        : AbstractStandardValueHandler<List<string>>
    {
        public override StandardValueType Type => StandardValueType.ListString;

        public override Dictionary<StandardValueType, StandardValueConversionLoss?> DefaultConversionLoss { get; } =
            new()
            {
                {StandardValueType.String, StandardValueConversionLoss.ValuesWillBeMerged},
                {StandardValueType.ListString, null},
                {
                    StandardValueType.Decimal, StandardValueConversionLoss.InconvertibleDataWillBeLost |
                                               StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained
                },
                {StandardValueType.Link, StandardValueConversionLoss.ValuesWillBeMerged},
                {StandardValueType.Boolean, StandardValueConversionLoss.NotEmptyValueWillBeConvertedToTrue},
                {
                    StandardValueType.DateTime,
                    StandardValueConversionLoss.InconvertibleDataWillBeLost |
                    StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained
                },
                {
                    StandardValueType.Time,
                    StandardValueConversionLoss.InconvertibleDataWillBeLost |
                    StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained
                },
                {
                    StandardValueType.ListListString, null
                }
            };

        protected override string? BuildDisplayValue(List<string> value)
        {
            return string.Join(InternalOptions.TextSeparator, value.Where(v => !string.IsNullOrEmpty(v)));
        }

        protected override List<string>? ConvertToTypedValue(object? currentValue)
        {
            if (currentValue is List<string> list)
            {
                list = list.Distinct().Where(s => !string.IsNullOrEmpty(s)).ToList();
                if (list.Any())
                {
                    return list;
                }
            }

            return null;
        }

        public override (string? NewValue, StandardValueConversionLoss? Loss) ConvertToString(
            List<string> currentValue)
        {
            if (currentValue.Count > 1)
            {
                return (string.Join(InternalOptions.TextSeparator, currentValue),
                    StandardValueConversionLoss.ValuesWillBeMerged);
            }

            return (currentValue[0], null);
        }

        public override (List<string>? NewValue, StandardValueConversionLoss? Loss) ConvertToListString(
            List<string> currentValue) => (currentValue, null);

        public override (decimal? NewValue, StandardValueConversionLoss? Loss) ConvertToNumber(
            List<string> currentValue)
        {
            return decimal.TryParse(currentValue[0], out var number)
                ? (number,
                    currentValue.Count > 1 ? StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained : null)
                : (null, StandardValueConversionLoss.All);
        }

        public override (bool? NewValue, StandardValueConversionLoss? Loss) ConvertToBoolean(List<string> currentValue)
        {
            return (true,
                currentValue.Count > 1 ? StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained : null);
        }

        public override (LinkData? NewValue, StandardValueConversionLoss? Loss) ConvertToLink(
            List<string> currentValue)
        {
            var text = string.Join(InternalOptions.TextSeparator, currentValue);
            var ld = new LinkData { Text = text, Url = text };
            return (ld, currentValue.Count > 1 ? StandardValueConversionLoss.ValuesWillBeMerged : null);
        }

        public override async Task<(DateTime? NewValue, StandardValueConversionLoss? Loss)> ConvertToDateTime(
            List<string> currentValue)
        {
            var date = await specialTextService.TryToParseDateTime(currentValue[0]);
            return (date, date.HasValue ? null : StandardValueConversionLoss.All);
        }

        public override (TimeSpan? NewValue, StandardValueConversionLoss? Loss) ConvertToTime(
            List<string> currentValue)
        {
            return TimeSpan.TryParse(currentValue[0], out var ts)
                ? (ts, currentValue.Count > 1 ? StandardValueConversionLoss.OnlyFirstNotEmptyValueWillBeRemained : null)
                : (null, StandardValueConversionLoss.All);
        }

        public override (string? NewValue, StandardValueConversionLoss? Loss) ConvertToFormula(
            List<string> currentValue) =>
            (null, StandardValueConversionLoss.All);

        public override (List<List<string>>? NewValue, StandardValueConversionLoss? Loss)
            ConvertToMultilevel(List<string> currentValue) =>
            (currentValue.Select(v => new List<string> { v }).ToList(), null);
    }
}