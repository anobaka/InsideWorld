﻿using Bakabase.Abstractions.Models.Domain;
using Bakabase.Modules.Enhancer.Abstractions.Models.Domain;

namespace Bakabase.Modules.Enhancer.Models.Domain;

public record CategoryEnhancerFullOptions : CategoryEnhancerOptions
{
    public EnhancerFullOptions? Options { get; set; }
}