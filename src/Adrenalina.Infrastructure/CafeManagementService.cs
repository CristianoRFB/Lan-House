using System.Globalization;
using System.Text;
using System.Text.Json;
using Adrenalina.Application;
using Adrenalina.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Adrenalina.Infrastructure;

public sealed class CafeManagementService(
    AdrenalinaDbContext db,
    AdrenalinaStoragePaths storagePaths,
    ILogger<CafeManagementService> logger) : ICafeManagementService, IAdminAuthService
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Settings.AnyAsync(cancellationToken))
        {
            db.Settings.Add(new AdminSettings());
        }

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            db.Users.AddRange(
                new UserAccount
                {
                    DisplayName = "Administrador",
                    Login = "admin",
                    PinHash = PasswordHasher.Hash("1234"),
                    PasswordHash = PasswordHasher.Hash("adrenalina123"),
                    ProfileType = UserProfileType.Admin,
                    AnnotationLimit = 0m
                },
                new UserAccount
                {
                    DisplayName = "Ghost Livre",
                    Login = "ghost",
                    PinHash = PasswordHasher.Hash("0000"),
                    PasswordHash = PasswordHasher.Hash("ghost123"),
                    ProfileType = UserProfileType.Ghost,
                    AnnotationLimit = 0m
                },
                new UserAccount
                {
                    DisplayName = "Operador Especial",
                    Login = "especial",
                    PinHash = PasswordHasher.Hash("1111"),
                    PasswordHash = PasswordHasher.Hash("especial123"),
                    ProfileType = UserProfileType.Special,
                    AnnotationLimit = 0m
                },
                new UserAccount
                {
                    DisplayName = "Cliente Comum",
                    Login = "cliente",
                    PinHash = PasswordHasher.Hash("2222"),
                    PasswordHash = PasswordHasher.Hash("cliente123"),
                    ProfileType = UserProfileType.Common,
                    AnnotationLimit = 25m
                });
        }

        if (!await db.Machines.AnyAsync(cancellationToken))
        {
            db.Machines.AddRange(
                new Machine { Name = "PC-01", Hostname = "PC-01", IpAddress = "192.168.0.101", Kind = MachineKind.Pc, Status = MachineStatus.Idle },
                new Machine { Name = "PC-02", Hostname = "PC-02", IpAddress = "192.168.0.102", Kind = MachineKind.Pc, Status = MachineStatus.Idle },
                new Machine { Name = "PS-01", Hostname = "PS-01", IpAddress = "192.168.0.201", Kind = MachineKind.Console, Status = MachineStatus.Idle });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthenticatedAdmin?> ValidateAsync(string login, string password, CancellationToken cancellationToken = default)
    {
        var normalized = login.Trim().ToLowerInvariant();
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(
                account => account.Login.ToLower() == normalized &&
                           (account.ProfileType == UserProfileType.Admin || account.ProfileType == UserProfileType.Special),
                cancellationToken);

        if (user is null || !PasswordHasher.Verify(user.PasswordHash, password))
        {
            return null;
        }

        return new AuthenticatedAdmin(user.Id, user.Login, user.DisplayName, user.ProfileType);
    }

    public async Task<UserDto?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);
        return user is null ? null : MapUser(user);
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsEntityAsync(cancellationToken);
        var machines = await db.Machines.AsNoTracking().OrderBy(entry => entry.Name).ToListAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().OrderBy(entry => entry.DisplayName).ToListAsync(cancellationToken);
        var sessions = await db.Sessions.AsNoTracking().OrderByDescending(entry => entry.StartedAtUtc).Take(20).ToListAsync(cancellationToken);
        var requests = await db.ClientRequests.AsNoTracking()
            .Where(entry => entry.Status == ClientRequestStatus.Pending)
            .OrderByDescending(entry => entry.RequestedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);
        var logs = (await db.AuditLogs.AsNoTracking()
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(12)
                .ToListAsync(cancellationToken))
            .Select(MapAudit)
            .ToList();

        var usageByDay = await db.Sessions.AsNoTracking()
            .Where(entry => entry.StartedAtUtc >= DateTime.UtcNow.AddDays(-7))
            .GroupBy(entry => entry.StartedAtUtc.Date)
            .Select(group => new ChartPointDto(group.Key.ToString("dd/MM"), group.Sum(entry => entry.ConsumedMinutes)))
            .ToListAsync(cancellationToken);

        var usageByMachine = await db.Sessions.AsNoTracking()
            .Where(entry => entry.StartedAtUtc >= DateTime.UtcNow.AddDays(-30))
            .Join(db.Machines.AsNoTracking(), session => session.MachineId, machine => machine.Id, (session, machine) => new { session, machine })
            .GroupBy(entry => entry.machine.Name)
            .Select(group => new ChartPointDto(group.Key, group.Sum(entry => entry.session.ConsumedMinutes)))
            .OrderByDescending(point => point.Value)
            .Take(6)
            .ToListAsync(cancellationToken);

        var machineLookup = machines.ToDictionary(entry => entry.Id);

        return new DashboardDto
        {
            CafeName = settings.CafeName,
            OnlineMachines = machines.Count(entry => IsMachineOnline(entry)),
            ActiveMachines = machines.Count(entry => entry.Status == MachineStatus.InSession),
            ActiveSessions = sessions.Count(entry => entry.Status == SessionStatus.Active),
            PendingRequests = requests.Count,
            PendingAnnotations = users.Sum(entry => entry.PendingAnnotationAmount),
            PromisedPayments = await db.LedgerEntries.AsNoTracking()
                .Where(entry => entry.Type == LedgerEntryType.PaymentPromise)
                .SumAsync(entry => entry.Amount, cancellationToken),
            Machines = await GetMachinesAsync(cancellationToken),
            Users = users.Take(8).Select(MapUser).ToList(),
            Sessions = sessions.Select(entry => MapSession(entry, machineLookup.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida")).ToList(),
            Requests = requests.Select(entry => MapRequest(entry, machineLookup.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida")).ToList(),
            Logs = logs,
            UsageByDay = usageByDay,
            UsageByMachine = usageByMachine
        };
    }

    public async Task<IReadOnlyList<MachineDto>> GetMachinesAsync(CancellationToken cancellationToken = default)
    {
        var machines = await db.Machines.AsNoTracking().OrderBy(entry => entry.Name).ToListAsync(cancellationToken);
        var snapshots = await db.ProcessSnapshots.AsNoTracking()
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        return machines.Select(machine => new MachineDto
        {
            Id = machine.Id,
            MachineKey = machine.MachineKey,
            Name = machine.Name,
            Hostname = machine.Hostname,
            IpAddress = machine.IpAddress,
            Kind = machine.Kind,
            Status = IsMachineOnline(machine) ? machine.Status : MachineStatus.Offline,
            GroupName = machine.GroupName,
            ServiceProtectionEnabled = machine.ServiceProtectionEnabled,
            BandwidthLimitEnabled = machine.BandwidthLimitEnabled,
            BandwidthLimitKbps = machine.BandwidthLimitKbps,
            LastCommandSummary = machine.LastCommandSummary,
            Observations = machine.Observations,
            LastSeenUtc = machine.LastSeenUtc,
            RecentProcesses = snapshots
                .Where(entry => entry.MachineId == machine.Id)
                .Take(6)
                .Select(entry => new ProcessDto
                {
                    ProcessName = entry.ProcessName,
                    WindowTitle = entry.WindowTitle,
                    MemoryMb = entry.MemoryMb
                })
                .ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return (await db.Users.AsNoTracking()
                .OrderBy(entry => entry.DisplayName)
                .ToListAsync(cancellationToken))
            .Select(MapUser)
            .ToList();
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var machines = await db.Machines.AsNoTracking().ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var sessions = await db.Sessions.AsNoTracking()
            .OrderByDescending(entry => entry.StartedAtUtc)
            .Take(150)
            .ToListAsync(cancellationToken);

        return sessions.Select(entry =>
            MapSession(entry, machines.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida")).ToList();
    }

    public async Task<IReadOnlyList<ClientRequestDto>> GetPendingRequestsAsync(CancellationToken cancellationToken = default)
    {
        var machines = await db.Machines.AsNoTracking().ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var requests = await db.ClientRequests.AsNoTracking()
            .Where(entry => entry.Status == ClientRequestStatus.Pending)
            .OrderByDescending(entry => entry.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        return requests.Select(entry =>
            MapRequest(entry, machines.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida")).ToList();
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetRecentLogsAsync(int take, CancellationToken cancellationToken = default)
    {
        return (await db.AuditLogs.AsNoTracking()
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken))
            .Select(MapAudit)
            .ToList();
    }

    public async Task<SettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        return MapSettings(await GetSettingsEntityAsync(cancellationToken));
    }

    public async Task<OperationResult> SaveSettingsAsync(SettingsUpdateRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsEntityAsync(cancellationToken);
        settings.CafeName = TextSanitizer.Normalize(request.CafeName);
        settings.DefaultTheme = request.DefaultTheme;
        settings.UpdateMode = request.UpdateMode;
        settings.BackupCutoffLocalTime = request.BackupCutoffLocalTime;
        settings.BackupRetentionDays = Math.Max(1, request.BackupRetentionDays);
        settings.WelcomeMessage = TextSanitizer.Normalize(request.WelcomeMessage);
        settings.GoodbyeMessage = TextSanitizer.Normalize(request.GoodbyeMessage);
        settings.LockMessage = TextSanitizer.Normalize(request.LockMessage);
        settings.AllowedProgramsCsv = TextSanitizer.Normalize(request.AllowedProgramsCsv);
        settings.BlockedProgramsCsv = TextSanitizer.Normalize(request.BlockedProgramsCsv);
        settings.LimitBandwidthEnabledByDefault = request.LimitBandwidthEnabledByDefault;
        settings.OfflineSyncEnabled = request.OfflineSyncEnabled;
        settings.ShowRemainingTimeByDefault = request.ShowRemainingTimeByDefault;
        settings.DefaultCommonAnnotationLimit = request.DefaultCommonAnnotationLimit;
        settings.DefaultPcHourlyRate = request.DefaultPcHourlyRate;
        settings.DefaultConsoleHourlyRate = request.DefaultConsoleHourlyRate;
        settings.DemoModeEnabled = request.DemoModeEnabled;
        settings.BrandLogoPath = TextSanitizer.Normalize(request.BrandLogoPath);
        settings.AlertSoundPath = TextSanitizer.Normalize(request.AlertSoundPath);
        settings.Touch();

        await LogAsync("Configuracao", "Atualizacao", actorUserId, null, null, "Configurações avançadas atualizadas.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Configurações salvas.");
    }

    public async Task<OperationResult> UpsertUserAsync(UserUpsertRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var login = TextSanitizer.Normalize(request.Login).ToLowerInvariant();
        if (!LoginRules.LooksLikeLetterLogin(login))
        {
            return new OperationResult(false, "O login precisa conter apenas letras e separadores simples.");
        }

        UserAccount user;
        var isNew = !request.Id.HasValue || request.Id == Guid.Empty;
        if (isNew)
        {
            if (await db.Users.AnyAsync(entry => entry.Login == login, cancellationToken))
            {
                return new OperationResult(false, "Já existe um usuário com esse login.");
            }

            user = new UserAccount();
            db.Users.Add(user);
        }
        else
        {
            var requestId = request.Id.GetValueOrDefault();
            user = await db.Users.FirstOrDefaultAsync(entry => entry.Id == requestId, cancellationToken)
                ?? throw new InvalidOperationException("Usuário não encontrado.");
        }

        user.DisplayName = TextSanitizer.Normalize(request.DisplayName);
        user.Login = login;
        user.ProfileType = request.ProfileType;
        user.Balance = request.Balance;
        user.AnnotationLimit = request.ProfileType == UserProfileType.Common ? request.AnnotationLimit : 0m;
        user.IsTemporary = request.IsTemporary;
        user.TemporaryUntilUtc = request.TemporaryUntilUtc;
        user.Notes = TextSanitizer.Normalize(request.Notes);
        user.CanSeeOwnBalance = true;
        user.CanSeeOwnAnnotations = true;
        user.Touch();

        if (!string.IsNullOrWhiteSpace(request.Pin))
        {
            if (!LoginRules.LooksLikeFourDigitPin(request.Pin))
            {
                return new OperationResult(false, "O PIN precisa ter 4 dígitos.");
            }

            user.PinHash = PasswordHasher.Hash(request.Pin);
        }
        else if (isNew)
        {
            user.PinHash = PasswordHasher.Hash("1234");
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(request.Password);
        }
        else if (isNew)
        {
            user.PasswordHash = PasswordHasher.Hash("adrenalina123");
        }

        await LogAsync("Usuario", isNew ? "Criacao" : "Atualizacao", actorUserId, null, user.Id, $"Conta {user.DisplayName} salva com perfil {user.ProfileType}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, isNew ? "Usuário criado." : "Usuário atualizado.");
    }

    public async Task<OperationResult> AddLedgerEntryAsync(LedgerEntryRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(entry => entry.Id == request.UserAccountId, cancellationToken);
        if (user is null)
        {
            return new OperationResult(false, "Usuário não encontrado.");
        }

        var amount = Math.Round(request.Amount, 2);
        if (amount == 0m)
        {
            return new OperationResult(false, "Informe um valor diferente de zero.");
        }

        if (request.Type == LedgerEntryType.Annotation && !user.HasUnlimitedAnnotations)
        {
            var settings = await GetSettingsEntityAsync(cancellationToken);
            var limit = user.AnnotationLimit > 0m ? user.AnnotationLimit : settings.DefaultCommonAnnotationLimit;
            if (user.PendingAnnotationAmount + amount > limit)
            {
                return new OperationResult(false, $"O limite de anotação para esse usuário é de R$ {limit:N2}.");
            }
        }

        switch (request.Type)
        {
            case LedgerEntryType.Credit:
                user.Balance += amount;
                break;
            case LedgerEntryType.Annotation:
                user.Balance -= amount;
                user.PendingAnnotationAmount += amount;
                break;
            case LedgerEntryType.PaymentPromise:
                break;
            default:
                user.Balance += amount;
                break;
        }

        db.LedgerEntries.Add(new LedgerEntry
        {
            UserAccountId = user.Id,
            Type = request.Type,
            Amount = amount,
            Description = TextSanitizer.Normalize(request.Description),
            PromisedPaymentDateUtc = request.PromisedPaymentDateUtc,
            CreatedByUserId = actorUserId
        });

        await LogAsync("Financeiro", request.Type.ToString(), actorUserId, null, user.Id, $"Lançamento de R$ {amount:N2} para {user.DisplayName}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Lançamento registrado.");
    }

    public async Task<OperationResult> StartSessionAsync(SessionStartRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.Id == request.MachineId, cancellationToken);
        if (machine is null)
        {
            return new OperationResult(false, "Máquina não encontrada.");
        }

        if (machine.CurrentSessionId.HasValue)
        {
            return new OperationResult(false, "Essa máquina já possui uma sessão ativa.");
        }

        UserAccount? user = null;
        if (request.UserAccountId.HasValue)
        {
            user = await db.Users.FirstOrDefaultAsync(entry => entry.Id == request.UserAccountId.Value, cancellationToken);
        }

        var profile = user?.ProfileType ?? UserProfileType.Ghost;
        var displayName = user?.DisplayName ?? TextSanitizer.Normalize(request.UserDisplayName);
        if (profile == UserProfileType.Common && !request.IsDemoMode && request.GrantedMinutes <= 0)
        {
            return new OperationResult(false, "Usuários comuns precisam iniciar com tempo maior que zero.");
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var hourlyRate = request.HourlyRate > 0m
            ? request.HourlyRate
            : ResolveHourlyRate(settings, machine.Kind);

        var session = new SessionRecord
        {
            MachineId = machine.Id,
            UserAccountId = user?.Id,
            UserDisplayName = string.IsNullOrWhiteSpace(displayName) ? "Uso livre" : displayName,
            UserProfileType = profile,
            MachineKind = machine.Kind,
            Status = SessionStatus.Active,
            StartedAtUtc = DateTime.UtcNow,
            LastTickedAtUtc = DateTime.UtcNow,
            GrantedMinutes = request.GrantedMinutes,
            RemainingMinutes = request.GrantedMinutes,
            HourlyRate = hourlyRate,
            IsDemoMode = request.IsDemoMode,
            HideTimerOnClient = request.HideTimerOnClient,
            LockMessage = settings.LockMessage
        };

        db.Sessions.Add(session);
        machine.CurrentSessionId = session.Id;
        machine.Status = MachineStatus.InSession;
        machine.Touch();

        db.Notifications.Add(new NotificationRecord
        {
            MachineId = machine.Id,
            UserAccountId = user?.Id,
            Severity = NotificationSeverity.Success,
            Title = "Sessão iniciada",
            Message = settings.WelcomeMessage.Replace("{usuario}", session.UserDisplayName, StringComparison.OrdinalIgnoreCase),
            PlaySound = true
        });

        await LogAsync("Sessao", "Inicio", actorUserId, machine.Id, user?.Id, $"Sessão iniciada em {machine.Name} para {session.UserDisplayName}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Sessão iniciada.");
    }

    public async Task<OperationResult> AdjustSessionAsync(SessionAdjustRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(entry => entry.Id == request.SessionId, cancellationToken);
        if (session is null)
        {
            return new OperationResult(false, "Sessão não encontrada.");
        }

        session.GrantedMinutes += request.AdditionalMinutes;
        session.RemainingMinutes += request.AdditionalMinutes;
        session.PendingAnnotationAmount += request.AdditionalAnnotationAmount;
        session.Touch();

        if (request.AdditionalAnnotationAmount != 0m && session.UserAccountId.HasValue)
        {
            var ledgerResult = await AddLedgerEntryAsync(new LedgerEntryRequest
            {
                UserAccountId = session.UserAccountId.Value,
                Type = LedgerEntryType.Annotation,
                Amount = request.AdditionalAnnotationAmount,
                Description = string.IsNullOrWhiteSpace(request.Reason) ? "Ajuste de sessão" : request.Reason
            }, actorUserId, cancellationToken);

            if (!ledgerResult.Success)
            {
                return ledgerResult;
            }
        }

        await LogAsync("Sessao", "Ajuste", actorUserId, session.MachineId, session.UserAccountId, $"Sessão ajustada com {request.AdditionalMinutes} minutos extras.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Sessão ajustada.");
    }

    public async Task<OperationResult> EndSessionAsync(Guid sessionId, string reason, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(entry => entry.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return new OperationResult(false, "Sessão não encontrada.");
        }

        if (session.Status is SessionStatus.Finished or SessionStatus.Expired)
        {
            return new OperationResult(false, "A sessão já foi encerrada.");
        }

        var machine = await db.Machines.FirstAsync(entry => entry.Id == session.MachineId, cancellationToken);
        var settings = await GetSettingsEntityAsync(cancellationToken);

        session.Status = SessionStatus.Finished;
        session.EndedAtUtc = DateTime.UtcNow;
        session.ClosureReason = TextSanitizer.Normalize(reason);
        await SettleSessionBillingAsync(session, actorUserId, cancellationToken);
        session.Touch();

        machine.CurrentSessionId = null;
        machine.Status = MachineStatus.Idle;
        machine.Touch();

        db.Notifications.Add(new NotificationRecord
        {
            MachineId = machine.Id,
            UserAccountId = session.UserAccountId,
            Severity = NotificationSeverity.Info,
            Title = "Sessão encerrada",
            Message = settings.GoodbyeMessage,
            PlaySound = true
        });

        await LogAsync("Sessao", "Fim", actorUserId, machine.Id, session.UserAccountId, $"Sessão encerrada em {machine.Name}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Sessão encerrada.");
    }

    public async Task<OperationResult> QueueMachineCommandAsync(MachineCommandRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.Id == request.MachineId, cancellationToken);
        if (machine is null)
        {
            return new OperationResult(false, "Máquina não encontrada.");
        }

        db.RemoteCommands.Add(new RemoteCommand
        {
            MachineId = machine.Id,
            RequestedByUserId = actorUserId,
            Type = request.Type,
            Title = TextSanitizer.Normalize(request.Title),
            Message = TextSanitizer.Normalize(request.Message),
            PayloadJson = request.PayloadJson ?? string.Empty,
            RequestedAtUtc = DateTime.UtcNow
        });

        machine.LastCommandSummary = $"{request.Type} às {DateTime.Now:HH:mm}";
        if (request.Type == RemoteCommandType.LockScreen)
        {
            machine.Status = MachineStatus.Locked;
        }

        await LogAsync("Maquina", request.Type.ToString(), actorUserId, machine.Id, null, $"Comando {request.Type} enviado para {machine.Name}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Comando enfileirado.");
    }

    public async Task<OperationResult> ResolveClientRequestAsync(ClientRequestResolution request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var entry = await db.ClientRequests.FirstOrDefaultAsync(item => item.Id == request.RequestId, cancellationToken);
        if (entry is null)
        {
            return new OperationResult(false, "Solicitação não encontrada.");
        }

        entry.Status = request.Approve ? ClientRequestStatus.Approved : ClientRequestStatus.Rejected;
        entry.ResolvedAtUtc = DateTime.UtcNow;
        entry.ResolvedByUserId = actorUserId;
        entry.AdminResponse = TextSanitizer.Normalize(request.ResponseMessage);
        entry.Touch();

        db.Notifications.Add(new NotificationRecord
        {
            MachineId = entry.MachineId,
            UserAccountId = entry.UserAccountId,
            Severity = request.Approve ? NotificationSeverity.Success : NotificationSeverity.Warning,
            Title = request.Approve ? "Solicitação aprovada" : "Solicitação rejeitada",
            Message = string.IsNullOrWhiteSpace(entry.AdminResponse)
                ? (request.Approve ? "Sua solicitação foi aprovada." : "Sua solicitação foi rejeitada.")
                : entry.AdminResponse,
            PlaySound = true
        });

        await LogAsync("Solicitacao", entry.Type.ToString(), actorUserId, entry.MachineId, entry.UserAccountId, $"Solicitação {entry.Type} {(request.Approve ? "aprovada" : "rejeitada")}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, "Solicitação processada.");
    }

    public async Task<OperationResult> CreateManualBackupAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var snapshot = await CreateBackupSnapshotAsync(settings, true, actorUserId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return new OperationResult(
            snapshot.Succeeded,
            snapshot.Succeeded
                ? $"Backup manual concluído em {snapshot.FolderPath}."
                : $"Falha ao gerar backup manual: {snapshot.Summary}");
    }

    public async Task<FileExportResult?> ExportReportAsync(ReportFilterRequest request, CancellationToken cancellationToken = default)
    {
        var start = request.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
        var end = request.EndDate.ToDateTime(new TimeOnly(23, 59), DateTimeKind.Local).ToUniversalTime();

        var sessions = await db.Sessions.AsNoTracking()
            .Where(entry => entry.StartedAtUtc >= start && entry.StartedAtUtc <= end)
            .ToListAsync(cancellationToken);
        var ledger = await db.LedgerEntries.AsNoTracking()
            .Where(entry => entry.CreatedAtUtc >= start && entry.CreatedAtUtc <= end)
            .ToListAsync(cancellationToken);
        var machines = await db.Machines.AsNoTracking().ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(entry => entry.Id, cancellationToken);
        var settings = await GetSettingsEntityAsync(cancellationToken);

        var summaryLines = BuildSummaryLines(settings.CafeName, request, sessions, ledger, machines, users);

        return request.Format switch
        {
            ReportExportFormat.Txt => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.txt",
                "text/plain",
                Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, summaryLines))),
            ReportExportFormat.Excel => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.xlsx",
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                BuildExcel(summaryLines, sessions, ledger, machines, users)),
            ReportExportFormat.Pdf => new FileExportResult(
                $"relatorio-{request.StartDate:yyyyMMdd}-{request.EndDate:yyyyMMdd}.pdf",
                "application/pdf",
                BuildPdf(summaryLines, sessions, ledger, machines, users)),
            _ => null
        };
    }

    public async Task<ClientLoginResponse> LoginClientAsync(ClientLoginRequest request, CancellationToken cancellationToken = default)
    {
        var machineKey = TextSanitizer.Normalize(request.MachineKey);
        var login = TextSanitizer.Normalize(request.Login).ToLowerInvariant();
        var pin = TextSanitizer.Normalize(request.Pin);

        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.MachineKey == machineKey, cancellationToken);
        if (machine is null)
        {
            return new ClientLoginResponse
            {
                Success = false,
                Message = "Máquina não registrada no servidor."
            };
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var existingSession = await db.Sessions
            .AsNoTracking()
            .OrderByDescending(entry => entry.StartedAtUtc)
            .FirstOrDefaultAsync(
                entry => entry.MachineId == machine.Id && entry.Status == SessionStatus.Active,
                cancellationToken);

        if (existingSession is not null || machine.CurrentSessionId.HasValue)
        {
            return new ClientLoginResponse
            {
                Success = false,
                Message = "Essa máquina já possui uma sessão ativa.",
                RuntimeState = BuildRuntimeState(settings, machine, existingSession, null, [])
            };
        }

        if (!LoginRules.LooksLikeLetterLogin(login) || !LoginRules.LooksLikeFourDigitPin(pin))
        {
            return new ClientLoginResponse
            {
                Success = false,
                Message = "Informe um login válido e PIN de 4 dígitos.",
                RuntimeState = BuildRuntimeState(settings, machine, null, null, [])
            };
        }

        var user = await db.Users.FirstOrDefaultAsync(entry => entry.Login == login, cancellationToken);
        if (user is null || !PasswordHasher.Verify(user.PinHash, pin))
        {
            await LogAsync("Cliente", "LoginNegado", null, machine.Id, user?.Id, $"Falha de login no cliente para {login}.", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            return new ClientLoginResponse
            {
                Success = false,
                Message = "Login ou PIN inválido.",
                RuntimeState = BuildRuntimeState(settings, machine, null, null, [])
            };
        }

        if (user.IsTemporary && user.TemporaryUntilUtc.HasValue && user.TemporaryUntilUtc.Value <= DateTime.UtcNow)
        {
            return new ClientLoginResponse
            {
                Success = false,
                Message = "Essa conta temporária expirou.",
                RuntimeState = BuildRuntimeState(settings, machine, null, user, [])
            };
        }

        var hourlyRate = ResolveHourlyRate(settings, machine.Kind);
        var grantedMinutes = 0;
        if (user.ProfileType == UserProfileType.Common)
        {
            grantedMinutes = CalculateGrantedMinutes(user.Balance, hourlyRate);
            if (grantedMinutes <= 0)
            {
                return new ClientLoginResponse
                {
                    Success = false,
                    Message = "Saldo insuficiente para iniciar a sessão.",
                    RuntimeState = BuildRuntimeState(settings, machine, null, user, [])
                };
            }
        }

        var startResult = await StartSessionAsync(
            new SessionStartRequest
            {
                MachineId = machine.Id,
                UserAccountId = user.Id,
                UserDisplayName = user.DisplayName,
                GrantedMinutes = user.HasUnlimitedTime ? 0 : grantedMinutes,
                HourlyRate = hourlyRate,
                IsDemoMode = false,
                HideTimerOnClient = false
            },
            user.Id,
            cancellationToken);

        if (!startResult.Success)
        {
            return new ClientLoginResponse
            {
                Success = false,
                Message = startResult.Message,
                RuntimeState = BuildRuntimeState(settings, machine, null, user, [])
            };
        }

        machine = await db.Machines.AsNoTracking().FirstAsync(entry => entry.Id == machine.Id, cancellationToken);
        var session = await db.Sessions.AsNoTracking()
            .OrderByDescending(entry => entry.StartedAtUtc)
            .FirstOrDefaultAsync(
                entry => entry.MachineId == machine.Id && entry.Status == SessionStatus.Active,
                cancellationToken);

        return new ClientLoginResponse
        {
            Success = true,
            Message = "Sessão iniciada com sucesso.",
            RuntimeState = BuildRuntimeState(settings, machine, session, user, [])
        };
    }

    public async Task<ClientHeartbeatResponse> SyncClientHeartbeatAsync(ClientHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.MachineKey == request.MachineKey, cancellationToken);
        if (machine is null)
        {
            machine = new Machine
            {
                MachineKey = request.MachineKey,
                Name = request.MachineName,
                Hostname = request.Hostname,
                IpAddress = request.IpAddress,
                Kind = request.Kind
            };

            db.Machines.Add(machine);
        }

        machine.Name = string.IsNullOrWhiteSpace(request.MachineName) ? machine.Name : request.MachineName;
        machine.Hostname = request.Hostname;
        machine.IpAddress = request.IpAddress;
        machine.Kind = request.Kind;
        machine.Status = request.Status;
        machine.LastSeenUtc = DateTime.UtcNow;
        machine.Touch();

        var oldSnapshots = db.ProcessSnapshots.Where(entry => entry.MachineId == machine.Id);
        db.ProcessSnapshots.RemoveRange(oldSnapshots);
        foreach (var process in request.Processes.Take(20))
        {
            db.ProcessSnapshots.Add(new MachineProcessSnapshot
            {
                MachineId = machine.Id,
                ProcessName = process.ProcessName,
                WindowTitle = process.WindowTitle,
                MemoryMb = process.MemoryMb
            });
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        var session = await db.Sessions.AsNoTracking()
            .OrderByDescending(entry => entry.StartedAtUtc)
            .FirstOrDefaultAsync(entry => entry.MachineId == machine.Id && entry.Status == SessionStatus.Active, cancellationToken);
        UserAccount? user = null;
        if (session?.UserAccountId is Guid userId)
        {
            user = await db.Users.AsNoTracking().FirstOrDefaultAsync(entry => entry.Id == userId, cancellationToken);
        }

        machine.Status = session is not null
            ? MachineStatus.InSession
            : machine.Status == MachineStatus.Locked
                ? MachineStatus.Locked
                : request.Status;

        var commands = await db.RemoteCommands
            .Where(entry => entry.MachineId == machine.Id && entry.Status == RemoteCommandStatus.Pending)
            .OrderBy(entry => entry.RequestedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var command in commands)
        {
            command.Status = RemoteCommandStatus.Delivered;
            command.Touch();
        }

        var notifications = await db.Notifications
            .Where(entry => entry.MachineId == machine.Id && !entry.IsReadByClient)
            .OrderBy(entry => entry.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsReadByClient = true;
            notification.Touch();
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ClientHeartbeatResponse
        {
            MachineId = machine.Id,
            Settings = MapSettings(settings),
            RuntimeState = BuildRuntimeState(settings, machine, session, user, notifications),
            Commands = commands.Select(entry => new RemoteCommandEnvelope(entry.Id, entry.Type, entry.Title, entry.Message, entry.PayloadJson)).ToList(),
            Notifications = notifications.Select(entry => new NotificationEnvelope(entry.Id, entry.Title, entry.Message, entry.Severity, entry.PlaySound)).ToList()
        };
    }

    public async Task<OperationResult> SubmitClientRequestsAsync(ClientRequestBatchRequest request, CancellationToken cancellationToken = default)
    {
        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.MachineKey == request.MachineKey, cancellationToken);
        if (machine is null)
        {
            return new OperationResult(false, "Máquina não registrada.");
        }

        foreach (var item in request.Requests)
        {
            db.ClientRequests.Add(new ClientRequestRecord
            {
                MachineId = machine.Id,
                Type = item.Type,
                RequestedLogin = TextSanitizer.Normalize(item.Login),
                RequestedDisplayName = TextSanitizer.Normalize(item.DisplayName),
                PayloadJson = JsonSerializer.Serialize(item, JsonDefaults.Options),
                RequestedAtUtc = item.OccurredAtUtc
            });
        }

        if (request.Requests.Count > 0)
        {
            await LogAsync("Cliente", "Solicitacao", null, machine.Id, null, $"{request.Requests.Count} solicitações recebidas de {machine.Name}.", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new OperationResult(true, "Solicitações sincronizadas.");
    }

    public async Task RunMaintenanceTickAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        await UpdateSessionsAsync(cancellationToken);
        await RunBackupIfNecessaryAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateSessionsAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsEntityAsync(cancellationToken);
        var sessions = await db.Sessions
            .Where(entry => entry.Status == SessionStatus.Active)
            .ToListAsync(cancellationToken);
        if (sessions.Count == 0)
        {
            return;
        }

        var machines = await db.Machines.ToDictionaryAsync(entry => entry.Id, cancellationToken);
        foreach (var session in sessions)
        {
            var elapsedMinutes = (int)(DateTime.UtcNow - session.LastTickedAtUtc).TotalMinutes;
            if (elapsedMinutes <= 0)
            {
                continue;
            }

            session.LastTickedAtUtc = DateTime.UtcNow;
            if (!session.IsDemoMode)
            {
                session.ConsumedMinutes += elapsedMinutes;
                session.TotalSpent = Math.Round(session.ConsumedMinutes / 60m * session.HourlyRate, 2);
            }

            if (session.CountsDownTime)
            {
                session.RemainingMinutes = Math.Max(0, session.RemainingMinutes - elapsedMinutes);
                foreach (var threshold in new[] { 10, 5, 1 })
                {
                    if (session.RemainingMinutes <= threshold && !ContainsAlert(session.TriggeredAlertsCsv, threshold))
                    {
                        session.TriggeredAlertsCsv = AppendAlert(session.TriggeredAlertsCsv, threshold);
                        db.Notifications.Add(new NotificationRecord
                        {
                            MachineId = session.MachineId,
                            UserAccountId = session.UserAccountId,
                            Severity = NotificationSeverity.Warning,
                            Title = "Tempo restante",
                            Message = $"Faltam {threshold} minuto(s) para o fim da sessão.",
                            PlaySound = true
                        });
                    }
                }

                if (session.RemainingMinutes <= 0)
                {
                    session.Status = SessionStatus.Expired;
                    session.EndedAtUtc = DateTime.UtcNow;
                    session.ClosureReason = "Tempo esgotado";
                    await SettleSessionBillingAsync(session, null, cancellationToken);
                    if (machines.TryGetValue(session.MachineId, out var machine))
                    {
                        machine.Status = MachineStatus.Locked;
                        machine.CurrentSessionId = null;
                        machine.Touch();
                        db.RemoteCommands.Add(new RemoteCommand
                        {
                            MachineId = machine.Id,
                            Type = RemoteCommandType.LockScreen,
                            Status = RemoteCommandStatus.Pending,
                            Title = "Tempo encerrado",
                            Message = settings.LockMessage,
                            RequestedAtUtc = DateTime.UtcNow
                        });
                    }

                    db.Notifications.Add(new NotificationRecord
                    {
                        MachineId = session.MachineId,
                        UserAccountId = session.UserAccountId,
                        Severity = NotificationSeverity.Critical,
                        Title = "Tempo encerrado",
                        Message = settings.LockMessage,
                        PlaySound = true
                    });

                    await LogAsync("Sessao", "Expirada", null, session.MachineId, session.UserAccountId, $"Sessão {session.Id} expirou automaticamente.", cancellationToken);
                }
            }
            else
            {
                session.RemainingMinutes = Math.Max(session.RemainingMinutes, 0);
            }

            session.Touch();
        }
    }

    private async Task RunBackupIfNecessaryAsync(CancellationToken cancellationToken)
    {
        var settings = await GetSettingsEntityAsync(cancellationToken);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        if (nowLocal.TimeOfDay < settings.BackupCutoffLocalTime.ToTimeSpan())
        {
            return;
        }

        var localStart = nowLocal.Date;
        var localEnd = localStart.AddDays(1);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localStart, TimeZoneInfo.Local);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localEnd, TimeZoneInfo.Local);
        var alreadyDone = await db.Backups.AsNoTracking()
            .AnyAsync(entry => entry.Succeeded && entry.ExecutedAtUtc >= utcStart && entry.ExecutedAtUtc < utcEnd, cancellationToken);
        if (alreadyDone)
        {
            return;
        }

        await CreateBackupSnapshotAsync(settings, false, null, cancellationToken);
    }

    private async Task<BackupSnapshot> CreateBackupSnapshotAsync(
        AdminSettings settings,
        bool isManual,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(storagePaths.BackupDirectory);
        Directory.CreateDirectory(storagePaths.LogDirectory);

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
        var fileName = $"adrenalina-{nowLocal:yyyyMMdd-HHmmss}.db";
        var destination = Path.Combine(storagePaths.BackupDirectory, fileName);

        try
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            await db.Database.ExecuteSqlInterpolatedAsync($"VACUUM INTO {destination}", cancellationToken);
            CleanupOldBackups(settings.BackupRetentionDays);

            var snapshot = new BackupSnapshot
            {
                FolderPath = destination,
                Succeeded = true,
                Summary = isManual ? "Backup manual concluído." : "Backup automático diário concluído.",
                ExecutedAtUtc = DateTime.UtcNow
            };

            db.Backups.Add(snapshot);
            await LogAsync(
                "Backup",
                isManual ? "Manual" : "Automatico",
                actorUserId,
                null,
                null,
                $"{(isManual ? "Backup manual" : "Backup automático")} gerado em {destination}.",
                cancellationToken);

            return snapshot;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao gerar backup {BackupMode}.", isManual ? "manual" : "automatico");

            var snapshot = new BackupSnapshot
            {
                FolderPath = destination,
                Succeeded = false,
                Summary = exception.Message,
                ExecutedAtUtc = DateTime.UtcNow
            };

            db.Backups.Add(snapshot);
            return snapshot;
        }
    }

    private void CleanupOldBackups(int retentionDays)
    {
        var threshold = DateTime.UtcNow.AddDays(-Math.Max(1, retentionDays));
        foreach (var file in Directory.GetFiles(storagePaths.BackupDirectory, "*.db"))
        {
            var created = File.GetCreationTimeUtc(file);
            if (created < threshold)
            {
                File.Delete(file);
            }
        }
    }

    private async Task SettleSessionBillingAsync(SessionRecord session, Guid? actorUserId, CancellationToken cancellationToken)
    {
        if (session.IsBillingSettled || session.IsDemoMode || !session.UserAccountId.HasValue)
        {
            session.IsBillingSettled = true;
            return;
        }

        if (session.UserProfileType is UserProfileType.Admin or UserProfileType.Ghost)
        {
            session.IsBillingSettled = true;
            return;
        }

        var user = await db.Users.FirstOrDefaultAsync(entry => entry.Id == session.UserAccountId.Value, cancellationToken);
        if (user is null)
        {
            session.IsBillingSettled = true;
            return;
        }

        var amount = Math.Round(session.TotalSpent, 2);
        if (amount != 0m)
        {
            user.Balance -= amount;
            user.Touch();

            db.LedgerEntries.Add(new LedgerEntry
            {
                UserAccountId = user.Id,
                Type = LedgerEntryType.Adjustment,
                Amount = -amount,
                Description = $"Consumo de sessão em {session.StartedAtUtc:dd/MM/yyyy HH:mm}",
                CreatedByUserId = actorUserId
            });
        }

        session.IsBillingSettled = true;
    }

    private static decimal ResolveHourlyRate(AdminSettings settings, MachineKind kind) =>
        kind == MachineKind.Console
            ? settings.DefaultConsoleHourlyRate
            : settings.DefaultPcHourlyRate;

    private static int CalculateGrantedMinutes(decimal balance, decimal hourlyRate)
    {
        if (balance <= 0m || hourlyRate <= 0m)
        {
            return 0;
        }

        return (int)Math.Floor(balance / hourlyRate * 60m);
    }

    private static bool ContainsAlert(string csv, int threshold) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => value == threshold.ToString(CultureInfo.InvariantCulture));

    private static string AppendAlert(string csv, int threshold)
    {
        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var token = threshold.ToString(CultureInfo.InvariantCulture);
        if (!parts.Contains(token))
        {
            parts.Add(token);
        }

        return string.Join(',', parts);
    }

    private async Task<AdminSettings> GetSettingsEntityAsync(CancellationToken cancellationToken)
    {
        return await db.Settings.FirstAsync(cancellationToken);
    }

    private async Task LogAsync(
        string category,
        string eventType,
        Guid? actorUserId,
        Guid? machineId,
        Guid? targetUserId,
        string description,
        CancellationToken cancellationToken)
    {
        var machineIp = string.Empty;
        if (machineId.HasValue)
        {
            machineIp = await db.Machines
                .Where(entry => entry.Id == machineId.Value)
                .Select(entry => entry.IpAddress)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        db.AuditLogs.Add(new AuditLog
        {
            Category = category,
            EventType = eventType,
            ActorUserId = actorUserId,
            MachineId = machineId,
            TargetUserId = targetUserId,
            Description = description,
            IpAddress = machineIp
        });
    }

    private static bool IsMachineOnline(Machine machine) =>
        machine.LastSeenUtc.HasValue && machine.LastSeenUtc.Value >= DateTime.UtcNow.AddMinutes(-2);

    private static UserDto MapUser(UserAccount entry) => new()
    {
        Id = entry.Id,
        DisplayName = entry.DisplayName,
        Login = entry.Login,
        ProfileType = entry.ProfileType,
        Balance = entry.Balance,
        PendingAnnotationAmount = entry.PendingAnnotationAmount,
        AnnotationLimit = entry.AnnotationLimit,
        IsTemporary = entry.IsTemporary,
        TemporaryUntilUtc = entry.TemporaryUntilUtc,
        Notes = entry.Notes
    };

    private static SessionDto MapSession(SessionRecord entry, string machineName) => new()
    {
        Id = entry.Id,
        MachineId = entry.MachineId,
        UserAccountId = entry.UserAccountId,
        MachineName = machineName,
        UserDisplayName = entry.UserDisplayName,
        UserProfileType = entry.UserProfileType,
        MachineKind = entry.MachineKind,
        Status = entry.Status,
        StartedAtUtc = entry.StartedAtUtc,
        EndedAtUtc = entry.EndedAtUtc,
        GrantedMinutes = entry.GrantedMinutes,
        RemainingMinutes = entry.RemainingMinutes,
        ConsumedMinutes = entry.ConsumedMinutes,
        IdleMinutes = entry.IdleMinutes,
        HourlyRate = entry.HourlyRate,
        TotalSpent = entry.TotalSpent,
        PendingAnnotationAmount = entry.PendingAnnotationAmount,
        IsDemoMode = entry.IsDemoMode,
        HideTimerOnClient = entry.HideTimerOnClient
    };

    private static ClientRequestDto MapRequest(ClientRequestRecord entry, string machineName) => new()
    {
        Id = entry.Id,
        MachineId = entry.MachineId,
        UserAccountId = entry.UserAccountId,
        MachineName = machineName,
        Type = entry.Type,
        Status = entry.Status,
        RequestedLogin = entry.RequestedLogin,
        RequestedDisplayName = entry.RequestedDisplayName,
        PayloadJson = entry.PayloadJson,
        RequestedAtUtc = entry.RequestedAtUtc,
        AdminResponse = entry.AdminResponse
    };

    private static AuditLogDto MapAudit(AuditLog entry) => new()
    {
        CreatedAtUtc = entry.CreatedAtUtc,
        Category = entry.Category,
        EventType = entry.EventType,
        Description = entry.Description,
        IpAddress = entry.IpAddress
    };

    private static SettingsDto MapSettings(AdminSettings entry) => new()
    {
        Id = entry.Id,
        CafeName = entry.CafeName,
        DefaultTheme = entry.DefaultTheme,
        UpdateMode = entry.UpdateMode,
        BackupCutoffLocalTime = entry.BackupCutoffLocalTime,
        BackupRetentionDays = entry.BackupRetentionDays,
        WelcomeMessage = entry.WelcomeMessage,
        GoodbyeMessage = entry.GoodbyeMessage,
        LockMessage = entry.LockMessage,
        AllowedProgramsCsv = entry.AllowedProgramsCsv,
        BlockedProgramsCsv = entry.BlockedProgramsCsv,
        LimitBandwidthEnabledByDefault = entry.LimitBandwidthEnabledByDefault,
        OfflineSyncEnabled = entry.OfflineSyncEnabled,
        ShowRemainingTimeByDefault = entry.ShowRemainingTimeByDefault,
        DefaultCommonAnnotationLimit = entry.DefaultCommonAnnotationLimit,
        DefaultPcHourlyRate = entry.DefaultPcHourlyRate,
        DefaultConsoleHourlyRate = entry.DefaultConsoleHourlyRate,
        DemoModeEnabled = entry.DemoModeEnabled,
        BrandLogoPath = entry.BrandLogoPath,
        AlertSoundPath = entry.AlertSoundPath
    };

    private static ClientRuntimeState BuildRuntimeState(
        AdminSettings settings,
        Machine machine,
        SessionRecord? session,
        UserAccount? user,
        IReadOnlyList<NotificationRecord> notifications)
    {
        return new ClientRuntimeState
        {
            MachineName = machine.Name,
            CurrentSessionId = session?.Id,
            Theme = settings.DefaultTheme,
            IsLocked = machine.Status == MachineStatus.Locked,
            IsDemoMode = session?.IsDemoMode ?? false,
            ShowRemainingTime = !(session?.HideTimerOnClient ?? false) && settings.ShowRemainingTimeByDefault,
            LockMessage = string.IsNullOrWhiteSpace(session?.LockMessage) ? settings.LockMessage : session.LockMessage,
            WelcomeMessage = settings.WelcomeMessage,
            GoodbyeMessage = settings.GoodbyeMessage,
            CurrentUserName = session?.UserDisplayName ?? "Aguardando login",
            CurrentUserLogin = user?.Login ?? string.Empty,
            CurrentUserNotes = user?.Notes ?? string.Empty,
            CurrentUserProfile = session?.UserProfileType ?? UserProfileType.Ghost,
            CurrentBalance = user?.Balance ?? 0m,
            PendingAnnotations = user?.PendingAnnotationAmount ?? 0m,
            RemainingMinutes = session?.RemainingMinutes ?? 0,
            SessionMessage = session is null ? "Máquina aguardando sessão." : "Sessão sincronizada com o servidor.",
            LastUpdatedAtUtc = DateTime.UtcNow,
            Notifications = notifications.Select(entry => new NotificationEnvelope(entry.Id, entry.Title, entry.Message, entry.Severity, entry.PlaySound)).ToList()
        };
    }

    private static IReadOnlyList<string> BuildSummaryLines(
        string cafeName,
        ReportFilterRequest request,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        var pcSessions = sessions.Where(entry => entry.MachineKind == MachineKind.Pc).ToList();
        var consoleSessions = sessions.Where(entry => entry.MachineKind == MachineKind.Console).ToList();

        return
        [
            cafeName,
            $"Período: {request.StartDate:dd/MM/yyyy} a {request.EndDate:dd/MM/yyyy}",
            $"Sessões de PC: {pcSessions.Count}",
            $"Sessões de console: {consoleSessions.Count}",
            $"Tempo total PCs: {pcSessions.Sum(entry => entry.ConsumedMinutes)} min",
            $"Tempo total consoles: {consoleSessions.Sum(entry => entry.ConsumedMinutes)} min",
            $"Valor anotado: R$ {ledger.Where(entry => entry.Type == LedgerEntryType.Annotation).Sum(entry => entry.Amount):N2}",
            $"Pagamentos prometidos: R$ {ledger.Where(entry => entry.Type == LedgerEntryType.PaymentPromise).Sum(entry => entry.Amount):N2}",
            $"Usuários atendidos: {sessions.Select(entry => entry.UserAccountId).Where(entry => entry.HasValue).Distinct().Count()}",
            "",
            "Sessões",
            .. sessions.Select(entry =>
            {
                var machineName = machines.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida";
                var userName = entry.UserAccountId.HasValue && users.TryGetValue(entry.UserAccountId.Value, out var user)
                    ? user.DisplayName
                    : entry.UserDisplayName;
                return $"- {machineName} | {userName} | {entry.MachineKind} | {entry.ConsumedMinutes} min | R$ {entry.TotalSpent:N2}";
            }),
            "",
            "Financeiro",
            .. ledger.Select(entry =>
            {
                var userName = users.TryGetValue(entry.UserAccountId, out var user) ? user.DisplayName : "Desconhecido";
                var dueDate = entry.PromisedPaymentDateUtc.HasValue ? $" | vence {entry.PromisedPaymentDateUtc.Value:dd/MM/yyyy}" : string.Empty;
                return $"- {entry.Type} | {userName} | R$ {entry.Amount:N2}{dueDate} | {entry.Description}";
            })
        ];
    }

    private static byte[] BuildExcel(
        IReadOnlyList<string> summaryLines,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Resumo");
        for (var index = 0; index < summaryLines.Count; index++)
        {
            summary.Cell(index + 1, 1).Value = summaryLines[index];
        }

        var sessionsSheet = workbook.Worksheets.Add("Sessoes");
        sessionsSheet.Cell(1, 1).Value = "Máquina";
        sessionsSheet.Cell(1, 2).Value = "Usuário";
        sessionsSheet.Cell(1, 3).Value = "Tipo";
        sessionsSheet.Cell(1, 4).Value = "Minutos";
        sessionsSheet.Cell(1, 5).Value = "Valor";

        for (var row = 0; row < sessions.Count; row++)
        {
            var session = sessions[row];
            sessionsSheet.Cell(row + 2, 1).Value = machines.TryGetValue(session.MachineId, out var machine) ? machine.Name : "Desconhecida";
            sessionsSheet.Cell(row + 2, 2).Value = session.UserAccountId.HasValue && users.TryGetValue(session.UserAccountId.Value, out var user) ? user.DisplayName : session.UserDisplayName;
            sessionsSheet.Cell(row + 2, 3).Value = session.MachineKind.ToString();
            sessionsSheet.Cell(row + 2, 4).Value = session.ConsumedMinutes;
            sessionsSheet.Cell(row + 2, 5).Value = session.TotalSpent;
        }

        var ledgerSheet = workbook.Worksheets.Add("Financeiro");
        ledgerSheet.Cell(1, 1).Value = "Usuário";
        ledgerSheet.Cell(1, 2).Value = "Tipo";
        ledgerSheet.Cell(1, 3).Value = "Valor";
        ledgerSheet.Cell(1, 4).Value = "Descrição";
        ledgerSheet.Cell(1, 5).Value = "Promessa";

        for (var row = 0; row < ledger.Count; row++)
        {
            var item = ledger[row];
            ledgerSheet.Cell(row + 2, 1).Value = users.TryGetValue(item.UserAccountId, out var user) ? user.DisplayName : "Desconhecido";
            ledgerSheet.Cell(row + 2, 2).Value = item.Type.ToString();
            ledgerSheet.Cell(row + 2, 3).Value = item.Amount;
            ledgerSheet.Cell(row + 2, 4).Value = item.Description;
            ledgerSheet.Cell(row + 2, 5).Value = item.PromisedPaymentDateUtc?.ToString("dd/MM/yyyy") ?? string.Empty;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] BuildPdf(
        IReadOnlyList<string> summaryLines,
        IReadOnlyList<SessionRecord> sessions,
        IReadOnlyList<LedgerEntry> ledger,
        IReadOnlyDictionary<Guid, Machine> machines,
        IReadOnlyDictionary<Guid, UserAccount> users)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.DefaultTextStyle(text => text.FontSize(10));
                page.Header().Text("Relatório Adrenalina").SemiBold().FontSize(18);
                page.Content().Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text(string.Join(Environment.NewLine, summaryLines.Take(10)));
                    column.Item().Text("Sessões").SemiBold();
                    foreach (var session in sessions.Take(12))
                    {
                        var machine = machines.TryGetValue(session.MachineId, out var machineEntry) ? machineEntry.Name : "Desconhecida";
                        var user = session.UserAccountId.HasValue && users.TryGetValue(session.UserAccountId.Value, out var userEntry)
                            ? userEntry.DisplayName
                            : session.UserDisplayName;
                        column.Item().Text($"{machine} | {user} | {session.ConsumedMinutes} min | R$ {session.TotalSpent:N2}");
                    }

                    column.Item().Text("Financeiro").SemiBold();
                    foreach (var item in ledger.Take(12))
                    {
                        var user = users.TryGetValue(item.UserAccountId, out var userEntry) ? userEntry.DisplayName : "Desconhecido";
                        column.Item().Text($"{item.Type} | {user} | R$ {item.Amount:N2} | {item.Description}");
                    }
                });
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Página ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }
}
