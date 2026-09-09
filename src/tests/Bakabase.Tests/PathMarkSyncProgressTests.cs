using Bakabase.Abstractions.Models.Domain;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.Abstractions.Services;
using Bakabase.InsideWorld.Business.Services;
using Bakabase.TestKit.Utils;
using Bootstrap.Components.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests;

[TestClass]
public class PathMarkSyncProgressTests
{
    [TestMethod]
    public async Task SyncMarks_ReportsLocalizedIndexBarrierBeforeCleanup()
    {
        var searchIndex = new BlockingResourceSearchIndexService();
        var sp = await TestServiceBuilder.BuildServiceProvider(services =>
        {
            services.RemoveAll<IResourceSearchIndexService>();
            services.AddSingleton<IResourceSearchIndexService>(searchIndex);
        });

        await sp.GetRequiredService<IPathMarkService>().Add(new PathMark
        {
            Path = Path.GetTempPath(),
            Type = PathMarkType.Property,
            ConfigJson =
                "{\"matchMode\":1,\"layer\":0,\"pool\":4,\"propertyId\":999999,\"valueType\":1," +
                "\"fixedValue\":\"unused\",\"applyScope\":1}",
        });

        var progress = new List<int>();
        var processes = new List<string?>();
        var syncTask = sp.GetRequiredService<PathMarkSyncService>().SyncMarks(
            percentage =>
            {
                progress.Add(percentage);
                return Task.CompletedTask;
            },
            process =>
            {
                processes.Add(process);
                return Task.CompletedTask;
            },
            new PauseToken(),
            CancellationToken.None);

        try
        {
            await searchIndex.BarrierEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

            progress.Should().EndWith(90);
            progress.Should().OnlyContain(p => p < 95);
            processes.Should().EndWith("SyncPathMark_UpdatingSearchIndex");
        }
        finally
        {
            searchIndex.ReleaseBarrier.TrySetResult();
        }

        await syncTask.WaitAsync(TimeSpan.FromSeconds(10));

        progress.Should().BeInAscendingOrder();
        progress.Should().ContainInOrder(80, 90, 95, 100);
        processes.Should().ContainInOrder(
            "SyncPathMark_CollectingPropertyEffects",
            "SyncPathMark_CollectingMediaLibraryEffects",
            "SyncPathMark_ComputingFinalState",
            "SyncPathMark_ApplyingPropertyChanges",
            "SyncPathMark_ApplyingMediaLibraryChanges",
            "SyncPathMark_PersistingEffects",
            "SyncPathMark_UpdatingSearchIndex",
            "SyncPathMark_UpdatingMarkStatuses",
            "SyncPathMark_Complete");
    }

    private sealed class BlockingResourceSearchIndexService : IResourceSearchIndexService
    {
        public TaskCompletionSource BarrierEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseBarrier { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool IsReady => true;
        public long Version => 0;
        public DateTime LastUpdatedAt => DateTime.MinValue;

        public Task<HashSet<int>?> SearchResourceIdsAsync(ResourceSearchFilterGroup? group) =>
            Task.FromResult<HashSet<int>?>(null);

        public Task<Dictionary<string, int>?> GetPropertyValueResourceCountsAsync(PropertyPool pool, int propertyId,
            IEnumerable<string> valueIds, IReadOnlySet<int>? resourceIds = null) =>
            Task.FromResult<Dictionary<string, int>?>(null);

        public void InvalidateResource(int resourceId)
        {
        }

        public void InvalidateResources(IEnumerable<int> resourceIds)
        {
        }

        public void RemoveResource(int resourceId)
        {
        }

        public void RemoveResources(IEnumerable<int> resourceIds)
        {
        }

        public async Task WaitForPendingUpdatesAsync(CancellationToken ct = default)
        {
            BarrierEntered.TrySetResult();
            await ReleaseBarrier.Task.WaitAsync(ct);
        }

        public Task RebuildAllAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task WaitForReadyAsync(TimeSpan? timeout = null) => Task.CompletedTask;

        public ResourceSearchIndexStatus GetStatus() => new()
        {
            IsReady = true,
        };
    }
}
