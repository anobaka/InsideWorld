﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.InsideWorld.Business.Components.Dependency.Abstractions;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bootstrap.Components.Miscellaneous.ResponseBuilders;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ComponentDescriptor = Bakabase.Abstractions.Models.Domain.ComponentDescriptor;

namespace Bakabase.Service.Controllers
{
    [Route("~/component")]
    public class ComponentController : Controller
    {
        private readonly ComponentService _componentService;
        private readonly Dictionary<string, IDependentComponentService> _componentServices;

        public ComponentController(ComponentService componentService,
            IEnumerable<IDependentComponentService> componentInstallers)
        {
            _componentService = componentService;
            _componentServices = componentInstallers.ToDictionary(a => a.Id, a => a);
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "GetComponentDescriptors")]
        public async Task<ListResponse<ComponentDescriptor>> GetDescriptors(ComponentType? type = null,
            ComponentDescriptorAdditionalItem additionalItems = ComponentDescriptorAdditionalItem.None)
        {
            return new ListResponse<ComponentDescriptor>(
                await _componentService.GetDescriptorDtoList(type, additionalItems));
        }

        [HttpGet("key")]
        [SwaggerOperation(OperationId = "GetComponentDescriptorByKey")]
        public async Task<SingletonResponse<ComponentDescriptor>> GetDescriptor(string key,
            ComponentDescriptorAdditionalItem additionalItems = ComponentDescriptorAdditionalItem.None)
        {
            return new SingletonResponse<ComponentDescriptor>(
                await _componentService.GetDescriptorDto(key, additionalItems));
        }

        [HttpGet("dependency/discovery")]
        [SwaggerOperation(OperationId = "DiscoverDependentComponent")]
        public async Task<BaseResponse> DiscoverDependentComponent(string id)
        {
            await _componentServices[id].Discover(HttpContext.RequestAborted);
            return BaseResponseBuilder.Ok;
        }

        [HttpGet("dependency/latest-version")]
        [SwaggerOperation(OperationId = "GetDependentComponentLatestVersion")]
        public async Task<SingletonResponse<DependentComponentVersion>> GetDependentComponentLatestVersion(string id, bool fromCache = true)
        {
            var installer = _componentServices[id];
            var latestVersion = await installer.GetLatestVersion(fromCache, HttpContext.RequestAborted);
            return new SingletonResponse<DependentComponentVersion>(latestVersion);
        }

        [HttpPost("dependency")]
        [SwaggerOperation(OperationId = "InstallDependentComponent")]
        public async Task<BaseResponse> InstallDependentComponent(string id)
        {
            _ = Task.Run(async () => await _componentServices[id].Install(new CancellationToken()));
            return BaseResponseBuilder.Ok;
        }
    }
}