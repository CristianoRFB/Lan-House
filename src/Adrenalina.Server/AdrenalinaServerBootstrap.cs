using Adrenalina.Application;
using Adrenalina.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Adrenalina.Server;

public static class AdrenalinaServerBootstrap
{
    public static WebApplication BuildApplication(AdrenalinaServerHostOptions? options = null)
    {
        options ??= new AdrenalinaServerHostOptions();

        var builderOptions = new WebApplicationOptions
        {
            Args = options.Args,
            ApplicationName = typeof(AdrenalinaServerBootstrap).Assembly.FullName,
            ContentRootPath = options.ContentRootPath ?? AppContext.BaseDirectory,
            WebRootPath = options.WebRootPath ?? Path.Combine(options.ContentRootPath ?? AppContext.BaseDirectory, "wwwroot")
        };

        var builder = WebApplication.CreateBuilder(builderOptions);

        // O servidor embutido precisa rodar sem depender de acesso ao Event Log do Windows.
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole();
        builder.Logging.AddDebug();

        if (!string.IsNullOrWhiteSpace(options.DataRootPath))
        {
            builder.Configuration["Adrenalina:RootDirectory"] = options.DataRootPath;
        }

        if (!string.IsNullOrWhiteSpace(options.Urls))
        {
            builder.WebHost.UseUrls(options.Urls);
        }

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookieOptions =>
            {
                cookieOptions.LoginPath = "/auth/login";
                cookieOptions.AccessDeniedPath = "/auth/login";
                cookieOptions.Cookie.Name = "Adrenalina.Admin";
                cookieOptions.SlidingExpiration = true;
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllersWithViews();
        builder.Services.AddAdrenalinaServerPlatform(builder.Configuration, builder.Environment);

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/error");
            app.UseHsts();
        }

        if (options.UseHttpsRedirection)
        {
            app.UseHttpsRedirection();
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Dashboard}/{action=Index}/{id?}");

        return app;
    }

    public static async Task InitializeAsync(WebApplication app, CancellationToken cancellationToken = default)
    {
        using var scope = app.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICafeManagementService>();
        await service.EnsureInitializedAsync(cancellationToken);
    }
}
