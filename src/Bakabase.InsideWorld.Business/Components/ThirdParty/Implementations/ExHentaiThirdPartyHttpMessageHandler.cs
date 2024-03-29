﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Infrastructures.Components.Configurations;
using Bakabase.InsideWorld.Business.Components.Network;
using Bakabase.InsideWorld.Business.Components.ThirdParty.Http;
using Bakabase.InsideWorld.Business.Configurations;
using Bakabase.InsideWorld.Models.Configs;
using Bakabase.InsideWorld.Models.Configs.Infrastructures;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Models.Aos;
using Bootstrap.Components.Configuration;
using Microsoft.Extensions.Options;

namespace Bakabase.InsideWorld.Business.Components.ThirdParty.Implementations
{
    public class
        ExHentaiThirdPartyHttpMessageHandler : InsideWorldOptionsBasedThirdPartyHttpMessageHandler<ExHentaiOptions,
            ThirdPartyHttpClientOptions>
    {
        public ExHentaiThirdPartyHttpMessageHandler(ThirdPartyHttpRequestLogger logger,
            AspNetCoreOptionsManager<ExHentaiOptions> optionsManager, InsideWorldWebProxy proxy) : base(logger, ThirdPartyId.ExHentai,
            optionsManager, proxy)
        {
        }

        protected override ThirdPartyHttpClientOptions ToThirdPartyHttpClientOptions(ExHentaiOptions options)
        {
            return new ThirdPartyHttpClientOptions
            {
                Cookie = options.Cookie,
                Interval = options.Downloader?.Interval ?? 500,
                MaxThreads = 1
            };
        }
    }
}