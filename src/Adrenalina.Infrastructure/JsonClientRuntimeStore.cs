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

            try
            {
                var json = await File.ReadAllTextAsync(paths.StateFilePath, cancellationToken);
                return JsonSerializer.Deserialize<ClientRuntimeState>(json, JsonDefaults.Options) ?? CreateDefaultState();
            }
            catch (JsonException)
            {
                PreserveCorruptFile(paths.StateFilePath);
                var state = CreateDefaultState();
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

            List<ClientShellRequest> items;
            try
            {
                var json = await File.ReadAllTextAsync(paths.RequestQueueFilePath, cancellationToken);
                items = JsonSerializer.Deserialize<List<ClientShellRequest>>(json, JsonDefaults.Options) ?? [];
            }
            catch (JsonException)
            {
                PreserveCorruptFile(paths.RequestQueueFilePath);
                items = [];
            }

            await WriteAtomicallyAsync(paths.RequestQueueFilePath, "[]", cancellationToken);
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

    private static async Task WriteAtomicallyAsync(string targetPath, string payload, CancellationToken cancellationToken)
    {
        var temporaryPath = targetPath + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, payload, cancellationToken);
        File.Move(temporaryPath, targetPath, overwrite: true);
    }

    private static void PreserveCorruptFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var preservedPath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
        File.Move(path, preservedPath);
    }

    private static ClientRuntimeState CreateDefaultState() => new()
    {
        MachineName = Environment.MachineName,
        IsLocked = true,
        LockMessage = "Faça login para liberar a máquina.",
        SessionMessage = "Máquina aguardando sincronização com o servidor."
    };
}
