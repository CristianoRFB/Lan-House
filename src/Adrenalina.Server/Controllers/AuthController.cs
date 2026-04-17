using System.Security.Claims;
using Adrenalina.Application;
using Adrenalina.Server.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
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

    [HttpPost("/auth/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
