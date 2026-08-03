using Adrenalina.Application;
using Adrenalina.Client;
using Adrenalina.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Adrenalina.Tests;

public sealed class ClientIsolationTests
{
    [Fact]
    public async Task ClientSettingsAndRuntimeUseIsolatedTemporaryFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        var settingsPath = Path.Combine(root, "Client", "clientsettings.json");
        var statePath = Path.Combine(root, "Runtime", "PC-TESTE", "client-state.json");
        var queuePath = Path.Combine(root, "Runtime", "PC-TESTE", "client-requests.json");

        try
        {
            var options = ClientOptionsStore.LoadOrCreate(settingsPath);
            options.ServerBaseUrl = "http://127.0.0.1:59999/";
            options.MachineName = "PC-TESTE";
            options.MachineKey = "pc-teste";
            options.SetupCompleted = true;
            ClientOptionsStore.Save(options, settingsPath);

            var loaded = ClientOptionsStore.LoadOrCreate(settingsPath);
            Assert.Equal("PC-TESTE", loaded.MachineName);
            Assert.True(loaded.SetupCompleted);

            await File.WriteAllTextAsync(settingsPath, "{configuracao-invalida");
            var recoveredOptions = ClientOptionsStore.LoadOrCreate(settingsPath);
            Assert.False(recoveredOptions.SetupCompleted);
            Assert.Single(Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "clientsettings.json.corrupt-*"));

            var store = new JsonClientRuntimeStore(new LocalClientStoragePaths
            {
                StateFilePath = statePath,
                RequestQueueFilePath = queuePath
            });
            await store.SaveStateAsync(new ClientRuntimeState { MachineName = "PC-TESTE", RemainingMinutes = 42 });
            var state = await store.LoadStateAsync();
            Assert.Equal(42, state.RemainingMinutes);

            await store.EnqueueRequestAsync(new ClientShellRequest { Type = Adrenalina.Domain.ClientRequestType.MoreTime });
            Assert.Single(await store.DrainRequestsAsync());
            Assert.Empty(await store.DrainRequestsAsync());

            Assert.NotEqual(AdrenalinaPaths.GetAdminDataRoot(), AdrenalinaPaths.GetClientSettingsRoot());
            Assert.DoesNotContain("Admin", Path.GetFullPath(settingsPath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task UnavailableServerReturnsClearFailureWithoutTouchingTheMachine()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var gateway = new ClientServerGateway(
                new ClientConnectionOptions(),
                new FixedHttpClientFactory(),
                new JsonClientRuntimeStore(new LocalClientStoragePaths
                {
                    StateFilePath = Path.Combine(root, "state.json"),
                    RequestQueueFilePath = Path.Combine(root, "requests.json")
                }),
                NullLogger<ClientServerGateway>.Instance);

            var result = await gateway.TestConnectionAsync("http://127.0.0.1:1/");
            Assert.False(result.Success);
            Assert.Contains("servidor", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CorruptLocalStateIsPreservedAndRecovered()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        var statePath = Path.Combine(root, "state.json");
        var queuePath = Path.Combine(root, "requests.json");
        try
        {
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(statePath, "{arquivo-invalido");
            var store = new JsonClientRuntimeStore(new LocalClientStoragePaths
            {
                StateFilePath = statePath,
                RequestQueueFilePath = queuePath
            });

            var recovered = await store.LoadStateAsync();

            Assert.True(recovered.IsLocked);
            Assert.True(File.Exists(statePath));
            Assert.Single(Directory.GetFiles(root, "state.json.corrupt-*"));
            Assert.False(File.Exists(statePath + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FixedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
