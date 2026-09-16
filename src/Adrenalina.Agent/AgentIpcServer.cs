using Adrenalina.Application;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Agent;

internal sealed class AgentIpcServer(WindowsPolicyEnforcer enforcer, ILogger logger)
{
    private static readonly HashSet<string> AllowedActions = ["lock", "unlock", "enter-maintenance", "exit-maintenance", "restart", "shutdown", "logoff"];

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = CreatePipe();
                await server.WaitForConnectionAsync(cancellationToken);
                await HandleConnectionAsync(server, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Falha no IPC do Agent; nova tentativa será feita.");
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(pipe);
        await using var writer = new StreamWriter(pipe) { AutoFlush = true };
        var line = await reader.ReadLineAsync(cancellationToken);
        var request = line is null ? null : JsonSerializer.Deserialize<StationAgentRequest>(line, JsonDefaults.Options);
        var response = Validate(request) ? Execute(request!) : new StationAgentResponse(false, false, "Comando do Agent rejeitado.");
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonDefaults.Options));
    }

    private StationAgentResponse Execute(StationAgentRequest request)
    {
        var success = request.Action switch
        {
            "lock" => LockStation(),
            "unlock" => UnlockStation(),
            "enter-maintenance" => EnterMaintenance(),
            "exit-maintenance" => ExitMaintenance(),
            "restart" => enforcer.Restart(),
            "shutdown" => enforcer.Shutdown(),
            "logoff" => enforcer.Logoff(),
            _ => false
        };
        return new StationAgentResponse(success, success, success ? "Comando aplicado." : "Não foi possível aplicar o comando.");
    }

    private bool RestoreForSession()
    {
        return enforcer.RestoreNormal();
    }

    private bool LockStation()
    {
        enforcer.SetSessionActive(false);
        return true;
    }

    private bool UnlockStation()
    {
        enforcer.SetSessionActive(true);
        return true;
    }

    private bool EnterMaintenance()
    {
        enforcer.EnterMaintenance();
        return true;
    }

    private bool ExitMaintenance()
    {
        enforcer.ExitMaintenance();
        return true;
    }

    private static bool Validate(StationAgentRequest? request)
    {
        if (request is null || request.Protocol != StationAgentProtocol.Version || !AllowedActions.Contains(request.Action) ||
            request.IssuedAtUtc.Kind != DateTimeKind.Utc || Math.Abs((DateTime.UtcNow - request.IssuedAtUtc).TotalMinutes) > 2 ||
            string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length > 100)
        {
            return false;
        }

        var secret = AgentSecretStore.Load();
        return !string.IsNullOrWhiteSpace(secret) &&
               MachineAuthentication.VerifyProof(secret, request.IssuedAtUtc, request.Nonce, request.Action, request.Proof);
    }

    private static NamedPipeServerStream CreatePipe()
    {
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            StationAgentProtocol.PipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            4096,
            4096,
            security);
    }
}
