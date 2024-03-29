﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Bakabase.InsideWorld.Business.Components.Resource.Components.Enhancer.Infrastructures;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Attributes;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bakabase.InsideWorld.Models.Models.Dtos;
using Bakabase.InsideWorld.Models.RequestModels;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Bakabase.InsideWorld.App.Core.Controllers
{
    [Route("~/resource-category")]
    public class ResourceCategoryController : Controller
    {
        private readonly ResourceCategoryService _service;

        public ResourceCategoryController(ResourceCategoryService service)
        {
            _service = service;
        }

        [HttpGet]
        [SwaggerOperation(OperationId = "GetAllResourceCategories")]
        public async Task<ListResponse<ResourceCategoryDto>> GetAll(
            [FromQuery] ResourceCategoryAdditionalItem additionalItems = ResourceCategoryAdditionalItem.None)
        {
            return new ListResponse<ResourceCategoryDto>(await _service.GetAllDto(null, additionalItems));
        }

        [HttpPost]
        [SwaggerOperation(OperationId = "AddResourceCategory")]
        public async Task<BaseResponse> Add([FromBody] ResourceCategoryAddRequestModel model)
        {
            return await _service.Add(model);
        }

        [HttpPost("{id:int}/duplication")]
        [SwaggerOperation(OperationId = "DuplicateResourceCategory")]
        public async Task<BaseResponse> Duplicate(int id, [FromBody] ResourceCategoryDuplicateRequestModel model)
        {
            return await _service.Duplicate(id, model);
        }

        [HttpPut("{id}")]
        [SwaggerOperation(OperationId = "UpdateResourceCategory")]
        public async Task<BaseResponse> Update(int id, [FromBody] ResourceCategoryUpdateRequestModel model)
        {
            return await _service.Patch(id, model);
        }

        [HttpPut("{id}/component")]
        [SwaggerOperation(OperationId = "ConfigureResourceCategoryComponents")]
        public async Task<BaseResponse> ConfigureComponents(int id,
            [FromBody] ResourceCategoryComponentConfigureRequestModel model)
        {
            return await _service.ConfigureComponents(id, model);
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(OperationId = "DeleteResourceCategoryAndClearAllRelatedData")]
        public async Task<BaseResponse> Delete(int id)
        {
            return await _service.DeleteAndClearAllRelatedData(id);
        }

        [HttpPut("orders")]
        [SwaggerOperation(OperationId = "SortCategories")]
        public async Task<BaseResponse> Sort([FromBody] IdBasedSortRequestModel model)
        {
            return await _service.Sort(model.Ids);
        }

        [HttpPost("setup-wizard")]
        [SwaggerOperation(OperationId = "SaveDataFromSetupWizard")]
        public async Task<BaseResponse> SaveDataFromSetupWizard([FromBody] CategorySetupWizardRequestModel model)
        {
            return await _service.SaveDataFromSetupWizard(model);
        }
    }
}