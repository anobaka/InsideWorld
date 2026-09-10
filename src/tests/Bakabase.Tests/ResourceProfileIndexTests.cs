using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Events;
using Bakabase.Abstractions.Components.Tasks;
using Bakabase.Abstractions.Models.Db;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Business;
using Bakabase.InsideWorld.Business.Components.Search.Index;
using Bakabase.InsideWorld.Business.Models.Db;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants.Aos;
using Bakabase.Modules.Search.Models.Db;
using Bakabase.TestKit.Utils;
using Bootstrap.Components.Tasks;
using Bootstrap.Components.Orm;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;

namespace Bakabase.Tests;

/// <summary>
/// Integration coverage for ResourceProfileIndexService — the inverted index that maps
/// resources to matching profiles. Covers RebuildAsync / readiness, the forward and
/// reverse lookups, priority-ordered results, and (filling a round-25 gap) profile
/// matching driven by a real search filter rather than a catch-all.
/// </summary>
[TestClass]
public sealed class ResourceProfileIndexTests
{
    private const string ResourceProfileIndexTaskId = "ResourceProfileIndex";
    private string _testRoot = null!;
    private IServiceProvider _sp = null!;
    private ControllableResourceSearchIndexService _searchIndex = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _sp = await TestServiceBuilder.BuildServiceProvider(services =>
        {
            services.RemoveAll<IResourceSearchIndexService>();
            services.AddSingleton<ControllableResourceSearchIndexService>();
            services.AddSingleton<IResourceSearchIndexService>(sp =>
                sp.GetRequiredService<ControllableResourceSearchIndexService>());
        });
        _searchIndex = _sp.GetRequiredService<ControllableResourceSearchIndexService>();
        _testRoot = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            $"ResourceProfileIndexTests.{DateTime.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testRoot))
        {
            try { Directory.Delete(_testRoot, true); } catch { }
        }
    }

    private async Task Seed(params string[] names)
    {
        foreach (var n in names)
        {
            Directory.CreateDirectory(Path.Combine(_testRoot, n));
        }

        await _sp.GetRequiredService<IPathMarkService>().Add(new PathMark
        {
            Path = _testRoot,
            Type = PathMarkType.Resource,
            ConfigJson = JsonConvert.SerializeObject(new ResourceMarkConfig
            {
                MatchMode = PathMatchMode.Layer,
                Layer = 1,
                FsTypeFilter = PathFilterFsType.Directory
            }),
            Priority = 100
        });
        await _sp.GetRequiredService<ResourceSyncService>().SyncResources(
            ResourceSource.PathMark, null, null, new PauseToken(), CancellationToken.None);
    }

    /// <summary>A profile search that matches only resources whose Filename equals the given value.</summary>
    private static string FilenameEqualsSearchJson(string filename)
        => JsonConvert.SerializeObject(new ResourceSearchDbModel
        {
            Group = new ResourceSearchFilterGroupDbModel
            {
                Combinator = SearchCombinator.And,
                Filters =
                [
                    new ResourceSearchFilterDbModel
                    {
                        PropertyPool = PropertyPool.Internal,
                        PropertyId = (int)InternalProperty.Filename,
                        Operation = SearchOperation.Equals,
                        Value = filename
                    }
                ]
            }
        });

    private Task<ResourceProfile> AddProfile(string name, string? searchJson, int priority = 100)
        => _sp.GetRequiredService<IResourceProfileService>()
            .Add(name, searchJson, null, null, null, null, null, priority);

    private IResourceProfileIndexService Index() => _sp.GetRequiredService<IResourceProfileIndexService>();

    private async Task RebuildIndex() => await Index().RebuildAsync(null, CancellationToken.None);

    private async Task<int> ResourceId(string filename)
        => (await _sp.GetRequiredService<IResourceService>().GetAll()).Single(r => r.FileName == filename).Id;

    private async Task RunPendingIncrementalUpdate()
    {
        var taskManager = _sp.GetRequiredService<BTaskManager>();
        var enqueueDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < enqueueDeadline)
        {
            var queuedTask = taskManager.GetTaskViewModel(ResourceProfileIndexTaskId);
            if (queuedTask != null && !queuedTask.Status.IsFinished()) break;
            await Task.Delay(20);
        }

        var task = taskManager.GetTaskViewModel(ResourceProfileIndexTaskId);
        Assert.IsNotNull(task,
            "Resource profile index update task was not enqueued after invalidation");
        Assert.IsFalse(task!.Status.IsFinished(),
            "Resource profile index update task was not replaced after the previous run finished");

        await taskManager.Start(ResourceProfileIndexTaskId);
        var completionDeadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (DateTime.UtcNow < completionDeadline)
        {
            var status = taskManager.GetTaskViewModel(ResourceProfileIndexTaskId)?.Status;
            if (status == BTaskStatus.Completed) return;
            if (status == BTaskStatus.Error)
            {
                Assert.Fail("Resource profile index update task failed");
            }

            await Task.Delay(20);
        }

        Assert.Fail("Resource profile index update task did not complete within the timeout");
    }

    private async Task DrainSetupInvalidations()
    {
        await RunPendingIncrementalUpdate();
        await _sp.GetRequiredService<BTaskManager>().Clean(ResourceProfileIndexTaskId);
    }

    [TestMethod]
    public async Task RebuildAsync_MakesIndexReady()
    {
        await Seed("a", "b");
        Assert.IsFalse(Index().IsReady);
        await RebuildIndex();
        Assert.IsTrue(Index().IsReady);
    }

    [TestMethod]
    public async Task GetMatchingResourceIds_CatchAllProfile_MatchesEveryResource()
    {
        await Seed("a", "b", "c");
        var profile = await AddProfile("all", "{}");
        await RebuildIndex();

        var matched = await Index().GetMatchingResourceIds(profile.Id);
        Assert.AreEqual(3, matched.Count);
    }

    [TestMethod]
    public async Task GetMatchingResourceIds_FilteringProfile_MatchesOnlyMatchingResources()
    {
        await Seed("apple", "banana", "cherry");
        var profile = await AddProfile("banana-only", FilenameEqualsSearchJson("banana"));
        await RebuildIndex();

        var matched = await Index().GetMatchingResourceIds(profile.Id);
        Assert.AreEqual(1, matched.Count);
        Assert.IsTrue(matched.Contains(await ResourceId("banana")));
    }

    [TestMethod]
    public async Task GetMatchingResourceIds_UnknownProfile_ReturnsEmpty()
    {
        await Seed("a");
        await RebuildIndex();
        Assert.AreEqual(0, (await Index().GetMatchingResourceIds(999999)).Count);
    }

    [TestMethod]
    public async Task GetMatchingProfileIds_ReturnsMatchingProfile()
    {
        await Seed("a");
        var profile = await AddProfile("all", "{}");
        await RebuildIndex();

        var profileIds = await Index().GetMatchingProfileIds(await ResourceId("a"));
        CollectionAssert.Contains(profileIds.ToList(), profile.Id);
    }

    [TestMethod]
    public async Task GetMatchingProfileIds_FilteringProfile_ExcludesNonMatchingResource()
    {
        await Seed("apple", "banana");
        var profile = await AddProfile("banana-only", FilenameEqualsSearchJson("banana"));
        await RebuildIndex();

        var appleProfiles = await Index().GetMatchingProfileIds(await ResourceId("apple"));
        Assert.IsFalse(appleProfiles.Contains(profile.Id));

        var bananaProfiles = await Index().GetMatchingProfileIds(await ResourceId("banana"));
        Assert.IsTrue(bananaProfiles.Contains(profile.Id));
    }

    [TestMethod]
    public async Task GetMatchingProfileIds_SortedByPriorityDescending()
    {
        await Seed("a");
        var low = await AddProfile("low", "{}", priority: 10);
        var high = await AddProfile("high", "{}", priority: 20);
        await RebuildIndex();

        var profileIds = await Index().GetMatchingProfileIds(await ResourceId("a"));
        CollectionAssert.AreEqual(new[] { high.Id, low.Id }, profileIds.ToList());
    }

    [TestMethod]
    public async Task GetMatchingProfileIdsForResources_BatchReturnsEachResource()
    {
        await Seed("a", "b");
        var profile = await AddProfile("all", "{}");
        await RebuildIndex();

        var resourceIds = (await _sp.GetRequiredService<IResourceService>().GetAll()).Select(r => r.Id).ToList();
        var map = await Index().GetMatchingProfileIdsForResources(resourceIds);

        Assert.AreEqual(2, map.Count);
        Assert.IsTrue(map.Values.All(ps => ps.Contains(profile.Id)));
    }

    [TestMethod]
    public async Task GetMatchingProfileIds_NoProfiles_ReturnsEmpty()
    {
        await Seed("a");
        await RebuildIndex();
        Assert.AreEqual(0, (await Index().GetMatchingProfileIds(await ResourceId("a"))).Count);
    }

    [TestMethod]
    public async Task ProfileInvalidation_ReplacesMappings_ReordersPriorityAndInvalidatesCaches()
    {
        await Seed("apple", "banana");
        var movedProfile = await AddProfile("moving", FilenameEqualsSearchJson("apple"), priority: 10);
        var catchAllProfile = await AddProfile("all", "{}", priority: 20);
        await _searchIndex.RebuildAllAsync(CancellationToken.None);
        await RebuildIndex();
        await DrainSetupInvalidations();

        var appleResourceId = await ResourceId("apple");
        var bananaResourceId = await ResourceId("banana");
        var cacheOrm = _sp.GetRequiredService<
            FullMemoryCacheResourceService<BakabaseDbContext, ResourceCacheDbModel, int>>();
        await cacheOrm.AddRange(
        [
            new ResourceCacheDbModel
            {
                ResourceId = appleResourceId,
                CachedTypes = ResourceCacheType.PlayableFiles,
                PlayableFilePaths = JsonConvert.SerializeObject(new[] { "apple.mp4" })
            },
            new ResourceCacheDbModel
            {
                ResourceId = bananaResourceId,
                CachedTypes = ResourceCacheType.PlayableFiles,
                PlayableFilePaths = JsonConvert.SerializeObject(new[] { "banana.mp4" })
            }
        ]);

        await _sp.GetRequiredService<IResourceProfileService>().Update(
            movedProfile.Id,
            "moving",
            FilenameEqualsSearchJson("banana"),
            null,
            null,
            null,
            null,
            null,
            30);
        await RunPendingIncrementalUpdate();

        CollectionAssert.AreEqual(
            new[] { catchAllProfile.Id },
            (await Index().GetMatchingProfileIds(appleResourceId)).ToList());
        CollectionAssert.AreEqual(
            new[] { movedProfile.Id, catchAllProfile.Id },
            (await Index().GetMatchingProfileIds(bananaResourceId)).ToList(),
            "a priority edit must re-sort an existing reverse mapping");
        Assert.IsFalse((await Index().GetMatchingResourceIds(movedProfile.Id)).Contains(appleResourceId));
        Assert.IsTrue((await Index().GetMatchingResourceIds(movedProfile.Id)).Contains(bananaResourceId));

        foreach (var resourceId in new[] { appleResourceId, bananaResourceId })
        {
            var cache = await cacheOrm.GetByKey(resourceId);
            Assert.IsNotNull(cache);
            Assert.IsFalse(cache!.CachedTypes.HasFlag(ResourceCacheType.PlayableFiles));
            Assert.IsNull(cache.PlayableFilePaths);
        }
    }

    [TestMethod]
    public async Task ResourceInvalidation_BulkUpdatesBothMappings_PreservesOrderAndInvalidatesChangedCache()
    {
        await Seed("apple", "banana");
        var appleProfile = await AddProfile("apple", FilenameEqualsSearchJson("apple"), priority: 20);
        var cherryProfile = await AddProfile("cherry", FilenameEqualsSearchJson("cherry"), priority: 30);
        var catchAllProfile = await AddProfile("all", "{}", priority: 10);
        await _sp.GetRequiredService<IResourceSearchIndexService>().RebuildAllAsync(CancellationToken.None);
        await RebuildIndex();
        await DrainSetupInvalidations();

        var appleResourceId = await ResourceId("apple");
        var bananaResourceId = await ResourceId("banana");
        var cacheOrm = _sp.GetRequiredService<
            FullMemoryCacheResourceService<BakabaseDbContext, ResourceCacheDbModel, int>>();
        await cacheOrm.AddRange(
        [
            new ResourceCacheDbModel
            {
                ResourceId = appleResourceId,
                CachedTypes = ResourceCacheType.PlayableFiles,
                PlayableFilePaths = JsonConvert.SerializeObject(new[] { "apple.mp4" })
            },
            new ResourceCacheDbModel
            {
                ResourceId = bananaResourceId,
                CachedTypes = ResourceCacheType.PlayableFiles,
                PlayableFilePaths = JsonConvert.SerializeObject(new[] { "banana.mp4" })
            }
        ]);

        // Publish the same event production writes use: the search-index update and profile-index
        // invalidation are asynchronous siblings, so the profile task must wait for the former.
        var resourceOrm = _sp.GetRequiredService<
            FullMemoryCacheResourceService<BakabaseDbContext, ResourceDbModel, int>>();
        await resourceOrm.UpdateByKey(
            appleResourceId,
            r => r.Path = Path.Combine(_testRoot, "cherry"));

        _sp.GetRequiredService<IResourceDataChangeEventPublisher>()
            .PublishResourcesChanged([appleResourceId, bananaResourceId]);
        await RunPendingIncrementalUpdate();

        CollectionAssert.AreEqual(
            new[] { cherryProfile.Id, catchAllProfile.Id },
            (await Index().GetMatchingProfileIds(appleResourceId)).ToList(),
            "Forward mappings should reflect the renamed resource and remain priority ordered");
        CollectionAssert.AreEqual(
            new[] { catchAllProfile.Id },
            (await Index().GetMatchingProfileIds(bananaResourceId)).ToList(),
            "An invalidated resource whose matching set did not change should retain its mapping");

        Assert.IsFalse((await Index().GetMatchingResourceIds(appleProfile.Id)).Contains(appleResourceId));
        Assert.IsTrue((await Index().GetMatchingResourceIds(cherryProfile.Id)).Contains(appleResourceId));
        Assert.IsTrue((await Index().GetMatchingResourceIds(catchAllProfile.Id)).Contains(appleResourceId));

        var appleCache = await cacheOrm.GetByKey(appleResourceId);
        var bananaCache = await cacheOrm.GetByKey(bananaResourceId);
        Assert.IsNotNull(appleCache);
        Assert.IsNotNull(bananaCache);
        Assert.IsFalse(appleCache!.CachedTypes.HasFlag(ResourceCacheType.PlayableFiles));
        Assert.IsNull(appleCache.PlayableFilePaths);
        Assert.IsTrue(bananaCache!.CachedTypes.HasFlag(ResourceCacheType.PlayableFiles));
        Assert.IsNotNull(bananaCache.PlayableFilePaths);

        var taskManager = _sp.GetRequiredService<BTaskManager>();
        var percentages = taskManager.GetPercentageEvents(ResourceProfileIndexTaskId)
            .Select(e => e.Event)
            .ToList();
        Assert.IsTrue(percentages.Any(p => p is > 0 and < 100),
            "Incremental work should report non-zero progress before completion");
        Assert.AreEqual(1, percentages.First(),
            "Incremental work should leave 0% as soon as processing starts");
        Assert.IsTrue(percentages.Zip(percentages.Skip(1), (left, right) => right >= left).All(x => x),
            "Incremental progress should be monotonic");
        var processes = taskManager.GetProcessEvents(ResourceProfileIndexTaskId)
            .Select(e => e.Event)
            .ToList();
        Assert.IsTrue(processes.Any(p => !string.IsNullOrWhiteSpace(p)),
            "Incremental work should report its process");
        Assert.AreEqual("ResourceProfileIndex_LoadingIncrementalUpdates", processes.First(),
            "Incremental work should report its process from the beginning");
    }

    [TestMethod]
    public async Task ResourceInvalidation_ArrivingDuringActiveTask_IsProcessedByFollowUpTask()
    {
        await Seed("apple", "banana");
        var appleProfile = await AddProfile("apple", FilenameEqualsSearchJson("apple"));
        var bananaProfile = await AddProfile("banana", FilenameEqualsSearchJson("banana"));
        var cherryProfile = await AddProfile("cherry", FilenameEqualsSearchJson("cherry"));
        var dateProfile = await AddProfile("date", FilenameEqualsSearchJson("date"));
        await _searchIndex.RebuildAllAsync(CancellationToken.None);
        await RebuildIndex();
        await DrainSetupInvalidations();

        var appleResourceId = await ResourceId("apple");
        var bananaResourceId = await ResourceId("banana");
        var resourceOrm = _sp.GetRequiredService<
            FullMemoryCacheResourceService<BakabaseDbContext, ResourceDbModel, int>>();
        var publisher = _sp.GetRequiredService<IResourceDataChangeEventPublisher>();

        await resourceOrm.UpdateByKey(
            appleResourceId,
            r => r.Path = Path.Combine(_testRoot, "cherry"));
        var barrier = _searchIndex.BlockNextBarrier();
        publisher.PublishResourcesChanged([appleResourceId]);

        var firstUpdate = RunPendingIncrementalUpdate();
        await barrier.Entered.WaitAsync(TimeSpan.FromSeconds(10));

        // The first task has drained its queue snapshot and is now waiting for SearchIndex.
        // This later change must remain queued and cause a second ResourceProfileIndex task.
        await resourceOrm.UpdateByKey(
            bananaResourceId,
            r => r.Path = Path.Combine(_testRoot, "date"));
        publisher.PublishResourcesChanged([bananaResourceId]);

        Assert.IsFalse(firstUpdate.IsCompleted,
            "The profile task must not evaluate mappings before the search-index barrier completes");
        barrier.Release();
        await firstUpdate;

        CollectionAssert.Contains(
            (await Index().GetMatchingProfileIds(appleResourceId)).ToList(),
            cherryProfile.Id);
        CollectionAssert.DoesNotContain(
            (await Index().GetMatchingProfileIds(appleResourceId)).ToList(),
            appleProfile.Id);
        CollectionAssert.Contains(
            (await Index().GetMatchingProfileIds(bananaResourceId)).ToList(),
            bananaProfile.Id,
            "The first snapshot must not silently consume an invalidation that arrived later");

        await RunPendingIncrementalUpdate();

        CollectionAssert.Contains(
            (await Index().GetMatchingProfileIds(bananaResourceId)).ToList(),
            dateProfile.Id);
        CollectionAssert.DoesNotContain(
            (await Index().GetMatchingProfileIds(bananaResourceId)).ToList(),
            bananaProfile.Id);
    }

    private sealed class ControllableResourceSearchIndexService(ResourceSearchIndexService inner)
        : IResourceSearchIndexService
    {
        private BarrierGate? _nextBarrier;

        public bool IsReady => inner.IsReady;
        public long Version => inner.Version;
        public DateTime LastUpdatedAt => inner.LastUpdatedAt;

        public BarrierGate BlockNextBarrier()
        {
            var gate = new BarrierGate();
            if (Interlocked.CompareExchange(ref _nextBarrier, gate, null) != null)
            {
                throw new InvalidOperationException("A search-index barrier is already blocked");
            }

            return gate;
        }

        public Task<HashSet<int>?> SearchResourceIdsAsync(ResourceSearchFilterGroup? group) =>
            inner.SearchResourceIdsAsync(group);

        public Task<Dictionary<string, int>?> GetPropertyValueResourceCountsAsync(
            PropertyPool pool,
            int propertyId,
            IEnumerable<string> valueIds,
            IReadOnlySet<int>? resourceIds = null) =>
            inner.GetPropertyValueResourceCountsAsync(pool, propertyId, valueIds, resourceIds);

        public void InvalidateResource(int resourceId) => inner.InvalidateResource(resourceId);
        public void InvalidateResources(IEnumerable<int> resourceIds) => inner.InvalidateResources(resourceIds);
        public void RemoveResource(int resourceId) => inner.RemoveResource(resourceId);
        public void RemoveResources(IEnumerable<int> resourceIds) => inner.RemoveResources(resourceIds);

        public async Task WaitForPendingUpdatesAsync(CancellationToken ct = default)
        {
            var gate = Interlocked.Exchange(ref _nextBarrier, null);
            if (gate != null)
            {
                gate.MarkEntered();
                await gate.WaitForRelease(ct);
            }

            await inner.WaitForPendingUpdatesAsync(ct);
        }

        public Task RebuildAllAsync(CancellationToken ct = default) => inner.RebuildAllAsync(ct);
        public Task WaitForReadyAsync(TimeSpan? timeout = null) => inner.WaitForReadyAsync(timeout);
        public ResourceSearchIndexStatus GetStatus() => inner.GetStatus();

        public sealed class BarrierGate
        {
            private readonly TaskCompletionSource _entered =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _release =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Entered => _entered.Task;

            public void MarkEntered() => _entered.TrySetResult();
            public void Release() => _release.TrySetResult();
            public Task WaitForRelease(CancellationToken ct) => _release.Task.WaitAsync(ct);
        }
    }
}
