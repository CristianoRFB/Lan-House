using System.Text.Json;
using Adrenalina.Application;

namespace Adrenalina.Infrastructure;

public sealed class JsonClientRuntimeStore(LocalClientStoragePaths paths) : IClientRuntimeStore
{
    private readonly SemaphoreSlim _sync = new(1, 1);

    public async Task<ClientRuntimeState> LoadStateAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();

            if (!File.Exists(paths.StateFilePath))
            {
                var state = new ClientRuntimeState
                {
                    MachineName = Environment.MachineName,
                    IsLocked = true,
                    LockMessage = "Faça login para liberar a máquina.",
                    SessionMessage = "Máquina bloqueada aguardando sincronização com o servidor."
                };

                await SaveStateInternalAsync(state, cancellationToken);
                return state;
            }

            var json = await File.ReadAllTextAsync(paths.StateFilePath, cancellationToken);
            return JsonSerializer.Deserialize<ClientRuntimeState>(json, JsonDefaults.Options) ?? new ClientRuntimeState();
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task SaveStateAsync(ClientRuntimeState state, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            await SaveStateInternalAsync(state, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<IReadOnlyList<ClientShellRequest>> DrainRequestsAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();

            if (!File.Exists(paths.RequestQueueFilePath))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(paths.RequestQueueFilePath, cancellationToken);
            var items = JsonSerializer.Deserialize<List<ClientShellRequest>>(json, JsonDefaults.Options) ?? [];
            await File.WriteAllTextAsync(paths.RequestQueueFilePath, "[]", cancellationToken);
            return items;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task EnqueueRequestAsync(ClientShellRequest request, CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();

            List<ClientShellRequest> items = [];
            if (File.Exists(paths.RequestQueueFilePath))
            {
                var json = await File.ReadAllTextAsync(paths.RequestQueueFilePath, cancellationToken);
                items = JsonSerializer.Deserialize<List<ClientShellRequest>>(json, JsonDefaults.Options) ?? [];
            }

            items.Add(request);
            var payload = JsonSerializer.Serialize(items, JsonDefaults.Options);
            await File.WriteAllTextAsync(paths.RequestQueueFilePath, payload, cancellationToken);
        }
        finally
        {
            _sync.Release();
        }
    }

    private void EnsureDirectories()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.StateFilePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.RequestQueueFilePath)!);
    }

    private async Task SaveStateInternalAsync(ClientRuntimeState state, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(state, JsonDefaults.Options);
        await File.WriteAllTextAsync(paths.StateFilePath, payload, cancellationToken);
    }
}
