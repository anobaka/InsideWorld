using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Bakabase.Modules.RemoteAccess.Abstractions.Components;
using Bakabase.Modules.RemoteAccess.Abstractions.Models;
using Bakabase.Modules.RemoteAccess.Components.Pairing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Bakabase.Tests.RemoteAccess;

[TestClass]
public class RemoteDeviceStoreTests
{
    private string _root = null!;

    private sealed class TempDirectory(string path) : IRemoteAccessDataDirectory
    {
        public string Path => path;
        public string Ensure() => Directory.CreateDirectory(path).FullName;
    }

    private string RemoteAccessDir => Path.Combine(_root, "remote-access");

    private RemoteDeviceStore Build() => new(new TempDirectory(RemoteAccessDir));

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "bakabase-store-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    [TestMethod]
    public void Reading_creates_nothing_on_disk()
    {
        // The all-in-one ships with remote access off. A user who never turns it on must
        // not find a remote-access folder appearing in their data directory.
        var store = Build();

        var data = store.Read();

        Assert.AreEqual(0, data.Devices.Count);
        Assert.IsFalse(store.Exists);
        Assert.IsFalse(Directory.Exists(RemoteAccessDir), "reading created the directory");
    }

    [TestMethod]
    public async Task Writing_creates_the_directory_and_the_file()
    {
        var store = Build();

        await store.MutateAsync(d => d.Devices.Add(NewDevice("dev-1")));

        Assert.IsTrue(Directory.Exists(RemoteAccessDir));
        Assert.IsTrue(store.Exists);
        Assert.IsTrue(File.Exists(Path.Combine(RemoteAccessDir, RemoteDeviceStore.FileName)));
    }

    [TestMethod]
    public async Task A_second_store_reads_back_what_the_first_wrote()
    {
        await Build().MutateAsync(d =>
        {
            d.Devices.Add(NewDevice("dev-1"));
            d.SigningSecret = "secret";
        });

        var reloaded = Build().Read();

        Assert.AreEqual(1, reloaded.Devices.Count);
        Assert.AreEqual("dev-1", reloaded.Devices[0].Id);
        Assert.AreEqual(RemoteDevicePlatform.Windows, reloaded.Devices[0].Platform);
        Assert.AreEqual("secret", reloaded.SigningSecret);
    }

    [TestMethod]
    public async Task No_temporary_file_is_left_behind()
    {
        var store = Build();
        await store.MutateAsync(d => d.Devices.Add(NewDevice("dev-1")));

        var leftovers = Directory.GetFiles(RemoteAccessDir).Where(f => f.EndsWith(".tmp")).ToArray();
        Assert.AreEqual(0, leftovers.Length, string.Join(", ", leftovers));
    }

    [TestMethod]
    public void A_corrupt_file_reads_as_empty_rather_than_throwing()
    {
        // Losing pairings is visible and recoverable; refusing to start is not.
        Directory.CreateDirectory(RemoteAccessDir);
        File.WriteAllText(Path.Combine(RemoteAccessDir, RemoteDeviceStore.FileName), "{ not json");

        var data = Build().Read();

        Assert.AreEqual(0, data.Devices.Count);
    }

    [TestMethod]
    public async Task Concurrent_mutations_do_not_lose_writes()
    {
        var store = Build();

        await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => store.MutateAsync(d => d.Devices.Add(NewDevice($"dev-{i}")))));

        Assert.AreEqual(20, store.Read().Devices.Count);
        Assert.AreEqual(20, Build().Read().Devices.Count);
    }

    [TestMethod]
    public async Task Mutate_can_return_a_value()
    {
        var store = Build();

        var id = await store.MutateAsync(d =>
        {
            var device = NewDevice("dev-1");
            d.Devices.Add(device);
            return device.Id;
        });

        Assert.AreEqual("dev-1", id);
    }

    private static RemoteDevice NewDevice(string id) => new()
    {
        Id = id,
        Name = "Test",
        Platform = RemoteDevicePlatform.Windows,
        Key = RemoteRequestSignature.ToBase64Url(RemoteRequestSignature.NewDeviceKey()),
        CreatedAt = DateTime.UtcNow
    };
}
