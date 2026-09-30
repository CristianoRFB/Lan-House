using Adrenalina.Application;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers.Api;

[ApiController]
[Route("api/client")]
public sealed class ClientSyncController(ICafeManagementService cafeService) : ControllerBase
{
    [HttpPost("heartbeat")]
    public async Task<ActionResult<ClientHeartbeatResponse>> Heartbeat([FromBody] ClientHeartbeatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MachineKey))
        {
            return BadRequest("A chave da maquina e obrigatoria.");
        }

        var response = await cafeService.SyncClientHeartbeatAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<ActionResult<ClientLoginResponse>> Login([FromBody] ClientLoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MachineKey))
        {
            return BadRequest("A chave da maquina e obrigatoria.");
        }

        var response = await cafeService.LoginClientAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("requests")]
    public async Task<ActionResult<OperationResult>> Requests([FromBody] ClientRequestBatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MachineKey))
        {
            return BadRequest("A chave da maquina e obrigatoria.");
        }

        var response = await cafeService.SubmitClientRequestsAsync(request, cancellationToken);
        return response.Success ? Ok(response) : Conflict(response);
    }
}
