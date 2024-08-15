﻿using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Modules.CustomProperty.Models.Domain.Constants;
using Bakabase.Modules.Enhancer.Abstractions;
using Bakabase.Modules.Enhancer.Abstractions.Attributes;
using Bakabase.Modules.Enhancer.Abstractions.Models.Domain.Constants;

namespace Bakabase.Modules.Enhancer.Components.Enhancers.Bangumi;

public enum BangumiEnhancerTarget
{
    [EnhancerTarget(StandardValueType.String, CustomPropertyType.SingleLineText,
        [EnhancerTargetOptionsItem.IntegrateWithAlias, EnhancerTargetOptionsItem.AutoGenerateProperties])]
    Name = 1,

    [EnhancerTarget(StandardValueType.ListTag, CustomPropertyType.Tags,
        [EnhancerTargetOptionsItem.IntegrateWithAlias, EnhancerTargetOptionsItem.AutoGenerateProperties])]
    Tags = 2,

    [EnhancerTarget(StandardValueType.String, CustomPropertyType.MultilineText,
        [EnhancerTargetOptionsItem.AutoGenerateProperties])]
    Introduction = 3,

    [EnhancerTarget(StandardValueType.Decimal, CustomPropertyType.Rating,
        [EnhancerTargetOptionsItem.AutoGenerateProperties])]
    Rating = 4,

    [EnhancerTarget(StandardValueType.ListString, CustomPropertyType.MultipleChoice,
        [EnhancerTargetOptionsItem.AutoGenerateProperties, EnhancerTargetOptionsItem.IntegrateWithAlias], true)]
    OtherPropertiesInLeftPanel = 5,

    [EnhancerTarget(StandardValueType.ListString, CustomPropertyType.Attachment,
        [EnhancerTargetOptionsItem.AutoGenerateProperties])]
    Cover = 6,
}