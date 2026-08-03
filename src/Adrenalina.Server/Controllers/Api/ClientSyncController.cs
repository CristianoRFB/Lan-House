using Adrenalina.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Adrenalina.Server.Controllers.Api;

[ApiController]
[Route("api/client")]
[EnableRateLimiting("client-api")]
public sealed class ClientSyncController(ICafeManagementService cafeService) : ControllerBase
{
    [HttpPost("heartbeat")]
    public async Task<ActionResult<ClientHeartbeatResponse>> Heartbeat([FromBody] ClientHeartbeatRequest request, CancellationToken cancellationToken)
    {
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
        var response = await cafeService.LoginClientAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<OperationResult>> Requests([FromBody] ClientRequestBatchRequest request, CancellationToken cancellationToken)
    {
        var response = await cafeService.SubmitClientRequestsAsync(request, cancellationToken);
        return Ok(response);
    }
}
