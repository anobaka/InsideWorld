﻿using System.Collections.Generic;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Models.Aos;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.InsideWorld.App.Core.Controllers
{
    [Route("~/third-party")]
    public class ThirdPartyController : Controller
    {
        private readonly ThirdPartyService _service;

        public ThirdPartyController(ThirdPartyService service)
        {
            _service = service;
        }

        [HttpGet("request-statistics")]
        [SwaggerOperation(OperationId = "GetAllThirdPartyRequestStatistics")]
        public SingletonResponse<ThirdPartyRequestStatistics[]> GetAllRequestStatistics()
        {
            return new SingletonResponse<ThirdPartyRequestStatistics[]>(_service.GetAllThirdPartyRequestStatistics());
        }
    }
}