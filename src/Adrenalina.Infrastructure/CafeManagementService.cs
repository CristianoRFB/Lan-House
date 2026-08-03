using System.Globalization;
using System.Text.Json;
using Adrenalina.Application;
using Adrenalina.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Infrastructure;

public sealed class CafeManagementService(
    AdrenalinaDbContext db,
    AdrenalinaStoragePaths storagePaths,
    AdrenalinaDatabaseInitializer databaseInitializer,
    AdrenalinaReportExporter reportExporter,
    ILogger<CafeManagementService> logger) : ICafeManagementService, IAdminAuthService
{
    public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        await databaseInitializer.InitializeAsync(cancellationToken);
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

        if (user is null || user.IsBlocked || !PasswordHasher.Verify(user.PasswordHash, password))
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
        var nowUtc = DateTime.UtcNow;
        var settings = await GetSettingsEntityAsync(cancellationToken);
        var machines = await db.Machines.AsNoTracking().OrderBy(entry => entry.Name).ToListAsync(cancellationToken);
        var users = await db.Users.AsNoTracking().OrderBy(entry => entry.DisplayName).ToListAsync(cancellationToken);
        var activeSessionCount = await db.Sessions.CountAsync(entry => entry.Status == SessionStatus.Active, cancellationToken);
        var pendingRequestCount = await db.ClientRequests.CountAsync(entry => entry.Status == ClientRequestStatus.Pending, cancellationToken);
        var sessions = await db.Sessions.AsNoTracking().OrderByDescending(entry => entry.StartedAtUtc).Take(20).ToListAsync(cancellationToken);
        var recentUsageSessions = await db.Sessions.AsNoTracking()
            .Where(entry => entry.StartedAtUtc >= nowUtc.AddDays(-30))
            .Select(entry => new
            {
                entry.MachineId,
                entry.StartedAtUtc,
                entry.ConsumedMinutes
            })
            .ToListAsync(cancellationToken);
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
        var promisedPayments = (await db.LedgerEntries.AsNoTracking()
                .Where(entry => entry.Type == LedgerEntryType.PaymentPromise)
                .Select(entry => entry.Amount)
                .ToListAsync(cancellationToken))
            .Sum();

        var machineLookup = machines.ToDictionary(entry => entry.Id);

        var usageByDay = recentUsageSessions
            .Where(entry => entry.StartedAtUtc >= nowUtc.AddDays(-7))
            .GroupBy(entry => entry.StartedAtUtc.Date)
            .OrderBy(group => group.Key)
            .Select(group => new ChartPointDto(group.Key.ToString("dd/MM"), group.Sum(entry => entry.ConsumedMinutes)))
            .ToList();

        var usageByMachine = recentUsageSessions
            .GroupBy(entry => machineLookup.TryGetValue(entry.MachineId, out var machine) ? machine.Name : "Desconhecida")
            .Select(group => new ChartPointDto(group.Key, group.Sum(entry => entry.ConsumedMinutes)))
            .OrderByDescending(point => point.Value)
            .Take(6)
            .ToList();

        return new DashboardDto
        {
            CafeName = settings.CafeName,
            OnlineMachines = machines.Count(entry => IsMachineOnline(entry)),
            ActiveMachines = machines.Count(entry => entry.Status == MachineStatus.InSession),
            ActiveSessions = activeSessionCount,
            PendingRequests = pendingRequestCount,
            PendingAnnotations = users.Sum(entry => entry.PendingAnnotationAmount),
            PromisedPayments = promisedPayments,
            Machines = machines.Select(MapMachine).ToList(),
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

        return machines.Select(MapMachine).ToList();
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
        var cafeName = TextSanitizer.Normalize(request.CafeName);
        if (string.IsNullOrWhiteSpace(cafeName))
        {
            return new OperationResult(false, "Informe o nome da lan house.");
        }

        if (!Enum.IsDefined(request.DefaultTheme) || !Enum.IsDefined(request.UpdateMode) ||
            request.BackupRetentionDays is < 1 or > 3650 ||
            request.DefaultCommonAnnotationLimit is < 0m or > 1_000_000m ||
            request.DefaultPcHourlyRate is < 0m or > 100_000m ||
            request.DefaultConsoleHourlyRate is < 0m or > 100_000m)
        {
            return new OperationResult(false, "Revise tema, retenção e valores padrão informados.");
        }

        var settings = await GetSettingsEntityAsync(cancellationToken);
        settings.CafeName = cafeName;
        settings.DefaultTheme = request.DefaultTheme;
        settings.UpdateMode = request.UpdateMode;
        settings.BackupCutoffLocalTime = request.BackupCutoffLocalTime;
        settings.BackupRetentionDays = request.BackupRetentionDays;
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

        var displayName = TextSanitizer.Normalize(request.DisplayName);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 100)
        {
            return new OperationResult(false, "Informe um nome de usuário com até 100 caracteres.");
        }

        if (!Enum.IsDefined(request.ProfileType)
            || Math.Abs(request.Balance) > 1_000_000m
            || request.AnnotationLimit is < 0m or > 1_000_000m)
        {
            return new OperationResult(false, "Informe perfil e limites financeiros válidos.");
        }

        DateTime? temporaryUntilUtc = null;
        if (request.IsTemporary)
        {
            if (!request.TemporaryUntilUtc.HasValue)
            {
                return new OperationResult(false, "Informe até quando a conta temporária será válida.");
            }

            temporaryUntilUtc = request.TemporaryUntilUtc.Value.Kind == DateTimeKind.Utc
                ? request.TemporaryUntilUtc.Value
                : request.TemporaryUntilUtc.Value.ToUniversalTime();
            if (temporaryUntilUtc <= DateTime.UtcNow)
            {
                return new OperationResult(false, "A validade da conta temporária precisa estar no futuro.");
            }
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
            if (await db.Users.AnyAsync(entry => entry.Id != requestId && entry.Login == login, cancellationToken))
            {
                return new OperationResult(false, "Já existe um usuário com esse login.");
            }
        }

        user.DisplayName = displayName;
        user.Login = login;
        user.ProfileType = request.ProfileType;
        user.Balance = request.Balance;
        user.AnnotationLimit = request.ProfileType == UserProfileType.Common ? request.AnnotationLimit : 0m;
        user.IsTemporary = request.IsTemporary;
        user.TemporaryUntilUtc = temporaryUntilUtc;
        user.Notes = TextSanitizer.Normalize(request.Notes);
        user.IsBlocked = request.IsBlocked;
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
            return new OperationResult(false, "Informe um PIN de 4 dígitos para o novo usuário.");
        }

        var removeInitialAccessFileAfterSave = false;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length is < 12 or > 256)
            {
                return new OperationResult(false, "A senha do painel deve ter entre 12 e 256 caracteres.");
            }

            user.PasswordHash = PasswordHasher.Hash(request.Password);
            if (!isNew && user.ProfileType == UserProfileType.Admin)
            {
                removeInitialAccessFileAfterSave = true;
            }
        }
        else if (isNew && request.ProfileType is UserProfileType.Admin or UserProfileType.Special)
        {
            return new OperationResult(false, "Informe uma senha com pelo menos 12 caracteres para perfis administrativos.");
        }
        else if (isNew)
        {
            user.PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N"));
        }

        await LogAsync("Usuario", isNew ? "Criacao" : "Atualizacao", actorUserId, null, user.Id, $"Conta {user.DisplayName} salva com perfil {user.ProfileType}.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (removeInitialAccessFileAfterSave)
        {
            DeleteInitialAccessFile();
        }
        return new OperationResult(true, isNew ? "Usuário criado." : "Usuário atualizado.");
    }

    public async Task<OperationResult> UpsertMachineAsync(MachineUpsertRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var machineKey = TextSanitizer.Normalize(request.MachineKey).ToLowerInvariant();
        var name = TextSanitizer.Normalize(request.Name);
        if (string.IsNullOrWhiteSpace(machineKey) || string.IsNullOrWhiteSpace(name))
        {
            return new OperationResult(false, "Informe nome e chave da máquina.");
        }

        if (machineKey.Length > 100 || name.Length > 100)
        {
            return new OperationResult(false, "Nome e chave devem ter no máximo 100 caracteres.");
        }

        if (!Enum.IsDefined(request.Kind))
        {
            return new OperationResult(false, "Informe um tipo de máquina válido.");
        }

        var isNew = !request.Id.HasValue || request.Id == Guid.Empty;
        Machine machine;
        if (isNew)
        {
            if (await db.Machines.AnyAsync(entry => entry.MachineKey == machineKey || entry.Name == name, cancellationToken))
            {
                return new OperationResult(false, "Já existe uma máquina com esse nome ou chave.");
            }

            machine = new Machine { Status = MachineStatus.Offline };
            db.Machines.Add(machine);
        }
        else
        {
            var id = request.Id.GetValueOrDefault();
            machine = await db.Machines.FirstOrDefaultAsync(entry => entry.Id == id, cancellationToken)
                ?? throw new InvalidOperationException("Máquina não encontrada.");

            if (await db.Machines.AnyAsync(entry => entry.Id != id && (entry.MachineKey == machineKey || entry.Name == name), cancellationToken))
            {
                return new OperationResult(false, "Já existe outra máquina com esse nome ou chave.");
            }
        }

        machine.MachineKey = machineKey;
        machine.Name = name;
        machine.Kind = request.Kind;
        machine.GroupName = TextSanitizer.Normalize(request.GroupName);
        machine.Observations = TextSanitizer.Normalize(request.Observations);
        machine.Touch();

        await LogAsync("Maquina", isNew ? "Criacao" : "Atualizacao", actorUserId, machine.Id, null, $"Máquina {machine.Name} salva.", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new OperationResult(true, isNew ? "Máquina cadastrada." : "Máquina atualizada.");
    }

    public async Task<OperationResult> AddLedgerEntryAsync(LedgerEntryRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(entry => entry.Id == request.UserAccountId, cancellationToken);
        if (user is null)
        {
            return new OperationResult(false, "Usuário não encontrado.");
        }

        var amount = Math.Round(request.Amount, 2);
        if (amount == 0m || Math.Abs(amount) > 1_000_000m || !Enum.IsDefined(request.Type))
        {
            return new OperationResult(false, "Informe tipo e valor financeiro válidos.");
        }

        if (request.Type is LedgerEntryType.Credit or LedgerEntryType.Annotation or LedgerEntryType.PaymentPromise && amount < 0m)
        {
            return new OperationResult(false, "Use valores positivos para crédito, anotação e promessa de pagamento.");
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
            if (user is null)
            {
                return new OperationResult(false, "Usuário não encontrado.");
            }

            if (user.IsBlocked || user.TemporaryUntilUtc.HasValue && user.TemporaryUntilUtc < DateTime.UtcNow)
            {
                return new OperationResult(false, "A conta informada está bloqueada ou expirada.");
            }
        }

        var profile = user?.ProfileType ?? UserProfileType.Ghost;
        var displayName = user?.DisplayName ?? TextSanitizer.Normalize(request.UserDisplayName);
        if (profile == UserProfileType.Common && !request.IsDemoMode && request.GrantedMinutes <= 0)
        {
            return new OperationResult(false, "Usuários comuns precisam iniciar com tempo maior que zero.");
        }

        if (request.GrantedMinutes is < 0 or > 43_200 || request.HourlyRate is < 0m or > 100_000m)
        {
            return new OperationResult(false, "Minutos ou valor por hora estão fora do limite permitido.");
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
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return new OperationResult(true, "Sessão iniciada.");
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "Concorrência detectada ao iniciar sessão na máquina {MachineId}.", machine.Id);
            return new OperationResult(false, "Essa máquina já recebeu outra sessão. Atualize a tela e tente novamente.");
        }
    }

    public async Task<OperationResult> AdjustSessionAsync(SessionAdjustRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(entry => entry.Id == request.SessionId, cancellationToken);
        if (session is null)
        {
            return new OperationResult(false, "Sessão não encontrada.");
        }

        if (session.Status != SessionStatus.Active)
        {
            return new OperationResult(false, "Somente sessões ativas podem ser ajustadas.");
        }

        if (request.AdditionalMinutes is < -1_440 or > 43_200 ||
            Math.Abs(request.AdditionalAnnotationAmount) > 1_000_000m ||
            request.AdditionalMinutes == 0 && request.AdditionalAnnotationAmount == 0m)
        {
            return new OperationResult(false, "Informe um ajuste de tempo ou anotação dentro dos limites permitidos.");
        }

        if (session.RemainingMinutes + request.AdditionalMinutes < 0 ||
            session.GrantedMinutes + request.AdditionalMinutes < 0)
        {
            return new OperationResult(false, "O ajuste não pode deixar o tempo da sessão negativo.");
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
        if (request.Type is not (RemoteCommandType.LockScreen or RemoteCommandType.RefreshConfiguration or
            RemoteCommandType.ShowMessage or RemoteCommandType.ToggleTimerVisibility))
        {
            return new OperationResult(false, "Esse tipo de comando não é permitido pelo cliente seguro.");
        }

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

        if (entry.Status != ClientRequestStatus.Pending)
        {
            return new OperationResult(false, "Essa solicitação já foi processada.");
        }

        entry.Status = request.Approve ? ClientRequestStatus.Approved : ClientRequestStatus.Rejected;
        entry.ResolvedAtUtc = DateTime.UtcNow;
        entry.ResolvedByUserId = actorUserId;
        entry.AdminResponse = TextSanitizer.Normalize(request.ResponseMessage);
        entry.Touch();

        if (request.Approve && entry.Type == ClientRequestType.Registration)
        {
            var login = entry.RequestedLogin.Trim().ToLowerInvariant();
            if (!LoginRules.LooksLikeLetterLogin(login) || await db.Users.AnyAsync(user => user.Login == login, cancellationToken))
            {
                return new OperationResult(false, "O login solicitado é inválido ou já está em uso.");
            }

            using var payload = JsonDocument.Parse(entry.PayloadJson);
            var pinHash = payload.RootElement.TryGetProperty("pinHash", out var pinHashProperty)
                ? pinHashProperty.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(pinHash))
            {
                return new OperationResult(false, "A solicitação não contém um PIN válido.");
            }

            var settings = await GetSettingsEntityAsync(cancellationToken);
            var user = new UserAccount
            {
                DisplayName = string.IsNullOrWhiteSpace(entry.RequestedDisplayName) ? login : entry.RequestedDisplayName,
                Login = login,
                PinHash = pinHash,
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),
                ProfileType = UserProfileType.Common,
                AnnotationLimit = settings.DefaultCommonAnnotationLimit
            };
            db.Users.Add(user);
            entry.UserAccountId = user.Id;
        }
        else if (request.Approve && entry.Type == ClientRequestType.MoreTime)
        {
            var session = await db.Sessions
                .FirstOrDefaultAsync(item => item.MachineId == entry.MachineId && item.Status == SessionStatus.Active, cancellationToken);
            if (session is null)
            {
                return new OperationResult(false, "Não há sessão ativa nessa máquina para adicionar tempo.");
            }

            using var payload = JsonDocument.Parse(entry.PayloadJson);
            var requestedMinutes = payload.RootElement.TryGetProperty("amount", out var amountProperty) && amountProperty.TryGetDecimal(out var amount)
                ? (int)Math.Round(amount)
                : 0;
            var additionalMinutes = Math.Clamp(requestedMinutes <= 0 ? 30 : requestedMinutes, 1, 720);
            session.GrantedMinutes += additionalMinutes;
            session.RemainingMinutes += additionalMinutes;
            session.Touch();
            entry.UserAccountId = session.UserAccountId;
        }

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
        var snapshot = await CreateBackupSnapshotAsync(settings, actorUserId, cancellationToken);
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

        return reportExporter.Export(settings.CafeName, request, sessions, ledger, machines, users);
    }

    public async Task<ClientLoginResponse> LoginClientAsync(ClientLoginRequest request, CancellationToken cancellationToken = default)
    {
        var machineKey = TextSanitizer.Normalize(request.MachineKey);
        var login = TextSanitizer.Normalize(request.Login).ToLowerInvariant();
        var pin = TextSanitizer.Normalize(request.Pin);

        if (machineKey.Length > 100)
        {
            return new ClientLoginResponse { Success = false, Message = "Identificação da máquina inválida." };
        }

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
        if (user is null || user.IsBlocked || !PasswordHasher.Verify(user.PinHash, pin))
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
        var machineKey = TextSanitizer.Normalize(request.MachineKey).ToLowerInvariant();
        if (machineKey.Length > 100 || !Enum.IsDefined(request.Status))
        {
            return new ClientHeartbeatResponse { Success = false, Message = "Heartbeat inválido." };
        }

        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.MachineKey == machineKey, cancellationToken);
        if (machine is null)
        {
            return new ClientHeartbeatResponse
            {
                Success = false,
                Message = "Máquina não cadastrada. Cadastre no painel ADMIN usando a mesma chave."
            };
        }

        var hostname = TextSanitizer.Normalize(request.Hostname);
        var ipAddress = TextSanitizer.Normalize(request.IpAddress);
        machine.Hostname = hostname[..Math.Min(100, hostname.Length)];
        machine.IpAddress = ipAddress[..Math.Min(64, ipAddress.Length)];
        machine.Status = request.Status;
        machine.LastSeenUtc = DateTime.UtcNow;
        machine.Touch();

        var acknowledgedCommandIds = request.AcknowledgedCommandIds.Take(100).ToList();
        if (acknowledgedCommandIds.Count > 0)
        {
            var acknowledgedCommands = await db.RemoteCommands
                .Where(entry => entry.MachineId == machine.Id && acknowledgedCommandIds.Contains(entry.Id))
                .ToListAsync(cancellationToken);
            foreach (var command in acknowledgedCommands)
            {
                command.Status = RemoteCommandStatus.Completed;
                command.ExecutedAtUtc = DateTime.UtcNow;
                command.ResultSummary = "Confirmado pelo Client.";
                command.Touch();
            }
        }

        var acknowledgedNotificationIds = request.AcknowledgedNotificationIds.Take(100).ToList();
        if (acknowledgedNotificationIds.Count > 0)
        {
            var acknowledgedNotifications = await db.Notifications
                .Where(entry => entry.MachineId == machine.Id && acknowledgedNotificationIds.Contains(entry.Id))
                .ToListAsync(cancellationToken);
            foreach (var notification in acknowledgedNotifications)
            {
                notification.IsReadByClient = true;
                notification.Touch();
            }
        }

        if (acknowledgedCommandIds.Count > 0 || acknowledgedNotificationIds.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
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
            .Where(entry => entry.MachineId == machine.Id &&
                            (entry.Status == RemoteCommandStatus.Pending || entry.Status == RemoteCommandStatus.Delivered))
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

        await db.SaveChangesAsync(cancellationToken);

        return new ClientHeartbeatResponse
        {
            Success = true,
            Message = "Sincronização concluída.",
            MachineId = machine.Id,
            Settings = MapSettings(settings),
            RuntimeState = BuildRuntimeState(settings, machine, session, user, notifications),
            Commands = commands.Select(entry => new RemoteCommandEnvelope(entry.Id, entry.Type, entry.Title, entry.Message, entry.PayloadJson)).ToList(),
            Notifications = notifications.Select(entry => new NotificationEnvelope(entry.Id, entry.Title, entry.Message, entry.Severity, entry.PlaySound)).ToList()
        };
    }

    public async Task<OperationResult> SubmitClientRequestsAsync(ClientRequestBatchRequest request, CancellationToken cancellationToken = default)
    {
        var machineKey = TextSanitizer.Normalize(request.MachineKey).ToLowerInvariant();
        if (machineKey.Length > 100)
        {
            return new OperationResult(false, "Identificação da máquina inválida.");
        }

        var machine = await db.Machines.FirstOrDefaultAsync(entry => entry.MachineKey == machineKey, cancellationToken);
        if (machine is null)
        {
            return new OperationResult(false, "Máquina não registrada.");
        }

        if (request.Requests.Count > 20)
        {
            return new OperationResult(false, "Envie no máximo 20 solicitações por lote.");
        }

        var normalizedRequests = request.Requests
            .Select(item => new { Item = item, Id = item.RequestId == Guid.Empty ? Guid.NewGuid() : item.RequestId })
            .ToList();
        if (normalizedRequests.Select(item => item.Id).Distinct().Count() != normalizedRequests.Count)
        {
            return new OperationResult(false, "O lote contém identificadores de solicitação duplicados.");
        }

        var requestIds = normalizedRequests.Select(item => item.Id).ToList();
        var existingRequestIds = (await db.ClientRequests.AsNoTracking()
            .Where(item => requestIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();
        var logins = normalizedRequests
            .Select(item => TextSanitizer.Normalize(item.Item.Login).ToLowerInvariant())
            .Where(login => !string.IsNullOrWhiteSpace(login))
            .Distinct()
            .ToList();
        var userIdsByLogin = await db.Users.AsNoTracking()
            .Where(user => logins.Contains(user.Login))
            .ToDictionaryAsync(user => user.Login, user => user.Id, cancellationToken);
        var addedCount = 0;

        foreach (var requestItem in normalizedRequests)
        {
            if (existingRequestIds.Contains(requestItem.Id))
            {
                continue;
            }

            var item = requestItem.Item;
            if (!Enum.IsDefined(item.Type) || Math.Abs(item.Amount) > 43_200m)
            {
                return new OperationResult(false, "A solicitação contém tipo ou valor inválido.");
            }

            var login = TextSanitizer.Normalize(item.Login).ToLowerInvariant();
            var displayName = TextSanitizer.Normalize(item.DisplayName);
            if (item.Type == ClientRequestType.Registration &&
                (!LoginRules.LooksLikeLetterLogin(login) ||
                 (!LoginRules.LooksLikeFourDigitPin(item.Pin) && string.IsNullOrWhiteSpace(item.PinHash)) ||
                 string.IsNullOrWhiteSpace(displayName)))
            {
                return new OperationResult(false, "Cadastro requer nome, login válido e PIN de 4 dígitos.");
            }

            if (!string.IsNullOrWhiteSpace(item.PinHash) && !PasswordHasher.IsHashFormatValid(item.PinHash))
            {
                return new OperationResult(false, "O hash do PIN informado é inválido.");
            }

            var existingUserId = userIdsByLogin.TryGetValue(login, out var userId) ? userId : (Guid?)null;
            var safePayload = JsonSerializer.Serialize(new
            {
                message = TextSanitizer.Normalize(item.Message),
                amount = item.Amount,
                pinHash = !string.IsNullOrWhiteSpace(item.PinHash)
                    ? item.PinHash
                    : LoginRules.LooksLikeFourDigitPin(item.Pin) ? PasswordHasher.Hash(item.Pin) : string.Empty
            }, JsonDefaults.Options);

            db.ClientRequests.Add(new ClientRequestRecord
            {
                Id = requestItem.Id,
                MachineId = machine.Id,
                UserAccountId = existingUserId,
                Type = item.Type,
                RequestedLogin = login,
                RequestedDisplayName = displayName,
                PayloadJson = safePayload,
                RequestedAtUtc = item.OccurredAtUtc < DateTime.UtcNow.AddDays(-30) || item.OccurredAtUtc > DateTime.UtcNow.AddMinutes(5)
                    ? DateTime.UtcNow
                    : item.OccurredAtUtc
            });
            addedCount++;
        }

        if (addedCount > 0)
        {
            await LogAsync("Cliente", "Solicitacao", null, machine.Id, null, $"{addedCount} solicitações novas recebidas de {machine.Name}.", cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return new OperationResult(true, addedCount == 0 ? "Solicitações já sincronizadas anteriormente." : "Solicitações sincronizadas.");
    }

    public async Task RunMaintenanceTickAsync(CancellationToken cancellationToken = default)
    {
        await UpdateSessionsAsync(cancellationToken);
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

    private async Task<BackupSnapshot> CreateBackupSnapshotAsync(
        AdminSettings settings,
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
                Summary = "Backup manual concluído.",
                ExecutedAtUtc = DateTime.UtcNow
            };

            db.Backups.Add(snapshot);
            await LogAsync(
                "Backup",
                "Manual",
                actorUserId,
                null,
                null,
                $"Backup manual gerado em {destination}.",
                cancellationToken);

            return snapshot;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao gerar backup manual.");

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

    private void DeleteInitialAccessFile()
    {
        var root = Path.GetDirectoryName(storagePaths.DatabaseFilePath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var path = Path.Combine(root, AdrenalinaDatabaseInitializer.InitialAccessFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
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
        var settings = await db.Settings
            .OrderBy(entry => entry.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = new AdminSettings();
        db.Settings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
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

    private static MachineDto MapMachine(Machine machine) => new()
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
        LastSeenUtc = machine.LastSeenUtc
    };

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
        Notes = entry.Notes,
        IsBlocked = entry.IsBlocked
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

}
