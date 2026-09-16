using System.Net;
using System.Security.Claims;
using Adrenalina.Application;
using Adrenalina.Server.Infrastructure;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Adrenalina.Server.Controllers;

public sealed class AuthController(IAdminAuthService authService) : Controller
{
    [HttpGet("/auth/login")]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel());
    }

    [HttpPost("/auth/login")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("admin-login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var admin = await authService.ValidateAsync(model.Login, model.Password, cancellationToken);
        if (admin is null)
        {
            model.ErrorMessage = "Credenciais inválidas.";
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.DisplayName),
            new(ClaimTypes.Role, admin.ProfileType.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return RedirectToAction("Index", "Dashboard");
    }

    [AllowAnonymous]
    [HttpGet("/auth/recover")]
    public IActionResult Recover()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new AdminAccessRecoveryViewModel());
    }

    [AllowAnonymous]
    [HttpPost("/auth/recover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return View(new AdminAccessRecoveryViewModel
            {
                ErrorMessage = "A recuperação só pode ser feita no próprio computador ADMIN."
            });
        }

        var recovery = await authService.RecoverAdminAccessAsync(cancellationToken);
        if (recovery is null)
        {
            return View(new AdminAccessRecoveryViewModel
            {
                ErrorMessage = "A conta admin não foi encontrada. Feche e abra o ADMIN para concluir a inicialização."
            });
        }

        return View(new AdminAccessRecoveryViewModel
        {
            TemporaryPassword = recovery.TemporaryPassword,
            AccessFilePath = recovery.AccessFilePath
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("/auth/change-password")]
    public IActionResult ChangePassword()
    {
        return View(new ChangeAdminPasswordViewModel());
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("/auth/change-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangeAdminPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!IsLocalRequest())
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            ModelState.AddModelError(string.Empty, "A troca rápida só pode ser feita no próprio computador ADMIN.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.ChangeAdminPasswordAsync(User.GetActorId(), model.NewPassword, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost("/auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("/auth/access-denied")]
    public IActionResult AccessDenied()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction(nameof(Login));
        }

        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private bool IsLocalRequest()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is null || IPAddress.IsLoopback(address);
    }
}
