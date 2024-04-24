﻿using System.Reflection;
using Bakabase.Abstractions.Components.CustomProperty;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.Modules.CustomProperty.Properties.Attachment;
using Bakabase.Modules.CustomProperty.Properties.Boolean;
using Bakabase.Modules.CustomProperty.Properties.Choice;
using Bakabase.Modules.CustomProperty.Properties.DateTime;
using Bakabase.Modules.CustomProperty.Properties.Formula;
using Bakabase.Modules.CustomProperty.Properties.Multilevel;
using Bakabase.Modules.CustomProperty.Properties.Number;
using Bakabase.Modules.CustomProperty.Properties.Text;
using Bakabase.Modules.CustomProperty.Properties.Time;
using Bootstrap.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Bakabase.Modules.CustomProperty.Extensions;

public static class CustomPropertyExtensions
{
    public static Dictionary<CustomPropertyType, ICustomPropertyDescriptor> Descriptors = new()
    {
        {CustomPropertyType.SingleLineText, new SingleLineTextPropertyDescriptor()},
        {CustomPropertyType.MultilineText, new MultilineTextPropertyDescriptor()},
        {CustomPropertyType.SingleChoice, new SingleChoicePropertyDescriptor()},
        {CustomPropertyType.MultipleChoice, new MultipleChoicePropertyDescriptor()},
        {CustomPropertyType.Number, new NumberPropertyDescriptor()},
        {CustomPropertyType.Percentage, new PercentagePropertyDescriptor()},
        {CustomPropertyType.Rating, new RatingPropertyDescriptor()},
        {CustomPropertyType.Boolean, new BooleanPropertyDescriptor()},
        {CustomPropertyType.Link, new LinkPropertyDescriptor()},
        {CustomPropertyType.Attachment, new AttachmentPropertyDescriptor()},
        {CustomPropertyType.Date, new DatePropertyDescriptor()},
        {CustomPropertyType.DateTime, new DateTimePropertyDescriptor()},
        {CustomPropertyType.Time, new TimePropertyDescriptor()},
        {CustomPropertyType.Formula, new FormulaPropertyDescriptor()},
        {CustomPropertyType.Multilevel, new MultilevelPropertyDescriptor()},
    };

    public static Abstractions.Models.Domain.CustomProperty? ToDomainModel(
        this Abstractions.Models.Db.CustomProperty? entity)
    {
        if (entity == null)
        {
            return null;
        }

        return Descriptors[entity.Type].BuildDomainProperty(entity);
    }

    public static StandardValueType ToStandardValueType(this CustomPropertyType type) => (StandardValueType) type;
    public static CustomPropertyType ToCustomValueType(this StandardValueType type) => (CustomPropertyType) type;

    public static IServiceCollection AddCustomProperty(this IServiceCollection services)
    {
        var types = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t is {IsClass: true, IsAbstract: false, IsPublic: true} &&
                        t.IsAssignableTo(SpecificTypeUtils<ICustomPropertyDescriptor>.Type))
            .ToList();
        foreach (var t in types)
        {
            services.AddSingleton(SpecificTypeUtils<ICustomPropertyDescriptor>.Type, t);
        }

        return services;
    }
}