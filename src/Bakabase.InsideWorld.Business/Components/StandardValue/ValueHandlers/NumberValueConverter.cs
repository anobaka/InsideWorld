﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Bakabase.InsideWorld.Business.Components.StandardValue.Abstractions;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.Modules.CustomProperty.Properties.Text;

namespace Bakabase.InsideWorld.Business.Components.StandardValue.Values
{
    public class NumberValueConverter : AbstractStandardValueHandler<decimal>
    {
        public override StandardValueType Type => StandardValueType.Number;

        public override Dictionary<StandardValueType, StandardValueConversionLoss?> DefaultConversionLoss { get; } =
            new()
            {
                {StandardValueType.SingleLineText, null},
                {StandardValueType.MultilineText, null},
                {StandardValueType.Link, StandardValueConversionLoss.All},
                {StandardValueType.SingleTextChoice, null},
                {StandardValueType.MultipleTextChoice, null},
                {StandardValueType.Number, null},
                {StandardValueType.Percentage, null},
                {StandardValueType.Rating, null},
                {StandardValueType.Boolean, StandardValueConversionLoss.NonZeroValueWillBeConvertedToTrue},
                {StandardValueType.Attachment, StandardValueConversionLoss.All},
                {StandardValueType.Date, StandardValueConversionLoss.All},
                {StandardValueType.DateTime, StandardValueConversionLoss.All},
                {StandardValueType.Time, StandardValueConversionLoss.All},
                {StandardValueType.Formula, StandardValueConversionLoss.All},
                {StandardValueType.MultipleTextTree, StandardValueConversionLoss.All},

            };

        public override (bool? NewValue, StandardValueConversionLoss? Loss) ConvertToBoolean(decimal currentValue)
        {
            return (currentValue != 0,
                currentValue == 0 ? null : StandardValueConversionLoss.NonZeroValueWillBeConvertedToTrue);
        }

        public override Task<(DateTime? NewValue, StandardValueConversionLoss? Loss)> ConvertToDateTime(
            decimal currentValue)
        {
            return Task.FromResult<(DateTime? NewValue, StandardValueConversionLoss? Loss)>((null,
                StandardValueConversionLoss.All));
        }

        public override (string? NewValue, StandardValueConversionLoss? Loss) ConvertToFormula(decimal currentValue)
        {
            return (null, StandardValueConversionLoss.All);
        }

        public override (LinkData? NewValue, StandardValueConversionLoss? Loss) ConvertToLink(decimal currentValue)
        {
            return (null, StandardValueConversionLoss.All);
        }

        public override (List<string>? NewValue, StandardValueConversionLoss? Loss) ConvertToListString(
            decimal currentValue)
        {
            return ([currentValue.ToString(CultureInfo.InvariantCulture)], null);
        }

        public override (List<List<string>>? NewValue, StandardValueConversionLoss? Loss) ConvertToMultilevel(
            decimal currentValue)
        {
            return ([[currentValue.ToString(CultureInfo.InvariantCulture)]], null);
        }

        public override (decimal? NewValue, StandardValueConversionLoss? Loss) ConvertToNumber(decimal currentValue)
        {
            return (currentValue, null);
        }

        public override (string? NewValue, StandardValueConversionLoss? Loss) ConvertToString(decimal currentValue)
        {
            return (currentValue.ToString(CultureInfo.InvariantCulture), null);
        }

        public override (TimeSpan? NewValue, StandardValueConversionLoss? Loss) ConvertToTime(decimal currentValue)
        {
            return (null, StandardValueConversionLoss.All);
        }

        protected override decimal ConvertToTypedValue(object? currentValue)
        {
            return currentValue is decimal d ? d : 0;
        }
    }
}