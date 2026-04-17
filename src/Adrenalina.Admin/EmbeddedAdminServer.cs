using Adrenalina.Application;
using Adrenalina.Domain;
using Adrenalina.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Adrenalina.Admin;

public sealed class EmbeddedAdminServer : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly string _contentRootPath;
    private readonly string _dataRootPath;
    private WebApplication? _app;

    public EmbeddedAdminServer()
    {
        var commonDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Adrenalina");

        _contentRootPath = Path.Combine(AppContext.BaseDirectory, "ServerContent");
        _dataRootPath = Path.Combine(commonDataPath, "admin-data");
        BaseAddress = new Uri("http://127.0.0.1:5076/");
    }

    public Uri BaseAddress { get; }

    public bool IsRunning => _app is not null;

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
            Directory.CreateDirectory(_dataRootPath);

            var app = AdrenalinaServerBootstrap.BuildApplication(new AdrenalinaServerHostOptions
            {
                ContentRootPath = _contentRootPath,
                WebRootPath = Path.Combine(_contentRootPath, "wwwroot"),
                DataRootPath = _dataRootPath,
                Urls = BaseAddress.GetLeftPart(UriPartial.Authority),
                UseHttpsRedirection = false
            });

            await app.StartAsync(cancellationToken);

            using var scope = app.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
            await service.EnsureInitializedAsync(cancellationToken);

            _app = app;
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
               ?? new OperationResult(false, "O servidor não está em execução.");
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
}
