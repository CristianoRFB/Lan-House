using Adrenalina.Application;
using Adrenalina.Domain;
using Adrenalina.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Adrenalina.Admin;

public sealed class EmbeddedAdminServer : IAsyncDisposable
{
    private const int DefaultPort = 5076;
    private const int MaxFallbackPortOffset = 19;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _contentRootPath;
    private readonly string _dataRootPath;
    private WebApplication? _app;
    private int _currentPort = DefaultPort;

    public EmbeddedAdminServer()
    {
        _contentRootPath = Path.Combine(AppContext.BaseDirectory, "ServerContent");
        _dataRootPath = AdrenalinaPaths.GetAdminDataRoot();
    }

    public Uri BaseAddress => new($"http://127.0.0.1:{_currentPort}/");

    public string ListenUrl => $"http://0.0.0.0:{_currentPort}";

    public int Port => BaseAddress.Port;

    public bool IsRunning => _app is not null;

    public bool UsedFallbackPort { get; private set; }

    public string StartupMessage { get; private set; } = "Servidor local pronto.";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_app is not null)
            {
                return;
            }

            Directory.CreateDirectory(_contentRootPath);
            EnsureAdminDataMigrated();
            Directory.CreateDirectory(_dataRootPath);
            _currentPort = DefaultPort;
            UsedFallbackPort = false;
            StartupMessage = "Inicializando servidor local...";

            Exception? lastAddressInUseException = null;

            foreach (var candidatePort in AdminPortResolver.GetCandidatePorts(DefaultPort, MaxFallbackPortOffset))
            {
                if (!AdminPortResolver.IsPortAvailable(candidatePort))
                {
                    continue;
                }

                _currentPort = candidatePort;
                var app = AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
                {
                    ContentRootPath = _contentRootPath,
                    WebRootPath = Path.Combine(_contentRootPath, "wwwroot"),
                    DataRootPath = _dataRootPath,
                    Urls = ListenUrl,
                    UseHttpsRedirection = false
                });

                try
                {
                    await AdrenalinaServerBootstrap.InitializeAsync(app, cancellationToken);
                    await app.StartAsync(cancellationToken);

                    _app = app;
                    UsedFallbackPort = candidatePort != DefaultPort;
                    StartupMessage = UsedFallbackPort
                        ? $"A porta {DefaultPort} estava ocupada. O ADMIN iniciou na porta {_currentPort} nesta execucao."
                        : $"Servidor local iniciado na porta {_currentPort}.";
                    return;
                }
                catch (Exception exception) when (AdminPortResolver.IsAddressInUse(exception))
                {
                    lastAddressInUseException = exception;
                    await app.DisposeAsync();
                }
                catch
                {
                    await app.DisposeAsync();
                    throw;
                }
            }

            throw new InvalidOperationException(
                $"Nao foi possivel iniciar o servidor local. As portas entre {DefaultPort} e {DefaultPort + MaxFallbackPortOffset} estao ocupadas.",
                lastAddressInUseException);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_app is null)
            {
                return;
            }

            await _app.StopAsync(cancellationToken);
            await _app.DisposeAsync();
            _app = null;
            _currentPort = DefaultPort;
            UsedFallbackPort = false;
            StartupMessage = "Servidor local parado.";
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task<DashboardDto?> TryGetDashboardAsync(CancellationToken cancellationToken = default)
    {
        return await RunScopedAsync(service => service.GetDashboardAsync(cancellationToken), cancellationToken);
    }

    public async Task<OperationResult> CreateManualBackupAsync(CancellationToken cancellationToken = default)
    {
        return await RunScopedAsync(
                   async service => await service.CreateManualBackupAsync(await ResolveAuditActorAsync(service, cancellationToken), cancellationToken),
                   cancellationToken)
               ?? new OperationResult(false, "O servidor nao esta em execucao.");
    }

    public async Task<SettingsDto?> TryGetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return await RunScopedAsync(service => service.GetSettingsAsync(cancellationToken), cancellationToken);
    }

    private async Task<Guid> ResolveAuditActorAsync(ICafeManagementService service, CancellationToken cancellationToken)
    {
        var users = await service.GetUsersAsync(cancellationToken);
        return users.FirstOrDefault(entry => entry.ProfileType == UserProfileType.Admin)?.Id
               ?? users.FirstOrDefault(entry => entry.ProfileType == UserProfileType.Special)?.Id
               ?? Guid.Empty;
    }

    private async Task<T?> RunScopedAsync<T>(Func<ICafeManagementService, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (_app is null)
        {
            return default;
        }

        using var scope = _app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        return await operation(service);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _lifecycleGate.Dispose();
    }

    private void EnsureAdminDataMigrated()
    {
        var currentDatabasePath = Path.Combine(_dataRootPath, "adrenalina.db");
        if (File.Exists(currentDatabasePath))
        {
            return;
        }

        var legacyRoot = AdrenalinaPaths.GetLegacyAdminDataRoot();
        var legacyDatabasePath = Path.Combine(legacyRoot, "adrenalina.db");
        if (!File.Exists(legacyDatabasePath))
        {
            return;
        }

        CopyDirectory(legacyRoot, _dataRootPath);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(file));
            if (!File.Exists(destinationPath))
            {
                File.Copy(file, destinationPath);
            }
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(directory));
            CopyDirectory(directory, destinationPath);
        }
    }
}
