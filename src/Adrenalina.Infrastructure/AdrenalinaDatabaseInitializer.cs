using System.Data;
using System.Security.Cryptography;
using Adrenalina.Application;
using Adrenalina.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Infrastructure;

public sealed class AdrenalinaDatabaseInitializer(
    AdrenalinaDbContext db,
    AdrenalinaStoragePaths storagePaths,
    ILogger<AdrenalinaDatabaseInitializer> logger)
{
    public const string InitialAccessFileName = "initial-admin-access.txt";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await UpgradeExistingSchemaAsync(cancellationToken);
        await SeedRequiredDataAsync(cancellationToken);
    }

    private async Task SeedRequiredDataAsync(CancellationToken cancellationToken)
    {
        if (!await db.Settings.AnyAsync(cancellationToken))
        {
            db.Settings.Add(new AdminSettings());
        }

        string? initialPassword = null;
        string? initialPin = null;
        if (!await db.Users.AnyAsync(entry => entry.Login == "admin", cancellationToken))
        {
            initialPassword = GeneratePassword();
            initialPin = RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4");
            db.Users.Add(new UserAccount
            {
                DisplayName = "Administrador",
                Login = "admin",
                PinHash = PasswordHasher.Hash(initialPin),
                PasswordHash = PasswordHasher.Hash(initialPassword),
                ProfileType = UserProfileType.Admin,
                AnnotationLimit = 0m
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        if (initialPassword is not null && initialPin is not null)
        {
            var root = Path.GetDirectoryName(storagePaths.DatabaseFilePath)!;
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, InitialAccessFileName);
            var payload = string.Join(Environment.NewLine,
            [
                "ADRENALINA - ACESSO INICIAL",
                "Login: admin",
                $"Senha: {initialPassword}",
                $"PIN: {initialPin}",
                "",
                "Troque a senha no primeiro acesso. Este arquivo será removido quando a nova senha for salva."
            ]);
            var temporaryPath = path + ".tmp";
            await File.WriteAllTextAsync(temporaryPath, payload, cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(18);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=') + "!A7";
    }

    private async Task UpgradeExistingSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ColumnExistsAsync(connection, "Users", "IsBlocked", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN IsBlocked INTEGER NOT NULL DEFAULT 0;", cancellationToken);
            }

            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
            await ExecuteAsync(connection, "PRAGMA synchronous=NORMAL;", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_Machines_LastSeenUtc ON Machines (LastSeenUtc);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_Sessions_MachineId_Status ON Sessions (MachineId, Status);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_Sessions_UserAccountId ON Sessions (UserAccountId);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_LedgerEntries_UserAccountId_CreatedAtUtc ON LedgerEntries (UserAccountId, CreatedAtUtc);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_RemoteCommands_MachineId_Status ON RemoteCommands (MachineId, Status);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_Notifications_MachineId_IsReadByClient ON Notifications (MachineId, IsReadByClient);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_ClientRequests_MachineId_Status ON ClientRequests (MachineId, Status);", cancellationToken);

            try
            {
                await ExecuteAsync(
                    connection,
                    "CREATE UNIQUE INDEX IF NOT EXISTS IX_Sessions_OneActivePerMachine ON Sessions (MachineId) WHERE Status = 1;",
                    cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Não foi possível garantir uma única sessão ativa por máquina. Verifique registros ativos duplicados.");
                throw new InvalidOperationException("O banco contém sessões ativas duplicadas para a mesma máquina.", exception);
            }

            await ExecuteAsync(connection, "PRAGMA user_version=2;", cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ColumnExistsAsync(
        System.Data.Common.DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info('{table}');";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task ExecuteAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
