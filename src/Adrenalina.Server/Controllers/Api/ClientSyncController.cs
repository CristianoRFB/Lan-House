using Adrenalina.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Adrenalina.Server.Infrastructure;
using System.Net;
using Adrenalina.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Adrenalina.Server.Controllers.Api;

[ApiController]
[Route("api/client")]
[EnableRateLimiting("client-api")]
public sealed class ClientSyncController(ICafeManagementService cafeService, MachineReplayGuard replayGuard, AdrenalinaDbContext db) : ControllerBase
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

    private bool IsMachineAuthenticated(string machineKey, DateTime timestampUtc, string nonce, string proof, string operation)
    {
        if (string.IsNullOrWhiteSpace(proof) &&
            HttpContext.Connection.RemoteIpAddress is { } address && IPAddress.IsLoopback(address))
        {
            return true;
        }

        var credentialHash = db.Machines.AsNoTracking()
            .Where(machine => machine.MachineKey == machineKey)
            .Select(machine => machine.MachineCredentialHash)
            .FirstOrDefault();
        var signingKey = string.IsNullOrWhiteSpace(credentialHash)
            ? MachineAuthentication.DeriveSigningKey(machineKey)
            : credentialHash;

        return MachineAuthentication.VerifyProofWithSigningKey(signingKey, timestampUtc, nonce, operation, proof) &&
               replayGuard.TryAccept($"{operation}:{machineKey}:{nonce}");
    }

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ClientHeartbeatResponse>> Heartbeat([FromBody] ClientHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        if (!IsMachineAuthenticated(request.MachineKey, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "heartbeat"))
        {
            return Unauthorized(new { success = false, message = "Autenticação da máquina inválida ou expirada." });
        }

        var observedRequest = new ClientHeartbeatRequest
        {
            MachineKey = request.MachineKey,
            RequestTimestampUtc = request.RequestTimestampUtc,
            Nonce = request.Nonce,
            MachineProof = request.MachineProof,
            Hostname = request.Hostname,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? request.IpAddress,
            Status = request.Status,
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

        if (!IsMachineAuthenticated(request.MachineKey, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "login"))
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

        if (!IsMachineAuthenticated(request.MachineKey, request.RequestTimestampUtc, request.Nonce, request.MachineProof, "requests"))
        {
            return Unauthorized(new { success = false, message = "Autenticação da máquina inválida ou expirada." });
        }

        var response = await cafeService.SubmitClientRequestsAsync(request, cancellationToken);
        return Ok(response);
    }
}
