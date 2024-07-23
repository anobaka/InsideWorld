﻿using Bootstrap.Models.RequestModels;

namespace Bakabase.Modules.Alias.Models.Input
{
    public class AliasSearchInputModel : SearchRequestModel
    {
        public HashSet<string>? Texts { get; set; }
        public string? Text { get; set; }
        public string? FuzzyText { get; set; }
    }
}