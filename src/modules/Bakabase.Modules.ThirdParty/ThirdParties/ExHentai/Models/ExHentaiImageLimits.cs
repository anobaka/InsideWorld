﻿namespace Bakabase.Modules.ThirdParty.ThirdParties.ExHentai.Models
{
    public class ExHentaiImageLimits
    {
        public int Current { get; set; }
        public int Limit { get; set; }
        public int Rest => Limit - Current;
        public int ResetCost { get; set; }
    }
}
