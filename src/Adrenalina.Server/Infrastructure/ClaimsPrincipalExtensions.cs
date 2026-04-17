using System.Security.Claims;

namespace Adrenalina.Server.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetActorId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }
}
