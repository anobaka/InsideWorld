﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions;
using Bakabase.Abstractions.Components.Configuration;
using Bakabase.Abstractions.Components.Cover;
using Bakabase.Abstractions.Components.Localization;
using Bakabase.Abstractions.Components.Property;
using Bakabase.Abstractions.Extensions;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Models.Input;
using Bakabase.Abstractions.Models.View;
using Bakabase.Abstractions.Services;
using Bakabase.Infrastructures.Components.Gui;
using Bakabase.Infrastructures.Components.Storage.Services;
using Bakabase.InsideWorld.Business.Components;
using Bakabase.InsideWorld.Business.Components.Dependency.Abstractions.Models.Constants;
using Bakabase.InsideWorld.Business.Components.Dependency.Implementations.FfMpeg;
using Bakabase.InsideWorld.Business.Components.Resource.Components.BackgroundTask;
using Bakabase.InsideWorld.Business.Components.Search;
using Bakabase.InsideWorld.Business.Components.Tasks;
using Bakabase.InsideWorld.Business.Configurations;
using Bakabase.InsideWorld.Business.Configurations.Models.Domain;
using Bakabase.InsideWorld.Business.Extensions;
using Bakabase.InsideWorld.Business.Models.Input;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Configs;
using Bakabase.InsideWorld.Models.Constants;
using Bakabase.InsideWorld.Models.Constants.AdditionalItems;
using Bakabase.InsideWorld.Models.Extensions;
using Bakabase.InsideWorld.Models.Models.Aos;
using Bakabase.InsideWorld.Models.RequestModels;
using Bakabase.Modules.Property.Abstractions.Components;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.Modules.Property.Components;
using Bakabase.Modules.Property.Extensions;
using Bakabase.Modules.Property.Models.Db;
using Bakabase.Modules.StandardValue.Abstractions.Components;
using Bakabase.Modules.StandardValue.Extensions;
using Bakabase.Modules.StandardValue.Models.Domain;
using Bakabase.Service.Extensions;
using Bakabase.Service.Models.Input;
using Bakabase.Service.Models.View;
using Bootstrap.Components.Configuration.Abstractions;
using Bootstrap.Components.Miscellaneous.ResponseBuilders;
using Bootstrap.Components.Storage;
using Bootstrap.Extensions;
using Bootstrap.Models.ResponseModels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Swashbuckle.AspNetCore.Annotations;
using static Bakabase.InsideWorld.Models.Configs.UIOptions;
using Image = SixLabors.ImageSharp.Image;

namespace Bakabase.Service.Controllers
{
    [Route("~/resource")]
    public class ResourceController(
        IResourceService service,
        ResourceTaskManager resourceTaskManager,
        FfMpegService ffMpegService,
        IBOptionsManager<ResourceOptions> resourceOptions,
        FfMpegService ffMpegInstaller,
        ILogger<ResourceController> logger,
        ICategoryService categoryService,
        ICoverDiscoverer coverDiscoverer,
        IPropertyService propertyService,
        ICustomPropertyService customPropertyService,
        IPropertyLocalizer propertyLocalizer,
        IMediaLibraryService mediaLibraryService,
        BackgroundTaskManager backgroundTaskManager,
        IBakabaseLocalizer localizer,
        IBOptionsManager<FileSystemOptions> fsOptionsManager)
        : Controller
    {
        [HttpGet("search-operation")]
        [SwaggerOperation(OperationId = "GetSearchOperationsForProperty")]
        public async Task<ListResponse<SearchOperation>> GetSearchOperationsForProperty(
            PropertyPool propertyPool, int propertyId)
        {
            PropertyType? pt;
            if (propertyPool != PropertyPool.Custom)
            {
                pt = PropertyInternals.BuiltinPropertyMap.GetValueOrDefault((ResourceProperty) propertyId)?.Type;
            }
            else
            {
                var p = await customPropertyService.GetByKey(propertyId);
                pt = p.Type;
            }

            if (!pt.HasValue)
            {
                return ListResponseBuilder<SearchOperation>.NotFound;
            }

            var psh = PropertyInternals.PropertySearchHandlerMap.GetValueOrDefault(pt.Value);

            return new ListResponse<SearchOperation>(psh?.SearchOperations.Keys);
        }

        [HttpGet("filter-value-property")]
        [SwaggerOperation(OperationId = "GetFilterValueProperty")]
        public async Task<SingletonResponse<PropertyViewModel>> GetFilterValueProperty(PropertyPool propertyPool,
            int propertyId,
            SearchOperation operation)
        {
            var p = await propertyService.GetProperty(propertyPool, propertyId);

            var psh = PropertyInternals.PropertySearchHandlerMap.GetValueOrDefault(p.Type);
            var options = psh?.SearchOperations.GetValueOrDefault(operation);
            if (options == null)
            {
                return SingletonResponseBuilder<PropertyViewModel>.NotFound;
            }

            if (options.ConvertProperty != null)
            {
                p = options.ConvertProperty(p);
            }

            return new SingletonResponse<PropertyViewModel>(p.ToViewModel(propertyLocalizer));
        }

        [HttpGet("last-search")]
        [SwaggerOperation(OperationId = "GetLastResourceSearch")]
        public async Task<SingletonResponse<ResourceSearchViewModel?>> GetLastResourceSearch()
        {
            var ls = resourceOptions.Value.LastSearchV2;
            if (ls == null)
            {
                return new SingletonResponse<ResourceSearchViewModel?>(null);
            }

            ResourceSearchDbModel[] arr = [ls];
            var viewModels = await arr.ToViewModels(propertyService, propertyLocalizer);
            return new SingletonResponse<ResourceSearchViewModel?>(viewModels[0]);
        }

        [HttpPost("saved-search")]
        [SwaggerOperation(OperationId = "SaveNewResourceSearch")]
        public async Task<BaseResponse> SaveNewSearch([FromBody] SavedSearchAddInputModel model)
        {
            model.Search.StandardPageable();
            await resourceOptions.SaveAsync(x =>
            {
                x.SavedSearches.Add(new ResourceOptions.SavedSearch
                    {Search = model.Search.ToDbModel(), Name = model.Name});
            });
            return BaseResponseBuilder.Ok;
        }

        [HttpPut("saved-search/{idx:int}/name")]
        [SwaggerOperation(OperationId = "PutSavedSearchName")]
        public async Task<BaseResponse> PutSavedSearchName(int idx, [FromBody] string name)
        {
            var searches = resourceOptions.Value.SavedSearches ?? [];
            if (searches.Count <= idx)
            {
                return BaseResponseBuilder.BuildBadRequest($"Invalid {nameof(idx)}:{idx}");
            }

            searches[idx].Name = name;
            await resourceOptions.SaveAsync(x => { x.SavedSearches = searches; });
            return BaseResponseBuilder.Ok;
        }

        [HttpGet("saved-search")]
        [SwaggerOperation(OperationId = "GetSavedSearches")]
        public async Task<ListResponse<SavedSearchViewModel>> GetSavedSearches()
        {
            var searches = resourceOptions.Value.SavedSearches;
            var dbModels = searches.Select(x => x.Search).ToArray();
            var viewModels = await dbModels.ToViewModels(propertyService, propertyLocalizer);
            var ret = searches.Select((s, i) => new SavedSearchViewModel(viewModels[i], s.Name));
            return new ListResponse<SavedSearchViewModel>(ret);
        }

        [HttpDelete("saved-search/{idx:int}")]
        [SwaggerOperation(OperationId = "DeleteSavedSearch")]
        public async Task<BaseResponse> DeleteSavedSearch(int idx)
        {
            await resourceOptions.SaveAsync(x =>
            {
                if (idx < x.SavedSearches.Count)
                {
                    x.SavedSearches.RemoveAt(idx);
                }
            });
            return BaseResponseBuilder.Ok;
        }

        [HttpPost("search")]
        [SwaggerOperation(OperationId = "SearchResources")]
        public async Task<SearchResponse<Resource>> Search([FromBody] ResourceSearchInputModel model, bool saveSearch)
        {
            model.StandardPageable();

            if (saveSearch)
            {
                try
                {
                    await resourceOptions.SaveAsync(a => a.LastSearchV2 = model.ToDbModel());
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save search criteria");
                }
            }

            var domainModel = await model.ToDomainModel(propertyService);

            return await service.Search(domainModel, false);
        }

        [HttpGet("keys")]
        [SwaggerOperation(OperationId = "GetResourcesByKeys")]
        public async Task<ListResponse<Resource>> GetByKeys([FromQuery] int[] ids,
            ResourceAdditionalItem additionalItems = ResourceAdditionalItem.None)
        {
            return new ListResponse<Resource>(await service.GetByKeys(ids, additionalItems));
        }

        // [HttpPut("{id}")]
        // [SwaggerOperation(OperationId = "PatchResource")]
        // public async Task<BaseResponse> Update(int id, [FromBody] ResourceUpdateRequestModel model)
        // {
        // 	return await _service.Patch(id, model);
        // }

        [HttpGet("directory")]
        [SwaggerOperation(OperationId = "OpenResourceDirectory")]
        public async Task<BaseResponse> Open(int id)
        {
            var resource = await service.Get(id, ResourceAdditionalItem.None);
            var rawFileOrDirectoryName = Path.Combine(resource.Path);
            // https://github.com/Bakabase/InsideWorld/issues/51
            if (!System.IO.File.Exists(rawFileOrDirectoryName) && !Directory.Exists(rawFileOrDirectoryName))
            {
                rawFileOrDirectoryName = resource.Directory;
            }

            var rawAttributes = System.IO.File.GetAttributes(rawFileOrDirectoryName);
            FileService.Open(rawFileOrDirectoryName,
                (rawAttributes & FileAttributes.Directory) != FileAttributes.Directory);
            return BaseResponseBuilder.Ok;
        }

        [HttpGet("{id}/cover")]
        [SwaggerOperation(OperationId = "DiscoverResourceCover")]
        [ResponseCache(VaryByQueryKeys = [nameof(id), "t"], Duration = 20 * 60)]
        public async Task<IActionResult> DiscoverCover(int id)
        {
            var resource = await service.Get(id, ResourceAdditionalItem.None);
            if (resource == null)
            {
                return NotFound();
            }

            var category = await categoryService.Get(resource.CategoryId, CategoryAdditionalItem.None);

            var cover = await coverDiscoverer.Discover(resource.Path,
                category?.CoverSelectionOrder ?? CoverSelectOrder.FilenameAscending, true, HttpContext.RequestAborted);
            if (cover == null)
            {
                return NotFound();
            }

            var img = cover.Data != null
                ? await Image.LoadAsync<Rgba32>(new MemoryStream(cover.Data))
                : await Image.LoadAsync<Rgba32>(cover.Path);

            var maxSize = 2048m;
            string mimeType;
            byte[] data;
            if (img.Width > maxSize || img.Height > maxSize)
            {
                var scale = Math.Min(maxSize / img.Width, maxSize / img.Height);
                var newSize = new Size((int) (scale * img.Width), (int) (scale * img.Height));
                img.Mutate(x => x.Resize(newSize));
                var ms = new MemoryStream();
                await img.SaveAsJpegAsync(ms);
                data = ms.ToArray();
                mimeType = MimeTypes.GetMimeType(".jpg");
            }
            else
            {
                if (cover.Data != null)
                {
                    var format = await Image.DetectFormatAsync(new MemoryStream(cover.Data!));
                    data = cover.Data;
                    mimeType = format.DefaultMimeType;
                }
                else
                {
                    data = await System.IO.File.ReadAllBytesAsync(cover.Path);
                    mimeType = MimeTypes.GetMimeType(cover.Path);
                }
            }

            return File(data, mimeType);
        }

        [HttpGet("{id}/playable-files")]
        [SwaggerOperation(OperationId = "GetResourcePlayableFiles")]
        [ResponseCache(Duration = 20 * 60)]
        public async Task<ListResponse<string>> GetPlayableFiles(int id)
        {
            return new ListResponse<string>(await service.GetPlayableFiles(id, HttpContext.RequestAborted) ??
                                            new string[] { });
        }

        [HttpPut("move")]
        [SwaggerOperation(OperationId = "MoveResources")]
        public async Task<BaseResponse> Move([FromBody] ResourceMoveRequestModel model)
        {
            var resources = (await service.GetByKeys(model.Ids)).ToDictionary(t => t.Id, t => t);
            if (!resources.Any())
            {
                return BaseResponseBuilder.BuildBadRequest($"Resources [{string.Join(',', model.Ids)}] are not found");
            }

            var mediaLibrary = await mediaLibraryService.Get(model.MediaLibraryId);

            if (mediaLibrary == null)
            {
                return BaseResponseBuilder.BuildBadRequest($"Invalid {nameof(model.MediaLibraryId)}");
            }

            await fsOptionsManager.SaveAsync(x => x.AddRecentMovingDestination(model.Path.StandardizePath()!));

            var taskName = $"Resource:BulkMove:{DateTime.Now:HH:mm:ss}";
            backgroundTaskManager.RunInBackground(taskName, new CancellationTokenSource(), async (bt, sp) =>
                {
                    var resourceService = sp.GetRequiredService<IResourceService>();

                    bt.Message = localizer.Resource_MovingTaskSummary(
                        resources.Select(r => r.Value.FileName).ToArray(), mediaLibrary.Name, model.Path);
                    foreach (var id in model.Ids)
                    {
                        await resourceTaskManager.Add(new ResourceTaskInfo
                        {
                            BackgroundTaskId = bt.Id,
                            Id = id,
                            Type = ResourceTaskType.Moving,
                            Summary = localizer.Resource_MovingTaskSummary(null, mediaLibrary.Name, model.Path),
                            OperationOnComplete = ResourceTaskOperationOnComplete.RemoveOnResourceView
                        });
                    }

                    foreach (var (id, resource) in resources)
                    {
                        var targetPath = Path.Combine(model.Path, resource.FileName).StandardizePath()!;
                        if (resource.IsFile)
                        {
                            await FileUtils.MoveAsync(resource.Path, targetPath, false,
                                async p =>
                                {
                                    await resourceTaskManager.Update(id, t => t.Percentage = Math.Min(99, p));
                                },
                                bt.Cts.Token);
                        }
                        else
                        {
                            await DirectoryUtils.MoveAsync(resource.Path, targetPath, false,
                                async p =>
                                {
                                    await resourceTaskManager.Update(id, t => t.Percentage = Math.Min(99, p));
                                },
                                bt.Cts.Token);
                        }

                        if (resource.MediaLibraryId != mediaLibrary.Id)
                        {
                            await resourceService.ChangeMediaLibraryAndPath(resource.Id, mediaLibrary.Id, targetPath);
                        }

                        // var p = 0;
                        // while (p < 100)
                        // {
                        //     p++;
                        //     await Task.Delay(100, bt.Cts.Token);
                        //     await resourceTaskManager.Update(id, t => t.Percentage = p);
                        // }

                        await resourceTaskManager.Update(id, t => t.Percentage = 100);

                        await resourceTaskManager.Clear(id);
                    }

                    return BaseResponseBuilder.Ok;
                }, BackgroundTaskLevel.Default, null, null,
                async task => await resourceTaskManager.Update(resources.Keys.ToArray(), t => t.Error = task.Message));

            return BaseResponseBuilder.Ok;
        }

        [HttpDelete("{id}/task")]
        [SwaggerOperation(OperationId = "ClearResourceTask")]
        public async Task<BaseResponse> ClearTask(int id)
        {
            await resourceTaskManager.Clear(id);
            return BaseResponseBuilder.Ok;
        }

        // [HttpPost("nfo")]
        // [SwaggerOperation(OperationId = "StartResourceNfoGenerationTask")]
        // public async Task<BaseResponse> StartNfoGenerationTask()
        // {
        // 	await _service.TryToGenerateNfoInBackground();
        // 	return BaseResponseBuilder.Ok;
        // }

        [HttpGet("{id}/previewer")]
        [SwaggerOperation(OperationId = "GetResourceDataForPreviewer")]
        public async Task<ListResponse<PreviewerItem>> GetResourceDataForPreviewer(int id)
        {
            var resource = await service.Get(id, ResourceAdditionalItem.None);

            if (resource == null)
            {
                return ListResponseBuilder<PreviewerItem>.NotFound;
            }

            var filePaths = new List<string>();
            if (System.IO.File.Exists(resource.Path))
            {
                filePaths.Add(resource.Path);
            }
            else
            {
                if (Directory.Exists(resource.Path))
                {
                    filePaths.AddRange(Directory.GetFiles(resource.Path, "*", SearchOption.AllDirectories));
                }
                else
                {
                    return ListResponseBuilder<PreviewerItem>.NotFound;
                }
            }

            var items = new List<PreviewerItem>();
            var ffmpegIsReady = ffMpegInstaller.Status == DependentComponentStatus.Installed;

            foreach (var f in filePaths)
            {
                var type = f.InferMediaType();
                switch (type)
                {
                    case MediaType.Image:
                        items.Add(new PreviewerItem
                        {
                            Duration = 1,
                            FilePath = f.StandardizePath()!,
                            Type = type
                        });
                        break;
                    case MediaType.Video:
                        if (ffmpegIsReady)
                        {
                            items.Add(new PreviewerItem
                            {
                                Duration = (int) Math.Ceiling(
                                    (await ffMpegService.GetDuration(f, HttpContext.RequestAborted))),
                                FilePath = f.StandardizePath()!,
                                Type = type
                            });
                        }

                        break;
                    case MediaType.Text:
                    case MediaType.Audio:
                    case MediaType.Unknown:
                        // Not available for previewing
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return new ListResponse<PreviewerItem>(items);
        }

        [HttpPut("{id:int}/property-value")]
        [SwaggerOperation(OperationId = "PutResourcePropertyValue")]
        public async Task<BaseResponse> PutPropertyValue(int id, [FromBody] ResourcePropertyValuePutInputModel model)
        {
            return await service.PutPropertyValue(id, model);
        }

        [HttpGet("{resourceId}/play")]
        [SwaggerOperation(OperationId = "PlayResourceFile")]
        public async Task<BaseResponse> Play(int resourceId, string file)
        {
            return await service.Play(resourceId, file);
        }

        [HttpDelete("unknown")]
        [SwaggerOperation(OperationId = "DeleteUnknownResources")]
        public async Task<BaseResponse> DeleteUnknown()
        {
            await service.DeleteUnknown();
            return BaseResponseBuilder.Ok;
        }

        [HttpGet("unknown/count")]
        [SwaggerOperation(OperationId = "GetUnknownResourcesCount")]
        public async Task<SingletonResponse<int>> GetUnknownCount()
        {
            return new SingletonResponse<int>(data: await service.GetUnknownCount());
        }
    }
}