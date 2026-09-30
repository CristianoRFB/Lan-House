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
                var state = CreateInitialState();

                await SaveStateInternalAsync(state, cancellationToken);
                return state;
            }

            var json = await File.ReadAllTextAsync(paths.StateFilePath, cancellationToken);
            try
            {
                var state = JsonSerializer.Deserialize<ClientRuntimeState>(json, JsonDefaults.Options);
                if (state is null || string.IsNullOrWhiteSpace(state.MachineName))
                {
                    throw new JsonException("O estado local do cliente esta incompleto.");
                }

                return state;
            }
            catch (JsonException)
            {
                File.Move(paths.StateFilePath, paths.StateFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                var state = CreateInitialState();
                await SaveStateInternalAsync(state, cancellationToken);
                return state;
            }
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

            var items = await ReadRequestsInternalAsync(cancellationToken);
            await WriteAtomicallyAsync(paths.RequestQueueFilePath, "[]", cancellationToken);
            return items;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task<IReadOnlyList<ClientShellRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            if (!File.Exists(paths.RequestQueueFilePath))
            {
                return [];
            }

            var items = await ReadRequestsInternalAsync(cancellationToken);
            // Persist identifiers added to requests created by older versions before sending them.
            if (items.Count > 0)
            {
                await WriteAtomicallyAsync(paths.RequestQueueFilePath, JsonSerializer.Serialize(items, JsonDefaults.Options), cancellationToken);
            }
            return items;
        }
        finally
        {
            _sync.Release();
        }
    }

    public async Task RemoveRequestsAsync(IReadOnlyCollection<Guid> requestIds, CancellationToken cancellationToken = default)
    {
        if (requestIds.Count == 0)
        {
            return;
        }

        await _sync.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectories();
            if (!File.Exists(paths.RequestQueueFilePath))
            {
                return;
            }

            var items = await ReadRequestsInternalAsync(cancellationToken);
            var ids = requestIds.ToHashSet();
            items.RemoveAll(item => ids.Contains(item.Id));
            await WriteAtomicallyAsync(paths.RequestQueueFilePath, JsonSerializer.Serialize(items, JsonDefaults.Options), cancellationToken);
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

            var items = await ReadRequestsInternalAsync(cancellationToken);

            items.Add(request);
            var payload = JsonSerializer.Serialize(items, JsonDefaults.Options);
            await WriteAtomicallyAsync(paths.RequestQueueFilePath, payload, cancellationToken);
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
        await WriteAtomicallyAsync(paths.StateFilePath, payload, cancellationToken);
    }

    private async Task<List<ClientShellRequest>> ReadRequestsInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.RequestQueueFilePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(paths.RequestQueueFilePath, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<List<ClientShellRequest>>(json, JsonDefaults.Options) ?? [];
        }
        catch (JsonException)
        {
            File.Move(paths.RequestQueueFilePath, paths.RequestQueueFilePath + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            return [];
        }
    }

    private static ClientRuntimeState CreateInitialState() => new()
    {
        MachineName = Environment.MachineName,
        IsLocked = true,
        LockMessage = "Faça login para liberar a máquina.",
        SessionMessage = "Máquina bloqueada aguardando sincronização com o servidor."
    };

    private static async Task WriteAtomicallyAsync(string path, string payload, CancellationToken cancellationToken)
    {
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, payload, cancellationToken);
        File.Move(temporaryPath, path, true);
    }
}
