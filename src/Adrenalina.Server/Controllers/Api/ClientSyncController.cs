using Adrenalina.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Adrenalina.Server.Controllers.Api;

[ApiController]
[Route("api/client")]
[EnableRateLimiting("client-api")]
public sealed class ClientSyncController(ICafeManagementService cafeService) : ControllerBase
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

    [HttpPost("heartbeat")]
    public async Task<ActionResult<ClientHeartbeatResponse>> Heartbeat([FromBody] ClientHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (!IsProtocolSupported(request.ProtocolVersion))
        {
            return BadRequest(ProtocolError());
        }

        var observedRequest = new ClientHeartbeatRequest
        {
            MachineKey = request.MachineKey,
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

        var response = await cafeService.SubmitClientRequestsAsync(request, cancellationToken);
        return Ok(response);
    }
}
