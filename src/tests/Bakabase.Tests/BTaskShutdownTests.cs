using System;
using System.Threading;
using System.Threading.Tasks;
using Bakabase.Abstractions.Components.Tasks;
using Bakabase.Abstractions.Models.Domain.Constants;
using Bakabase.TestKit.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Bakabase.Tests;

/// <summary>
/// Covers <see cref="BTaskManager.PrepareForShutdown"/> — the step the exit flow uses to ask
/// background work to wind down the moment the user commits to quitting, rather than waiting for
/// disposal (which only runs once the web host has stopped, many seconds later).
/// </summary>
[TestClass]
public sealed class BTaskShutdownTests
{
    private IServiceProvider _sp = null!;
    private BTaskManager _btm = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _sp = await TestServiceBuilder.BuildServiceProvider();
        _btm = _sp.GetRequiredService<BTaskManager>();
    }

    private async Task WaitForStatus(string id, BTaskStatus status, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (_btm.GetTaskViewModel(id)?.Status == status) return;
            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Task {id} did not reach {status} within {timeout} " +
            $"(last seen: {_btm.GetTaskViewModel(id)?.Status.ToString() ?? "gone"})");
    }

    [TestMethod]
    public async Task PrepareForShutdown_CancelsRunningNonCriticalTask()
    {
        const string id = "shutdown-non-critical";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _btm.Enqueue(BTaskBuilder.Create(id)
            .Run(async args =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, args.CancellationToken);
            }));
        await _btm.Start(id);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await _btm.PrepareForShutdown();

        await WaitForStatus(id, BTaskStatus.Cancelled, TimeSpan.FromSeconds(5));
    }

    [TestMethod]
    public async Task PrepareForShutdown_LeavesCriticalTaskRunning()
    {
        const string id = "shutdown-critical";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _btm.Enqueue(BTaskBuilder.Create(id)
            .Critical()
            .Run(async args =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, args.CancellationToken);
            }));
        await _btm.Start(id);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await _btm.PrepareForShutdown();

        // Interrupting a critical task is what loses data — the exit flow waits for these instead,
        // and offers the user "Quit now" if the wait drags on.
        await Task.Delay(300);
        Assert.AreEqual(BTaskStatus.Running, _btm.GetTaskViewModel(id)!.Status);

        await _btm.Stop(id);
    }

    [TestMethod]
    public async Task PrepareForShutdown_IsIdempotent()
    {
        const string id = "shutdown-twice";
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await _btm.Enqueue(BTaskBuilder.Create(id)
            .Run(async args =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, args.CancellationToken);
            }));
        await _btm.Start(id);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // DisposeAsync runs the same step, so the exit path always calls it twice.
        await _btm.PrepareForShutdown();
        await _btm.PrepareForShutdown();

        await WaitForStatus(id, BTaskStatus.Cancelled, TimeSpan.FromSeconds(5));
    }
}
