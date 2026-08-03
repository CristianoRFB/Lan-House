using Adrenalina.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Adrenalina.Infrastructure;

public sealed class AdrenalinaDbContext(DbContextOptions<AdrenalinaDbContext> options) : DbContext(options)
{
    public DbSet<AdminSettings> Settings => Set<AdminSettings>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<RemoteCommand> RemoteCommands => Set<RemoteCommand>();
    public DbSet<NotificationRecord> Notifications => Set<NotificationRecord>();
    public DbSet<ClientRequestRecord> ClientRequests => Set<ClientRequestRecord>();
    public DbSet<BackupSnapshot> Backups => Set<BackupSnapshot>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var timeOnlyConverter = new ValueConverter<TimeOnly, string>(
            value => value.ToString("HH\\:mm"),
            value => TimeOnly.Parse(value));

        modelBuilder.Entity<AdminSettings>()
            .Property(property => property.BackupCutoffLocalTime)
            .HasConversion(timeOnlyConverter);

        modelBuilder.Entity<UserAccount>()
            .HasIndex(entity => entity.Login)
            .IsUnique();

        modelBuilder.Entity<Machine>()
            .HasIndex(entity => entity.MachineKey)
            .IsUnique();

        modelBuilder.Entity<Machine>()
            .HasIndex(entity => entity.Name)
            .IsUnique();

        modelBuilder.Entity<Machine>()
            .HasIndex(entity => entity.LastSeenUtc);

        modelBuilder.Entity<LedgerEntry>()
            .Property(property => property.Amount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UserAccount>()
            .Property(property => property.Balance)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UserAccount>()
            .Property(property => property.PendingAnnotationAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<UserAccount>()
            .Property(property => property.AnnotationLimit)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AdminSettings>()
            .Property(property => property.DefaultCommonAnnotationLimit)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AdminSettings>()
            .Property(property => property.DefaultPcHourlyRate)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AdminSettings>()
            .Property(property => property.DefaultConsoleHourlyRate)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SessionRecord>()
            .Property(property => property.HourlyRate)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SessionRecord>()
            .Property(property => property.TotalSpent)
            .HasPrecision(18, 2);

        modelBuilder.Entity<SessionRecord>()
            .Property(property => property.PendingAnnotationAmount)
            .HasPrecision(18, 2);

        modelBuilder.Entity<AuditLog>()
            .HasIndex(entity => entity.CreatedAtUtc);

        modelBuilder.Entity<SessionRecord>()
            .HasIndex(entity => entity.Status);

        modelBuilder.Entity<SessionRecord>()
            .HasIndex(entity => new { entity.MachineId, entity.Status });

        modelBuilder.Entity<SessionRecord>()
            .HasIndex(entity => entity.MachineId)
            .HasFilter("Status = 1")
            .IsUnique()
            .HasDatabaseName("IX_Sessions_OneActivePerMachine");

        modelBuilder.Entity<SessionRecord>()
            .HasIndex(entity => entity.UserAccountId);

        modelBuilder.Entity<LedgerEntry>()
            .HasIndex(entity => new { entity.UserAccountId, entity.CreatedAtUtc });

        modelBuilder.Entity<RemoteCommand>()
            .HasIndex(entity => new { entity.MachineId, entity.Status });

        modelBuilder.Entity<NotificationRecord>()
            .HasIndex(entity => new { entity.MachineId, entity.IsReadByClient });

        modelBuilder.Entity<ClientRequestRecord>()
            .HasIndex(entity => entity.Status);

        modelBuilder.Entity<ClientRequestRecord>()
            .HasIndex(entity => new { entity.MachineId, entity.Status });

        modelBuilder.Entity<SessionRecord>()
            .HasOne<Machine>()
            .WithMany()
            .HasForeignKey(entity => entity.MachineId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SessionRecord>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(entity => entity.UserAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<LedgerEntry>()
            .HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(entity => entity.UserAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RemoteCommand>()
            .HasOne<Machine>()
            .WithMany()
            .HasForeignKey(entity => entity.MachineId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<NotificationRecord>()
            .HasOne<Machine>()
            .WithMany()
            .HasForeignKey(entity => entity.MachineId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ClientRequestRecord>()
            .HasOne<Machine>()
            .WithMany()
            .HasForeignKey(entity => entity.MachineId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
