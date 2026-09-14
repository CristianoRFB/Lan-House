using Adrenalina.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Adrenalina.Server.Controllers;

[Authorize(Roles = "Admin")]
public sealed class BackupController(ICafeManagementService cafeService) : ControllerBase
{
    [HttpGet("/diagnostico/backup")]
    public async Task<ActionResult<BackupValidationResult>> Validate([FromQuery] string path, CancellationToken cancellationToken)
    {
        return Ok(await cafeService.ValidateBackupAsync(path, cancellationToken));
    }
}
