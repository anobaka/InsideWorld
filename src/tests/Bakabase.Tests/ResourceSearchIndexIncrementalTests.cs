using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Events;
using Bakabase.Abstractions.Components.Localization;
using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Business.Components.Search.Index;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.InsideWorld.Models.Constants.Aos;
using Bakabase.Modules.Property.Abstractions.Services;
using Bakabase.TestKit.Utils;
using Bootstrap.Components.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bakabase.Tests;

/// <summary>
/// Integration coverage for the inverted index's incremental update path —
/// RemoveResource(s) / InvalidateResource(s) and the batched background processor.
/// Earlier tests only exercised a full RebuildAllAsync.
/// </summary>
[TestClass]
public sealed class ResourceSearchIndexIncrementalTests
{
    private string _testRoot = null!;
    private IServiceProvider _sp = null!;
    private Property _filenameProperty = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _sp = await TestServiceBuilder.BuildServiceProvider();
        _testRoot = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            $"IndexIncrementalTests.{DateTime.Now:yyyyMMddHHmmssfff}.{Guid.NewGuid():N}");
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

        var internalProps = await _sp.GetRequiredService<IPropertyService>().GetProperties(PropertyPool.Internal);
        _filenameProperty = internalProps.First(p => p.Id == (int)InternalProperty.Filename);
    }

    private IResourceSearchIndexService Index() => _sp.GetRequiredService<IResourceSearchIndexService>();

    private async Task BuildIndex()
    {
        var idx = Index();
        await idx.RebuildAllAsync(CancellationToken.None);
        await idx.WaitForReadyAsync(TimeSpan.FromSeconds(10));
        await idx.WaitForPendingUpdatesAsync(CancellationToken.None);
    }

    private async Task WaitForPendingUpdatesAndAssertCount(int expected)
    {
        await Index().WaitForPendingUpdatesAsync(CancellationToken.None);
        Assert.AreEqual(expected, Index().GetStatus().TotalResourceCount);
    }

    private async Task<int> ResourceId(string filename)
        => (await _sp.GetRequiredService<IResourceService>().GetAll()).Single(r => r.FileName == filename).Id;

    private async Task<int> SearchCountByFilename(string value)
    {
        var resp = await _sp.GetRequiredService<IResourceService>().Search(new ResourceSearch
        {
            PageSize = 100,
            Group = new ResourceSearchFilterGroup
            {
                Combinator = SearchCombinator.And,
                Filters =
                [
                    new ResourceSearchFilter
                    {
                        PropertyPool = PropertyPool.Internal,
                        PropertyId = (int)InternalProperty.Filename,
                        Operation = SearchOperation.Equals,
                        DbValue = value,
                        Property = _filenameProperty
                    }
                ]
            }
        });
        return resp.Data!.Count;
    }

    [TestMethod]
    public async Task RemoveResource_RemovesFromIndex()
    {
        await Seed("a", "b", "c");
        await BuildIndex();
        Assert.AreEqual(3, Index().GetStatus().TotalResourceCount);

        Index().RemoveResource(await ResourceId("a"));
        await WaitForPendingUpdatesAndAssertCount(2);
    }

    [TestMethod]
    public async Task RemoveResource_RemovedResourceDropsOutOfSearch()
    {
        await Seed("apple", "banana");
        await BuildIndex();
        Assert.AreEqual(1, await SearchCountByFilename("apple"));

        Index().RemoveResource(await ResourceId("apple"));
        await WaitForPendingUpdatesAndAssertCount(1);

        Assert.AreEqual(0, await SearchCountByFilename("apple"));
    }

    [TestMethod]
    public async Task RemoveResources_BatchRemovesEveryGivenResource()
    {
        await Seed("a", "b", "c");
        await BuildIndex();

        Index().RemoveResources([await ResourceId("a"), await ResourceId("b")]);
        await WaitForPendingUpdatesAndAssertCount(1);
    }

    [TestMethod]
    public async Task RemoveResource_LeavesOtherResourcesSearchable()
    {
        await Seed("apple", "banana");
        await BuildIndex();

        Index().RemoveResource(await ResourceId("apple"));
        await WaitForPendingUpdatesAndAssertCount(1);

        Assert.AreEqual(1, await SearchCountByFilename("banana"));
    }

    [TestMethod]
    public async Task InvalidateResource_ReindexesResourceFromDatabase()
    {
        await Seed("a", "b");
        await BuildIndex();
        var id = await ResourceId("a");

        Index().RemoveResource(id);
        await WaitForPendingUpdatesAndAssertCount(1);

        // The resource still exists in the database; invalidation must re-read and re-index it.
        Index().InvalidateResource(id);
        await WaitForPendingUpdatesAndAssertCount(2);
    }

    [TestMethod]
    public async Task IndexVersion_AdvancesOnIncrementalUpdate()
    {
        await Seed("a", "b");
        await BuildIndex();
        var versionBefore = Index().Version;

        Index().RemoveResource(await ResourceId("a"));
        await WaitForPendingUpdatesAndAssertCount(1);

        Assert.IsTrue(Index().Version > versionBefore);
    }

    [TestMethod]
    public async Task WaitForPendingUpdates_WaitsAcrossBatchBoundary()
    {
        await Seed("a", "b");
        await BuildIndex();
        var id = await ResourceId("a");

        for (var i = 0; i < 100; i++)
        {
            Index().InvalidateResource(id);
        }
        Index().RemoveResource(id);

        // The remove is the 101st operation, so it must be processed in the batch after
        // the first 100 invalidations before the FIFO barrier can complete.
        await WaitForPendingUpdatesAndAssertCount(1);
        Assert.AreEqual(0, await SearchCountByFilename("a"));
    }

    [TestMethod]
    public async Task IncrementalBatch_RetriesThenFallsBackUntilSuccessfulRebuild()
    {
        await Seed("a");
        var scopeFactory = new FailNextScopeFactory(_sp.GetRequiredService<IServiceScopeFactory>());
        var index = new ResourceSearchIndexService(
            scopeFactory,
            _sp.GetRequiredService<IResourceDataChangeEvent>(),
            _sp.GetRequiredService<ILogger<ResourceSearchIndexService>>(),
            _sp.GetRequiredService<IBakabaseLocalizer>());

        await index.RebuildAllAsync(CancellationToken.None);
        var versionBeforeRetry = index.Version;
        scopeFactory.FailNext(2);

        index.InvalidateResource(await ResourceId("a"));
        await index.WaitForPendingUpdatesAsync(CancellationToken.None);

        Assert.IsTrue(index.IsReady);
        Assert.AreEqual(versionBeforeRetry + 1, index.Version,
            "two failed attempts followed by a successful retry must advance the version once");

        var versionBeforeFailure = index.Version;
        scopeFactory.FailNext(3);

        index.InvalidateResource(await ResourceId("a"));
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => index.WaitForPendingUpdatesAsync(CancellationToken.None));

        Assert.IsFalse(index.IsReady);
        Assert.AreEqual(versionBeforeFailure, index.Version,
            "a failed batch must not advance the observable index version");

        var filter = new ResourceSearchFilterGroup
        {
            Combinator = SearchCombinator.And,
            Filters =
            [
                new ResourceSearchFilter
                {
                    PropertyPool = PropertyPool.Internal,
                    PropertyId = (int)InternalProperty.Filename,
                    Operation = SearchOperation.Equals,
                    DbValue = "a",
                    Property = _filenameProperty
                }
            ]
        };
        Assert.IsNull(await index.SearchResourceIdsAsync(filter),
            "an index with an unrecovered batch failure must force the caller to full-scan");

        await index.RebuildAllAsync(CancellationToken.None);
        await index.WaitForReadyAsync(TimeSpan.FromSeconds(10));

        Assert.IsTrue(index.IsReady);
        CollectionAssert.AreEqual(
            new[] {await ResourceId("a")},
            (await index.SearchResourceIdsAsync(filter))!.OrderBy(x => x).ToArray());
    }

    private sealed class FailNextScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _remainingFailures;

        public void FailNext(int count) => Volatile.Write(ref _remainingFailures, count);

        public IServiceScope CreateScope()
        {
            if (Interlocked.Decrement(ref _remainingFailures) >= 0)
            {
                throw new InvalidOperationException("Injected scope creation failure");
            }

            return inner.CreateScope();
        }
    }
}
