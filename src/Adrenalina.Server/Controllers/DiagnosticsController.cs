using Adrenalina.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize(Roles = "Admin")]
public sealed class DiagnosticsController(ICafeManagementService cafeService) : ControllerBase
{
    [HttpGet("/diagnostico/integridade")]
    public async Task<ActionResult<DatabaseIntegrityResult>> Integrity(CancellationToken cancellationToken)
    {
        return Ok(await cafeService.CheckDatabaseIntegrityAsync(cancellationToken));
    }
}
