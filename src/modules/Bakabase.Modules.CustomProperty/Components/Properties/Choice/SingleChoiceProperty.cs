﻿using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.RequestModels;
using Bakabase.Modules.CustomProperty.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.CustomProperty.Components.Properties.Choice.Abstractions;
using Bakabase.Modules.CustomProperty.Extensions;
using Bakabase.Modules.StandardValue.Abstractions.Components;

namespace Bakabase.Modules.CustomProperty.Components.Properties.Choice;

public record SingleChoiceProperty : ChoiceProperty<string>;

public record SingleChoicePropertyValue : CustomPropertyValue<string>
{
    // protected override bool IsMatch(string? value, CustomPropertyValueSearchRequestModel model)
    // {
    //     throw new NotImplementedException();
    // }
}

public class SingleChoicePropertyDescriptor(IStandardValueHelper standardValueHelper)
    : AbstractCustomPropertyDescriptor<SingleChoiceProperty,
        ChoicePropertyOptions<string>, SingleChoicePropertyValue, string, string>(standardValueHelper)
{
    public override CustomPropertyType EnumType => CustomPropertyType.SingleChoice;

    public override SearchOperation[] SearchOperations { get; } =
    [
        SearchOperation.Equals,
        SearchOperation.NotEquals,
        SearchOperation.IsNull,
        SearchOperation.IsNotNull,
        SearchOperation.In,
        SearchOperation.NotIn
    ];

    protected override (object DbValue, SearchOperation Operation)? BuildSearchFilterByKeyword(
        SingleChoiceProperty property, string keyword)
    {
        var ids = property.Options?.Choices?.Where(c => c.Label.Contains(keyword)).Select(x => x.Value).ToList();
        return ids?.Any() == true ? (ids, SearchOperation.In) : null;
    }

    protected override (string? DbValue, bool PropertyChanged) TypedPrepareDbValueFromBizValue(
        SingleChoiceProperty property, string bizValue)
    {
        if (!string.IsNullOrEmpty(bizValue))
        {
            var propertyChanged = (property.Options ??= new ChoicePropertyOptions<string>()).AddChoices(true, bizValue);
            var stringValue = property.Options.Choices?.Find(x => x.Label == bizValue)?.Value;
            var nv = new StringValueBuilder(stringValue).Value;
            return (nv, propertyChanged);
        }

        return (null, false);
    }

    protected override bool IsMatch(string? value, SearchOperation operation, object? filterValue)
    {
        switch (operation)
        {
            case SearchOperation.Equals:
            case SearchOperation.NotEquals:
            {
                var searchId = filterValue as string;
                // invalid filter
                if (string.IsNullOrEmpty(searchId))
                {
                    return true;
                }

                return operation == SearchOperation.Equals
                    ? string.Equals(searchId, value)
                    : !string.Equals(searchId, value);
            }
            case SearchOperation.IsNull:
                return string.IsNullOrEmpty(value);
            case SearchOperation.IsNotNull:
                return !string.IsNullOrEmpty(value);
            case SearchOperation.In:
            case SearchOperation.NotIn:
            {
                if (string.IsNullOrEmpty(value))
                {
                    return false;
                }

                var searchIds = filterValue as List<string>;
                if (searchIds?.Any() == true)
                {
                    return operation == SearchOperation.In
                        ? searchIds.Contains(value)
                        : !searchIds.Contains(value);
                }

                // invalid filter
                return true;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override string? TypedConvertDbValueToBizValue(SingleChoiceProperty property, string value)
    {
        return property.Options?.Choices?.FirstOrDefault(c => c.Value == value)?.Label;
    }
}