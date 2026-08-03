using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Adrenalina.Tests;

internal sealed class TestEnvironment : IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private TestEnvironment(string rootPath, ServiceProvider provider, string initialAdminPassword)
    {
        RootPath = rootPath;
        _provider = provider;
        InitialAdminPassword = initialAdminPassword;
    }

    public string RootPath { get; }
    public string DatabasePath => Path.Combine(RootPath, "adrenalina.db");
    public string InitialAdminPassword { get; }

    public static async Task<TestEnvironment> CreateAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "Adrenalina.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var storage = new AdrenalinaStoragePaths
        {
            DatabaseFilePath = Path.Combine(root, "adrenalina.db"),
            BackupDirectory = Path.Combine(root, "backups"),
            LogDirectory = Path.Combine(root, "logs"),
            ClientRuntimeDirectory = Path.Combine(root, "runtime")
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(storage);
        services.AddDbContext<AdrenalinaDbContext>(options => options.UseSqlite($"Data Source={storage.DatabaseFilePath};Pooling=False;Foreign Keys=True;Default Timeout=5"));
        services.AddScoped<AdrenalinaDatabaseInitializer>();
        services.AddSingleton<AdrenalinaReportExporter>();
        services.AddScoped<CafeManagementService>();
        services.AddScoped<ICafeManagementService>(provider => provider.GetRequiredService<CafeManagementService>());
        services.AddScoped<IAdminAuthService>(provider => provider.GetRequiredService<CafeManagementService>());
        var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var cafeService = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        await cafeService.EnsureInitializedAsync();
        var initialAccessPath = Path.Combine(root, AdrenalinaDatabaseInitializer.InitialAccessFileName);
        var passwordLine = (await File.ReadAllLinesAsync(initialAccessPath)).Single(line => line.StartsWith("Senha: ", StringComparison.Ordinal));
        var initialAdminPassword = passwordLine["Senha: ".Length..];
        var adminId = (await cafeService.GetUsersAsync()).Single(user => user.Login == "admin").Id;
        var clientResult = await cafeService.UpsertUserAsync(new UserUpsertRequest
        {
            DisplayName = "Cliente Comum",
            Login = "cliente",
            Pin = "2222",
            ProfileType = Adrenalina.Domain.UserProfileType.Common,
            AnnotationLimit = 25m
        }, adminId);
        if (!clientResult.Success)
        {
            throw new InvalidOperationException(clientResult.Message);
        }

        var environment = new TestEnvironment(root, serviceProvider, initialAdminPassword);
        return environment;
    }

    public async Task<T> RunAsync<T>(Func<ICafeManagementService, Task<T>> operation)
    {
        await using var scope = _provider.CreateAsyncScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ICafeManagementService>());
    }

    public async Task RunAsync(Func<ICafeManagementService, Task> operation)
    {
        await using var scope = _provider.CreateAsyncScope();
        await operation(scope.ServiceProvider.GetRequiredService<ICafeManagementService>());
    }

    public async ValueTask DisposeAsync()
    {
        await _provider.DisposeAsync();

        var safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "Adrenalina.Tests"));
        var resolved = Path.GetFullPath(RootPath);
        if (resolved.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
        {
            Directory.Delete(resolved, recursive: true);
        }
    }
}
