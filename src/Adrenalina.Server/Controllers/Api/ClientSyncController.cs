using Adrenalina.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Adrenalina.Server.Infrastructure;
using System.Net;
using Adrenalina.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Server.Controllers.Api;

[ApiController]
[Route("api/client")]
[EnableRateLimiting("client-api")]
public sealed class ClientSyncController(
    ICafeManagementService cafeService,
    MachineReplayGuard replayGuard,
    AdrenalinaDbContext db,
    IWebHostEnvironment environment,
    ILogger<ClientSyncController> logger) : ControllerBase
{
    private static bool IsProtocolSupported(int version) =>
        version is >= ProtocolContract.MinimumSupportedVersion and <= ProtocolContract.CurrentVersion;

    private object ProtocolError() => new
    {
        success = false,
        message = "Versão do protocolo do Client não suportada.",
        currentVersion = ProtocolContract.CurrentVersion,
        minimumSupportedVersion = ProtocolContract.MinimumSupportedVersion
    };

    private async Task<bool> IsMachineAuthenticatedAsync(
        string machineKey,
        Guid? machineId,
        string machineCredentialId,
        DateTime timestampUtc,
        string nonce,
        string proof,
        string operation,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(proof) &&
            environment.IsDevelopment() &&
            HttpContext.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address))
        {
            return true;
        }

        var machine = await db.Machines.AsNoTracking()
            .Where(machine => (machineId.HasValue && machine.Id == machineId.Value && machine.MachineCredentialId == machineCredentialId) ||
                              (!machineId.HasValue && machine.MachineKey == machineKey))
            .Where(machine => !machine.IsRevoked)
            .Select(machine => new { machine.MachineCredentialHash })
            .FirstOrDefaultAsync(cancellationToken);
        if (machine is null)
        {
            logger.LogWarning("Client request rejected for unregistered machine during {Operation} from {RemoteIp}.",
                operation,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return false;
        }

        var credentialHash = machine.MachineCredentialHash;
        var signingKey = string.IsNullOrWhiteSpace(credentialHash)
            ? MachineAuthentication.DeriveSigningKey(machineKey)
            : credentialHash;

        if (!MachineAuthentication.VerifyProofWithSigningKey(signingKey, timestampUtc, nonce, operation, proof))
        {
            logger.LogWarning("Client request rejected for invalid machine proof during {Operation} from {RemoteIp}.",
                operation,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return false;
        }

        if (!replayGuard.TryAccept($"{operation}:{machineKey}:{nonce}"))
        {
            logger.LogWarning("Client request rejected as replay during {Operation} from {RemoteIp}.",
                operation,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            return false;
        }

        return true;
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ClientHeartbeatResponse>> Heartbeat([FromBody] ClientHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        if (!await IsMachineAuthenticatedAsync(request.MachineKey, request.MachineId, request.MachineCredentialId, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "heartbeat", cancellationToken))
        {
            return Unauthorized(new { success = false, message = "Autenticação da máquina inválida ou expirada." });
        }

        var observedRequest = new ClientHeartbeatRequest
        {
            MachineKey = request.MachineKey,
            MachineId = request.MachineId,
            MachineCredentialId = request.MachineCredentialId,
            RequestTimestampUtc = request.RequestTimestampUtc,
            Nonce = request.Nonce,
            MachineProof = request.MachineProof,
            Hostname = request.Hostname,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? request.IpAddress,
            Status = request.Status,
            ClientVersion = request.ClientVersion,
            AgentVersion = request.AgentVersion,
            AgentHealthy = request.AgentHealthy,
            PolicyVersion = request.PolicyVersion,
            AcknowledgedCommandIds = request.AcknowledgedCommandIds,
            AcknowledgedNotificationIds = request.AcknowledgedNotificationIds
        };
        var response = await cafeService.SyncClientHeartbeatAsync(observedRequest, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    [EnableRateLimiting("client-login")]
    public async Task<ActionResult<ClientLoginResponse>> Login([FromBody] ClientLoginRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        if (!await IsMachineAuthenticatedAsync(request.MachineKey, request.MachineId, request.MachineCredentialId, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "login", cancellationToken))
        {
            return Unauthorized(new { success = false, message = "Autenticação da máquina inválida ou expirada." });
        }

        var response = await cafeService.LoginClientAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<OperationResult>> Requests([FromBody] ClientRequestBatchRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        if (!await IsMachineAuthenticatedAsync(request.MachineKey, request.MachineId, request.MachineCredentialId, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "requests", cancellationToken))
        {
            return Unauthorized(new { success = false, message = "Autenticação da máquina inválida ou expirada." });
        }

        var response = await cafeService.SubmitClientRequestsAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("pairing/request")]
    [EnableRateLimiting("client-pairing-request")]
    public async Task<ActionResult<ClientPairingResponse>> PairingRequest([FromBody] ClientPairingRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        return Ok(await cafeService.RequestMachinePairingAsync(request, cancellationToken));
    }

    [HttpPost("pairing/poll")]
    [EnableRateLimiting("client-pairing-poll")]
    public async Task<ActionResult<ClientPairingResponse>> PairingPoll([FromBody] ClientPairingPollRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        return Ok(await cafeService.PollMachinePairingAsync(request, cancellationToken));
    }
}
