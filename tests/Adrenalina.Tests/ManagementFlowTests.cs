using Adrenalina.Application;
using Adrenalina.Domain;

namespace Adrenalina.Tests;

public sealed class ManagementFlowTests
{
    [Fact]
    public async Task DatabaseCreationAndSeedAreIdempotent()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        Assert.True(File.Exists(environment.DatabasePath));

        await environment.RunAsync(service => service.EnsureInitializedAsync());
        await environment.RunAsync(service => service.EnsureInitializedAsync());

        var users = await environment.RunAsync(service => service.GetUsersAsync());
        Assert.Equal(2, users.Count);
        Assert.Single(users, user => user.Login == "admin");
    }

    [Fact]
    public async Task AdminAuthenticationAcceptsSeedAndRejectsBlockedAccount()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var authenticated = await environment.RunAsync(service =>
            ((IAdminAuthService)service).ValidateAsync("admin", environment.InitialAdminPassword));
        Assert.NotNull(authenticated);

        var admin = (await environment.RunAsync(service => service.GetUsersAsync())).Single(user => user.Login == "admin");
        const string newPassword = "Senha-Nova-Segura-2026!";
        var passwordChanged = await environment.RunAsync(service => service.UpsertUserAsync(new UserUpsertRequest
        {
            Id = admin.Id,
            DisplayName = admin.DisplayName,
            Login = admin.Login,
            ProfileType = admin.ProfileType,
            Password = newPassword
        }, admin.Id));
        Assert.True(passwordChanged.Success);
        Assert.False(File.Exists(Path.Combine(environment.RootPath, Adrenalina.Infrastructure.AdrenalinaDatabaseInitializer.InitialAccessFileName)));
        Assert.Null(await environment.RunAsync(service =>
            ((IAdminAuthService)service).ValidateAsync("admin", environment.InitialAdminPassword)));
        Assert.NotNull(await environment.RunAsync(service =>
            ((IAdminAuthService)service).ValidateAsync("admin", newPassword)));

        var blockedResult = await environment.RunAsync(service => service.UpsertUserAsync(new UserUpsertRequest
        {
            Id = admin.Id,
            DisplayName = admin.DisplayName,
            Login = admin.Login,
            ProfileType = admin.ProfileType,
            IsBlocked = true
        }, admin.Id));
        Assert.True(blockedResult.Success);

        var blocked = await environment.RunAsync(service =>
            ((IAdminAuthService)service).ValidateAsync("admin", newPassword));
        Assert.Null(blocked);
    }

    [Fact]
    public async Task MachineCanBeRegisteredQueriedAndSynchronized()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var adminId = await GetAdminIdAsync(environment);

        var result = await environment.RunAsync(service => service.UpsertMachineAsync(new MachineUpsertRequest
        {
            Name = "PC-TESTE",
            MachineKey = "pc-teste-chave",
            Kind = MachineKind.Pc,
            GroupName = "Laboratorio"
        }, adminId));
        Assert.True(result.Success);

        var machines = await environment.RunAsync(service => service.GetMachinesAsync());
        var machine = Assert.Single(machines);
        Assert.Equal("pc-teste-chave", machine.MachineKey);

        var heartbeat = await environment.RunAsync(service => service.SyncClientHeartbeatAsync(new ClientHeartbeatRequest
        {
            MachineKey = machine.MachineKey,
            Hostname = "HOST-TESTE",
            IpAddress = "127.0.0.1",
            Status = MachineStatus.Idle
        }));
        Assert.True(heartbeat.Success);

        var unknown = await environment.RunAsync(service => service.SyncClientHeartbeatAsync(new ClientHeartbeatRequest
        {
            MachineKey = "nao-cadastrada"
        }));
        Assert.False(unknown.Success);
    }

    [Fact]
    public async Task PinLoginStartsSessionAndTimeCanBeAdjustedAndEnded()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var (adminId, machineId) = await PrepareMachineAndCreditAsync(environment);

        var login = await environment.RunAsync(service => service.LoginClientAsync(new ClientLoginRequest
        {
            MachineKey = "pc-fluxo-chave",
            Login = "cliente",
            Pin = "2222"
        }));
        Assert.True(login.Success);
        Assert.NotNull(login.RuntimeState.CurrentSessionId);

        var sessionId = login.RuntimeState.CurrentSessionId!.Value;
        var adjustment = await environment.RunAsync(service => service.AdjustSessionAsync(new SessionAdjustRequest
        {
            SessionId = sessionId,
            AdditionalMinutes = 20,
            Reason = "Teste seguro"
        }, adminId));
        Assert.True(adjustment.Success);

        var adjusted = (await environment.RunAsync(service => service.GetSessionsAsync())).Single(session => session.Id == sessionId);
        Assert.True(adjusted.RemainingMinutes >= 20);

        var ended = await environment.RunAsync(service => service.EndSessionAsync(sessionId, "Fim do teste", adminId));
        Assert.True(ended.Success);
        var finalSession = (await environment.RunAsync(service => service.GetSessionsAsync())).Single(session => session.Id == sessionId);
        Assert.Equal(SessionStatus.Finished, finalSession.Status);
        Assert.Equal(machineId, finalSession.MachineId);
    }

    [Fact]
    public async Task RegistrationAndMoreTimeRequestsCanBeApprovedOrRejected()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var (adminId, _) = await PrepareMachineAndCreditAsync(environment);
        var login = await environment.RunAsync(service => service.LoginClientAsync(new ClientLoginRequest
        {
            MachineKey = "pc-fluxo-chave",
            Login = "cliente",
            Pin = "2222"
        }));
        Assert.True(login.Success);

        var sent = await environment.RunAsync(service => service.SubmitClientRequestsAsync(new ClientRequestBatchRequest
        {
            MachineKey = "pc-fluxo-chave",
            Requests =
            [
                new ClientShellRequest { Type = ClientRequestType.MoreTime, Login = "cliente", Amount = 15 },
                new ClientShellRequest { Type = ClientRequestType.Registration, Login = "novo", Pin = "9876", DisplayName = "Novo Cliente" },
                new ClientShellRequest { Type = ClientRequestType.Registration, Login = "rejeitado", Pin = "8765", DisplayName = "Rejeitado" }
            ]
        }));
        Assert.True(sent.Success);

        var pending = await environment.RunAsync(service => service.GetPendingRequestsAsync());
        var moreTime = pending.Single(request => request.Type == ClientRequestType.MoreTime);
        var registration = pending.Single(request => request.RequestedLogin == "novo");
        var rejectedRegistration = pending.Single(request => request.RequestedLogin == "rejeitado");

        Assert.True((await environment.RunAsync(service => service.ResolveClientRequestAsync(
            new ClientRequestResolution { RequestId = moreTime.Id, Approve = true }, adminId))).Success);
        Assert.False((await environment.RunAsync(service => service.ResolveClientRequestAsync(
            new ClientRequestResolution { RequestId = moreTime.Id, Approve = true }, adminId))).Success);
        Assert.True((await environment.RunAsync(service => service.ResolveClientRequestAsync(
            new ClientRequestResolution { RequestId = registration.Id, Approve = true }, adminId))).Success);
        Assert.True((await environment.RunAsync(service => service.ResolveClientRequestAsync(
            new ClientRequestResolution { RequestId = rejectedRegistration.Id, Approve = false }, adminId))).Success);

        var users = await environment.RunAsync(service => service.GetUsersAsync());
        Assert.Contains(users, user => user.Login == "novo");
        Assert.DoesNotContain(users, user => user.Login == "rejeitado");

        var session = (await environment.RunAsync(service => service.GetSessionsAsync())).Single(item => item.Id == login.RuntimeState.CurrentSessionId);
        Assert.True(session.RemainingMinutes >= 15);
    }

    [Fact]
    public async Task ReplayedClientRequestIsStoredOnlyOnce()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        await PrepareMachineAndCreditAsync(environment);
        var requestId = Guid.NewGuid();
        var batch = new ClientRequestBatchRequest
        {
            MachineKey = "pc-fluxo-chave",
            Requests =
            [
                new ClientShellRequest
                {
                    RequestId = requestId,
                    Type = ClientRequestType.MoreTime,
                    Login = "cliente",
                    Amount = 30
                }
            ]
        };

        Assert.True((await environment.RunAsync(service => service.SubmitClientRequestsAsync(batch))).Success);
        Assert.True((await environment.RunAsync(service => service.SubmitClientRequestsAsync(batch))).Success);

        var pending = await environment.RunAsync(service => service.GetPendingRequestsAsync());
        Assert.Single(pending, item => item.Id == requestId);
    }

    [Fact]
    public async Task UnsafeRemoteCommandsAreRejected()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var (adminId, machineId) = await PrepareMachineAndCreditAsync(environment);

        var result = await environment.RunAsync(service => service.QueueMachineCommandAsync(
            new MachineCommandRequest
            {
                MachineId = machineId,
                Type = (RemoteCommandType)2,
                Title = "Reiniciar"
            },
            adminId));

        Assert.False(result.Success);
        Assert.Contains("não é permitido", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommandIsRedeliveredUntilClientAcknowledgesIt()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var (adminId, machineId) = await PrepareMachineAndCreditAsync(environment);
        Assert.True((await environment.RunAsync(service => service.QueueMachineCommandAsync(
            new MachineCommandRequest
            {
                MachineId = machineId,
                Type = RemoteCommandType.ShowMessage,
                Title = "Aviso",
                Message = "Teste de entrega"
            },
            adminId))).Success);

        var heartbeat = new ClientHeartbeatRequest { MachineKey = "pc-fluxo-chave", Status = MachineStatus.Idle };
        var first = await environment.RunAsync(service => service.SyncClientHeartbeatAsync(heartbeat));
        var second = await environment.RunAsync(service => service.SyncClientHeartbeatAsync(heartbeat));
        Assert.Single(first.Commands);
        Assert.Equal(first.Commands.Single().Id, second.Commands.Single().Id);

        var acknowledged = await environment.RunAsync(service => service.SyncClientHeartbeatAsync(new ClientHeartbeatRequest
        {
            MachineKey = "pc-fluxo-chave",
            Status = MachineStatus.Idle,
            AcknowledgedCommandIds = [first.Commands.Single().Id]
        }));
        Assert.Empty(acknowledged.Commands);
    }

    [Fact]
    public async Task ReportsExportInAllSupportedFormats()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var today = DateOnly.FromDateTime(DateTime.Today);

        var text = await environment.RunAsync(service => service.ExportReportAsync(new ReportFilterRequest
        {
            StartDate = today,
            EndDate = today,
            Format = ReportExportFormat.Txt
        }));
        var excel = await environment.RunAsync(service => service.ExportReportAsync(new ReportFilterRequest
        {
            StartDate = today,
            EndDate = today,
            Format = ReportExportFormat.Excel
        }));
        var pdf = await environment.RunAsync(service => service.ExportReportAsync(new ReportFilterRequest
        {
            StartDate = today,
            EndDate = today,
            Format = ReportExportFormat.Pdf
        }));

        Assert.NotNull(text);
        Assert.NotNull(excel);
        Assert.NotNull(pdf);
        Assert.Contains("Adrenalina", System.Text.Encoding.UTF8.GetString(text.Content));
        Assert.Equal("PK", System.Text.Encoding.ASCII.GetString(excel.Content, 0, 2));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Content, 0, 4));

        var invalid = await environment.RunAsync(service => service.ExportReportAsync(new ReportFilterRequest
        {
            StartDate = today,
            EndDate = today.AddDays(-1),
            Format = ReportExportFormat.Txt
        }));
        Assert.Null(invalid);
    }

    [Fact]
    public async Task ManualBackupCreatesAValidSQLiteCopyInTemporaryStorage()
    {
        await using var environment = await TestEnvironment.CreateAsync();
        var adminId = await GetAdminIdAsync(environment);

        var result = await environment.RunAsync(service => service.CreateManualBackupAsync(adminId));

        Assert.True(result.Success, result.Message);
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(environment.RootPath, "backups"), "*.db"));
        var header = new byte[15];
        await using var stream = File.OpenRead(backup);
        _ = await stream.ReadAsync(header);
        Assert.Equal("SQLite format 3", System.Text.Encoding.ASCII.GetString(header));
    }

    private static async Task<Guid> GetAdminIdAsync(TestEnvironment environment) =>
        (await environment.RunAsync(service => service.GetUsersAsync())).Single(user => user.Login == "admin").Id;

    private static async Task<(Guid AdminId, Guid MachineId)> PrepareMachineAndCreditAsync(TestEnvironment environment)
    {
        var users = await environment.RunAsync(service => service.GetUsersAsync());
        var adminId = users.Single(user => user.Login == "admin").Id;
        var clientId = users.Single(user => user.Login == "cliente").Id;
        Assert.True((await environment.RunAsync(service => service.UpsertMachineAsync(new MachineUpsertRequest
        {
            Name = "PC-FLUXO",
            MachineKey = "pc-fluxo-chave",
            Kind = MachineKind.Pc
        }, adminId))).Success);
        Assert.True((await environment.RunAsync(service => service.AddLedgerEntryAsync(new LedgerEntryRequest
        {
            UserAccountId = clientId,
            Type = LedgerEntryType.Credit,
            Amount = 24m,
            Description = "Crédito do teste"
        }, adminId))).Success);

        var machineId = (await environment.RunAsync(service => service.GetMachinesAsync())).Single().Id;
        return (adminId, machineId);
    }
}
