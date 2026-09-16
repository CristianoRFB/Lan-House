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
    public const string DefaultAdminPassword = "admin admin";

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
            initialPassword = DefaultAdminPassword;
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

            if (!await ColumnExistsAsync(connection, "Users", "FailedLoginAttempts", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER NOT NULL DEFAULT 0;", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Users", "LockedUntilUtc", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Users ADD COLUMN LockedUntilUtc TEXT NULL;", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Machines", "MachineCredentialHash", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN MachineCredentialHash TEXT NOT NULL DEFAULT '';", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Machines", "MachineCredentialId", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN MachineCredentialId TEXT NOT NULL DEFAULT '';", cancellationToken);
            }

            await ExecuteAsync(connection, "UPDATE Machines SET MachineCredentialId = lower(hex(randomblob(16))) WHERE MachineCredentialId = '';", cancellationToken);

            if (!await ColumnExistsAsync(connection, "Machines", "MachineCredentialVersion", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN MachineCredentialVersion INTEGER NOT NULL DEFAULT 1;", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Machines", "IsRevoked", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN IsRevoked INTEGER NOT NULL DEFAULT 0;", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Machines", "ClientVersion", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN ClientVersion TEXT NOT NULL DEFAULT '';", cancellationToken);
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN AgentVersion TEXT NOT NULL DEFAULT '';", cancellationToken);
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN ProtocolVersion INTEGER NOT NULL DEFAULT 1;", cancellationToken);
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN AgentHealthy INTEGER NOT NULL DEFAULT 0;", cancellationToken);
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN LastAgentSeenUtc TEXT NULL;", cancellationToken);
                await ExecuteAsync(connection, "ALTER TABLE Machines ADD COLUMN PolicyVersion INTEGER NOT NULL DEFAULT 1;", cancellationToken);
            }

            await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS MachinePairingSessions (Id TEXT NOT NULL CONSTRAINT PK_MachinePairingSessions PRIMARY KEY, CreatedAtUtc TEXT NOT NULL, UpdatedAtUtc TEXT NOT NULL, MachineId TEXT NOT NULL, CodeHash TEXT NOT NULL, ExpiresAtUtc TEXT NOT NULL, RequestedAtUtc TEXT NULL, ApprovedAtUtc TEXT NULL, CompletedAtUtc TEXT NULL, ApprovedByUserId TEXT NULL, Hostname TEXT NOT NULL, MachineFingerprint TEXT NOT NULL, Status TEXT NOT NULL, FOREIGN KEY (MachineId) REFERENCES Machines (Id) ON DELETE CASCADE);", cancellationToken);
            await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_MachinePairingSessions_CodeHash_Status ON MachinePairingSessions (CodeHash, Status);", cancellationToken);

            if (!await ColumnExistsAsync(connection, "Backups", "Sha256", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Backups ADD COLUMN Sha256 TEXT NOT NULL DEFAULT '';", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "Backups", "SizeBytes", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE Backups ADD COLUMN SizeBytes INTEGER NOT NULL DEFAULT 0;", cancellationToken);
            }

            if (!await ColumnExistsAsync(connection, "RemoteCommands", "ExpiresAtUtc", cancellationToken))
            {
                await ExecuteAsync(connection, "ALTER TABLE RemoteCommands ADD COLUMN ExpiresAtUtc TEXT NULL;", cancellationToken);
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
            await ExecuteAsync(connection, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Machines_MachineCredentialId ON Machines (MachineCredentialId);", cancellationToken);

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
